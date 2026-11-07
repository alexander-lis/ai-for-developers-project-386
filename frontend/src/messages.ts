/** Network and server failures: one text for every screen, in a toast or instead of a list. */
export const serverUnavailable = "Не удалось связаться с сервером, попробуйте ещё раз";

/** A 400 the form could not attach to one of its fields. */
export const checkInput = "Проверьте введённые данные";

/** The Owner wrote to a meeting type that was deleted since the list was loaded. */
export const meetingTypeAlreadyDeleted = "Этот тип встречи уже удалён";

/** A Guest picked a meeting type that the Owner has deleted since the list was loaded. */
export const meetingTypeUnavailable = "Этот тип встречи больше недоступен";

export const noMeetingTypes = "Владелец ещё не добавил типы встреч";

const dayWords: Partial<Record<Intl.LDMLPluralRule, string>> = { one: "день", few: "дня" };

/** No free slot on any of the `days` days of the booking window the server returned. */
export function noFreeSlots(days: number): string {
  const word = dayWords[new Intl.PluralRules("ru-RU").select(days)] ?? "дней";
  return `В ближайшие ${days} ${word} свободных слотов нет`;
}

/** The slot stopped being free after the Guest saw it: see "Конфликт" in GLOSSARY.md. */
export const bookingConflict = "Это время уже занято, выберите другое";

export const noMeetings = "Предстоящих встреч пока нет";
