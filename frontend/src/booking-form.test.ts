import { expect, it } from "vitest";
import { validateBookingForm } from "./booking-form";

it("trims the guest name", () => {
  expect(validateBookingForm({ guestName: "  Анна\n" })).toEqual({ guestName: "Анна" });
});

it.each([
  ["", "Введите имя"],
  ["   ", "Введите имя"],
  ["а".repeat(101), "Не длиннее 100 символов"],
])("rejects guest name %j with %s", (guestName, message) => {
  expect(validateBookingForm({ guestName })).toEqual({ errors: { guestName: message } });
});

it("counts the length after trimming", () => {
  expect(validateBookingForm({ guestName: ` ${"а".repeat(100)} ` })).toEqual({
    guestName: "а".repeat(100),
  });
});
