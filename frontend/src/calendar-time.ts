/** Intl gives "нояб.", "сент."; the interface wants three letters, in the genitive after a day. */
const months = ["янв", "фев", "мар", "апр", "мая", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];

/**
 * A slot or a meeting as "пн, 10 ноя, 10:00–11:00" in the calendar time zone, whatever the
 * browser time zone is. Takes the ISO moments the API returns.
 */
export function formatTimeRange(range: { start: string; end: string }, timeZone: string): string {
  const format = new Intl.DateTimeFormat("ru-RU", {
    timeZone,
    weekday: "short",
    day: "numeric",
    month: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  });
  const start = partsOf(format, range.start);
  const end = partsOf(format, range.end);
  // With a numeric month Intl pads the day: "09".
  const day = Number(start.day);
  const month = months[Number(start.month) - 1];
  return `${start.weekday}, ${day} ${month}, ${start.hour}:${start.minute}–${end.hour}:${end.minute}`;
}

/**
 * A duration as "60 мин": of a meeting type, or of a meeting from its own start and end, since the
 * meeting type may have been edited or deleted after the meeting was booked.
 */
export function formatDuration(
  of: { durationMinutes: number } | { start: string; end: string },
): string {
  const minutes =
    "durationMinutes" in of
      ? of.durationMinutes
      : (Date.parse(of.end) - Date.parse(of.start)) / 60_000;
  return `${minutes} мин`;
}

/**
 * A day from the API ("2026-11-09") as a local midnight, the way the date picker counts days.
 * The picker only labels the day, so the browser time zone does not shift it.
 */
export function fromPlainDate(date: string): Date {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(year, month - 1, day);
}

/** The day the date picker shows for a local date, as the API writes it. */
export function toPlainDate(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function partsOf(format: Intl.DateTimeFormat, moment: string) {
  const parts = format.formatToParts(new Date(moment));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((p) => p.type === type)?.value ?? "";
  return {
    weekday: part("weekday"),
    day: part("day"),
    month: part("month"),
    hour: part("hour"),
    minute: part("minute"),
  };
}
