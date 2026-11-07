/** What the Guest typed into the booking form. */
export type BookingFormValues = { guestName: string };

export type BookingFormErrors = Partial<Record<keyof BookingFormValues, string>>;

/** Checks the form by the contract rules; the guest name comes back trimmed. */
export function validateBookingForm(
  values: BookingFormValues,
): { guestName: string } | { errors: BookingFormErrors } {
  const guestName = values.guestName.trim();
  if (!guestName) {
    return { errors: { guestName: "Введите имя" } };
  }
  if (guestName.length > 100) {
    return { errors: { guestName: "Не длиннее 100 символов" } };
  }
  return { guestName };
}
