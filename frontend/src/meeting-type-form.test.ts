import { expect, it } from "vitest";
import { meetingTypeServerErrors, validateMeetingTypeForm } from "./meeting-type-form";

it("turns valid values into a trimmed meeting type input", () => {
  expect(
    validateMeetingTypeForm({
      name: "  Знакомство ",
      description: " Первый звонок\n",
      durationMinutes: " 30 ",
    }),
  ).toEqual({ input: { name: "Знакомство", description: "Первый звонок", durationMinutes: 30 } });
});

it("leaves out a blank description", () => {
  expect(
    validateMeetingTypeForm({ name: "Знакомство", description: "  \n", durationMinutes: "30" }),
  ).toEqual({ input: { name: "Знакомство", durationMinutes: 30 } });
});

it.each([
  ["", "Введите название"],
  ["   ", "Введите название"],
  ["а".repeat(101), "Не длиннее 100 символов"],
])("rejects name %j with %s", (name, message) => {
  expect(validateMeetingTypeForm({ name, description: "", durationMinutes: "30" })).toEqual({
    errors: { name: message },
  });
});

it("rejects a description longer than 500 characters", () => {
  expect(
    validateMeetingTypeForm({
      name: "Знакомство",
      description: "а".repeat(501),
      durationMinutes: "30",
    }),
  ).toEqual({ errors: { description: "Не длиннее 500 символов" } });
});

it("accepts a description of 500 characters after trimming", () => {
  expect(
    validateMeetingTypeForm({
      name: "Знакомство",
      description: ` ${"а".repeat(500)}\n`,
      durationMinutes: "30",
    }),
  ).toHaveProperty("input.description", "а".repeat(500));
});

it.each(["", "4", "481", "30.5", "30 мин", "-30", "1e2"])(
  "rejects duration %j",
  (durationMinutes) => {
    expect(
      validateMeetingTypeForm({ name: "Знакомство", description: "", durationMinutes }),
    ).toEqual({ errors: { durationMinutes: "От 5 до 480 минут" } });
  },
);

it.each([
  ["5", 5],
  ["480", 480],
])("accepts duration %j", (durationMinutes, minutes) => {
  expect(
    validateMeetingTypeForm({ name: "Знакомство", description: "", durationMinutes }),
  ).toHaveProperty("input.durationMinutes", minutes);
});

it("reports every invalid field at once", () => {
  expect(
    validateMeetingTypeForm({ name: "", description: "а".repeat(501), durationMinutes: "" }),
  ).toEqual({
    errors: {
      name: "Введите название",
      description: "Не длиннее 500 символов",
      durationMinutes: "От 5 до 480 минут",
    },
  });
});

it("counts name length after trimming", () => {
  expect(
    validateMeetingTypeForm({
      name: ` ${"а".repeat(100)} `,
      description: "",
      durationMinutes: "30",
    }),
  ).toHaveProperty("input.name", "а".repeat(100));
});

it("puts server errors under the fields they name", () => {
  expect(
    meetingTypeServerErrors({
      name: ["Не длиннее 100 символов"],
      durationMinutes: ["Слишком долго", "Не число"],
    }),
  ).toEqual({
    errors: { name: "Не длиннее 100 символов", durationMinutes: "Слишком долго Не число" },
  });
});

it("falls back to a form error for keys with no field", () => {
  expect(
    meetingTypeServerErrors({ name: ["Введите название"], body: ["Не удалось прочитать JSON"] }),
  ).toEqual({ errors: { name: "Введите название" }, formError: "Проверьте введённые данные" });
  expect(meetingTypeServerErrors({})).toEqual({
    errors: {},
    formError: "Проверьте введённые данные",
  });
});
