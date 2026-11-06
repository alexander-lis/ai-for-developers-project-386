import type { MeetingTypeInput } from "@/client";
import { checkInput } from "@/messages";

/** What the Owner typed into the meeting type form. */
export type MeetingTypeFormValues = {
  name: string;
  description: string;
  durationMinutes: string;
};

export type MeetingTypeFormErrors = Partial<Record<keyof MeetingTypeFormValues, string>>;

/** Checks the form by the contract rules and builds the request body from trimmed values. */
export function validateMeetingTypeForm(
  values: MeetingTypeFormValues,
): { input: MeetingTypeInput } | { errors: MeetingTypeFormErrors } {
  const name = values.name.trim();
  const description = values.description.trim();
  const duration = values.durationMinutes.trim();
  // Digits only: Number() would also take "1e2", "0x1E" and "".
  const durationMinutes = /^\d+$/.test(duration) ? Number(duration) : Number.NaN;

  const errors: MeetingTypeFormErrors = {};
  if (!name) {
    errors.name = "Введите название";
  } else if (name.length > 100) {
    errors.name = "Не длиннее 100 символов";
  }

  if (description.length > 500) {
    errors.description = "Не длиннее 500 символов";
  }

  if (!(durationMinutes >= 5 && durationMinutes <= 480)) {
    errors.durationMinutes = "От 5 до 480 минут";
  }

  if (Object.keys(errors).length > 0) {
    return { errors };
  }

  return { input: { name, ...(description && { description }), durationMinutes } };
}

const fields = ["name", "description", "durationMinutes"] as const;

/**
 * Messages of a 400 by field name, as the server sent them, under the form's fields. A key with no
 * field, or no key at all, leaves a form error instead.
 */
export function meetingTypeServerErrors(serverErrors: Record<string, string[]>): {
  errors: MeetingTypeFormErrors;
  formError?: string;
} {
  const errors: MeetingTypeFormErrors = {};
  for (const field of fields) {
    if (serverErrors[field]?.length) {
      errors[field] = serverErrors[field].join(" ");
    }
  }
  const unmatched = Object.keys(serverErrors).some(
    (key) => !(fields as readonly string[]).includes(key),
  );
  return unmatched || Object.keys(errors).length === 0
    ? { errors, formError: checkInput }
    : { errors };
}
