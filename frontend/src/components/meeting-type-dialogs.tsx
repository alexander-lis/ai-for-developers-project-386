import { useId, useState, type FormEvent } from "react";
import { toast } from "sonner";
import {
  meetingTypesCreate,
  meetingTypesDelete,
  meetingTypesUpdate,
  type MeetingType,
} from "@/client";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  meetingTypeServerErrors,
  validateMeetingTypeForm,
  type MeetingTypeFormErrors,
  type MeetingTypeFormValues,
} from "@/meeting-type-form";
import { serverUnavailable } from "@/messages";

/** How a write ended for the list: it changed, or the meeting type was already deleted (404). */
export type WriteOutcome = "done" | "meetingTypeDeleted";

type DialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Network and server failures do not end the write: the dialog stays open with a toast. */
  onDone: (outcome: WriteOutcome) => void;
};

/** One form for creating a meeting type and for editing one. */
export function MeetingTypeDialog({
  meetingType,
  ...props
}: DialogProps & {
  /** The meeting type to edit; without it the dialog creates a new one. */
  meetingType?: MeetingType;
}) {
  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{meetingType ? "Изменить тип встречи" : "Новый тип встречи"}</DialogTitle>
        </DialogHeader>
        {/* The popup unmounts when closed, so every opening starts from fresh values. */}
        <MeetingTypeForm meetingType={meetingType} onDone={props.onDone} />
      </DialogContent>
    </Dialog>
  );
}

function MeetingTypeForm({
  meetingType,
  onDone,
}: {
  meetingType?: MeetingType;
  onDone: DialogProps["onDone"];
}) {
  const id = useId();
  const [values, setValues] = useState<MeetingTypeFormValues>({
    name: meetingType?.name ?? "",
    description: meetingType?.description ?? "",
    durationMinutes: meetingType ? String(meetingType.durationMinutes) : "",
  });
  const [errors, setErrors] = useState<MeetingTypeFormErrors>({});
  const [formError, setFormError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  function change(field: keyof MeetingTypeFormValues, value: string) {
    setValues({ ...values, [field]: value });
    setErrors({ ...errors, [field]: undefined });
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const checked = validateMeetingTypeForm(values);
    if ("errors" in checked) {
      setErrors(checked.errors);
      return;
    }

    setFormError(undefined);
    setSubmitting(true);
    const { data, error, response } = meetingType
      ? await meetingTypesUpdate({ path: { id: meetingType.id }, body: checked.input })
      : await meetingTypesCreate({ body: checked.input });
    setSubmitting(false);

    if (data) {
      onDone("done");
    } else if (response?.status === 404) {
      onDone("meetingTypeDeleted");
    } else if (response?.status === 400) {
      // The form checks the same rules first, so this is rare.
      const mapped = meetingTypeServerErrors(error && "errors" in error ? error.errors : {});
      setErrors(mapped.errors);
      setFormError(mapped.formError);
    } else {
      toast.error(serverUnavailable);
    }
  }

  // Wiring shared by the three controls: id for the label, value, error state.
  const control = (field: keyof MeetingTypeFormValues) => ({
    id: `${id}-${field}`,
    value: values[field],
    "aria-invalid": Boolean(errors[field]),
    onChange: (event: { target: { value: string } }) => change(field, event.target.value),
  });

  return (
    <form noValidate onSubmit={(event) => void submit(event)} className="flex flex-col gap-4">
      <FieldGroup>
        <Field data-invalid={Boolean(errors.name)}>
          <FieldLabel htmlFor={`${id}-name`}>Название</FieldLabel>
          <Input {...control("name")} />
          <FieldError>{errors.name}</FieldError>
        </Field>
        <Field data-invalid={Boolean(errors.description)}>
          <FieldLabel htmlFor={`${id}-description`}>Описание</FieldLabel>
          <Textarea {...control("description")} />
          <FieldError>{errors.description}</FieldError>
        </Field>
        <Field data-invalid={Boolean(errors.durationMinutes)}>
          <FieldLabel htmlFor={`${id}-durationMinutes`}>Продолжительность, минут</FieldLabel>
          <Input inputMode="numeric" {...control("durationMinutes")} />
          <FieldError>{errors.durationMinutes}</FieldError>
        </Field>
      </FieldGroup>
      <FieldError>{formError}</FieldError>
      <DialogFooter>
        <DialogClose render={<Button variant="outline" />}>Отмена</DialogClose>
        <Button type="submit" disabled={submitting}>
          {meetingType ? "Сохранить" : "Создать"}
        </Button>
      </DialogFooter>
    </form>
  );
}

/** Confirms deleting a meeting type; meetings already booked keep their snapshot of it. */
export function DeleteMeetingTypeDialog({
  meetingType,
  open,
  onOpenChange,
  onDone,
}: DialogProps & { meetingType?: MeetingType }) {
  const [deleting, setDeleting] = useState(false);

  async function confirm(id: string) {
    setDeleting(true);
    const { response } = await meetingTypesDelete({ path: { id } });
    setDeleting(false);

    // 204 has no Content-Type, so the client leaves `data` null: success is the status.
    if (response?.ok) {
      onDone("done");
    } else if (response?.status === 404) {
      onDone("meetingTypeDeleted");
    } else {
      toast.error(serverUnavailable);
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      {meetingType && (
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Удалить тип встречи «{meetingType.name}»?</AlertDialogTitle>
            <AlertDialogDescription>Уже записанные встречи сохранятся</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Отмена</AlertDialogCancel>
            <Button
              variant="destructive"
              disabled={deleting}
              onClick={() => void confirm(meetingType.id)}
            >
              Удалить
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      )}
    </AlertDialog>
  );
}
