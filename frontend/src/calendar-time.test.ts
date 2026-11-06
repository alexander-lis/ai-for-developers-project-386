import { expect, it } from "vitest";
import { formatDuration, formatTimeRange, fromPlainDate, toPlainDate } from "./calendar-time";

const moscow = "Europe/Moscow";

it("formats a time range as day and time range in the calendar time zone", () => {
  expect(
    formatTimeRange({ start: "2026-11-09T07:00:00Z", end: "2026-11-09T08:00:00Z" }, moscow),
  ).toBe("пн, 9 ноя, 10:00–11:00");
});

it("takes the day from the calendar time zone, not from UTC", () => {
  expect(
    formatTimeRange({ start: "2026-11-09T22:30:00Z", end: "2026-11-09T23:00:00Z" }, moscow),
  ).toBe("вт, 10 ноя, 01:30–02:00");
});

it("follows the given time zone", () => {
  expect(
    formatTimeRange(
      { start: "2026-11-09T07:00:00Z", end: "2026-11-09T08:00:00Z" },
      "Asia/Yekaterinburg",
    ),
  ).toBe("пн, 9 ноя, 12:00–13:00");
});

it("writes midnight as 00:00", () => {
  expect(
    formatTimeRange({ start: "2026-11-09T21:00:00Z", end: "2026-11-09T21:30:00Z" }, moscow),
  ).toBe("вт, 10 ноя, 00:00–00:30");
});

it("abbreviates every month to three letters in the genitive", () => {
  const months = Array.from({ length: 12 }, (_, index) => {
    const start = new Date(Date.UTC(2026, index, 15, 9)).toISOString();
    return formatTimeRange({ start, end: start }, "UTC").split(", ")[1];
  });

  expect(months).toEqual([
    "15 янв",
    "15 фев",
    "15 мар",
    "15 апр",
    "15 мая",
    "15 июн",
    "15 июл",
    "15 авг",
    "15 сен",
    "15 окт",
    "15 ноя",
    "15 дек",
  ]);
});

it("turns a plain date into a local date of the same day for the date picker", () => {
  const date = fromPlainDate("2026-11-09");

  expect([date.getFullYear(), date.getMonth(), date.getDate(), date.getHours()]).toEqual([
    2026, 10, 9, 0,
  ]);
});

it("turns a picked local date back into a plain date", () => {
  expect(toPlainDate(new Date(2026, 0, 5, 15, 30))).toBe("2026-01-05");
  expect(toPlainDate(fromPlainDate("2026-12-31"))).toBe("2026-12-31");
});

it("writes the duration of a meeting in minutes from its start and end", () => {
  expect(formatDuration({ start: "2026-11-09T07:00:00Z", end: "2026-11-09T08:00:00Z" })).toBe(
    "60 мин",
  );
  expect(formatDuration({ start: "2026-11-09T07:00:00Z", end: "2026-11-09T07:45:00Z" })).toBe(
    "45 мин",
  );
});

it("writes the duration of a meeting type in minutes", () => {
  expect(formatDuration({ durationMinutes: 45 })).toBe("45 мин");
});

it("keeps long durations in minutes, as the meeting types screen does", () => {
  expect(formatDuration({ start: "2026-11-09T06:00:00Z", end: "2026-11-09T14:00:00Z" })).toBe(
    "480 мин",
  );
});
