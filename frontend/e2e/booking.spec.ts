import { expect, test, type Page } from "@playwright/test";
import type { BookingDay, MeetingType } from "@/client";

// The tests of a run share one database and live by real clocks, so each run makes its own
// meeting type and each test finds its meeting by a guest name of its own.
const run = Date.now().toString(36);
const meetingTypeName = `Созвон e2e ${run}`;
let meetingType: MeetingType;

test.beforeAll(async ({ playwright }, testInfo) => {
  const api = await playwright.request.newContext({ baseURL: testInfo.project.use.baseURL });
  const response = await api.post("/api/meeting-types", {
    data: { name: meetingTypeName, durationMinutes: 60 },
  });
  expect(response.status()).toBe(201);
  meetingType = await response.json();
  await api.dispose();
});

/** Picks the meeting type and the first slot on screen; returns that slot as the server sent it. */
async function pickFirstSlot(page: Page) {
  await page.goto("/book");
  const days = page.waitForResponse(`**/api/meeting-types/${meetingType.id}/booking-days`);
  await page
    .getByRole("region", { name: "Тип встречи" })
    .getByRole("button", { name: meetingTypeName })
    .click();
  // The screen selects the first day with slots by itself.
  const bookingDays: BookingDay[] = await (await days).json();
  const slot = bookingDays.flatMap((day) => day.slots)[0];

  const time = page.getByRole("region", { name: "Время" });
  const slotButton = time.locator("li").getByRole("button").first();
  const slotText = (await slotButton.textContent()) ?? "";
  expect(slotText).not.toBe("");
  await slotButton.click();
  await expect(slotButton).toHaveAttribute("aria-pressed", "true");
  return { slot, slotText, time };
}

async function confirmBooking(page: Page, guestName: string) {
  await page.getByRole("button", { name: "Записаться" }).click();
  const dialog = page.getByRole("dialog", { name: "Записаться на встречу" });
  await dialog.getByLabel("Имя").fill(guestName);
  await dialog.getByRole("button", { name: "Подтвердить" }).click();
  await expect(dialog).toBeHidden();
}

function upcomingMeeting(page: Page, guestName: string) {
  return page
    .getByRole("list", { name: "Предстоящие встречи" })
    .getByRole("listitem")
    .filter({ has: page.getByText(guestName, { exact: true }) });
}

test("гость записывается на Слот, Встречу видят гость в «Моих встречах» и владелец во «Встречах»", async ({
  page,
}) => {
  const guestName = `Гость ${run}`;
  const { slotText, time } = await pickFirstSlot(page);

  await confirmBooking(page, guestName);

  await expect(
    page.getByText(`Вы записаны: ${meetingTypeName}, ${slotText}`, { exact: true }),
  ).toBeVisible();
  await expect(time.getByRole("button", { name: slotText, exact: true })).toHaveCount(0);

  await page.getByRole("link", { name: "Мои встречи" }).click();
  await expect(upcomingMeeting(page, guestName)).toContainText(meetingTypeName);
  await expect(upcomingMeeting(page, guestName)).toContainText(slotText);

  await page.getByRole("button", { name: "Войти как владелец" }).click();
  await page.getByRole("link", { name: "Встречи", exact: true }).click();
  await expect(upcomingMeeting(page, guestName)).toContainText(meetingTypeName);
  await expect(upcomingMeeting(page, guestName)).toContainText(slotText);
});

test("гость записывается на Слот, который заняли, пока он был выбран, и получает Конфликт", async ({
  page,
  request,
}) => {
  const { slot, time } = await pickFirstSlot(page);
  const taken = await request.post("/api/meetings", {
    data: { meetingTypeId: meetingType.id, guestName: `Другой гость ${run}`, start: slot.start },
  });
  expect(taken.status()).toBe(201);

  await confirmBooking(page, `Гость ${run}-2`);

  await expect(time.getByRole("alert")).toHaveText("Это время уже занято, выберите другое");
});
