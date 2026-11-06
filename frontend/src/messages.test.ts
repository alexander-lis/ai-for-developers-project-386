import { expect, it } from "vitest";
import { noFreeSlots } from "./messages";

it("counts the days of the booking window in the no free slots text", () => {
  expect(noFreeSlots(14)).toBe("В ближайшие 14 дней свободных слотов нет");
  expect(noFreeSlots(21)).toBe("В ближайшие 21 день свободных слотов нет");
  expect(noFreeSlots(3)).toBe("В ближайшие 3 дня свободных слотов нет");
});
