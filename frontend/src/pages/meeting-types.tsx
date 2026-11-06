import { useState } from "react";
import { toast } from "sonner";
import { meetingTypesList, type MeetingType } from "@/client";
import { formatDuration } from "@/calendar-time";
import { LoadStatus } from "@/components/load-status";
import {
  DeleteMeetingTypeDialog,
  MeetingTypeDialog,
  type WriteOutcome,
} from "@/components/meeting-type-dialogs";
import { Button } from "@/components/ui/button";
import { meetingTypeAlreadyDeleted, noMeetingTypes } from "@/messages";
import { useLoad } from "@/use-load";

/** A dialog keeps its meeting type while closing, so the closing animation shows the same text. */
type DialogState = { open: boolean; meetingType?: MeetingType };

async function fetchMeetingTypes() {
  // Network and server failures come back as `error` with no `data`.
  const { data } = await meetingTypesList();
  return data;
}

/** Owner screen: the Owner's meeting types, as the server sorts them, with create, edit, delete. */
export function MeetingTypesPage() {
  const { state, reload, refresh } = useLoad(fetchMeetingTypes);
  const [editor, setEditor] = useState<DialogState>({ open: false });
  const [deletion, setDeletion] = useState<DialogState>({ open: false });

  // After a write the list stays on screen until the fresh one arrives.
  function finishWrite(outcome: WriteOutcome) {
    setEditor((dialog) => ({ ...dialog, open: false }));
    setDeletion((dialog) => ({ ...dialog, open: false }));
    if (outcome === "meetingTypeDeleted") {
      toast(meetingTypeAlreadyDeleted);
    }
    refresh();
  }

  return (
    <div className="flex flex-col gap-8 py-20">
      <div className="flex items-center justify-between gap-6">
        <h1 className="text-3xl font-semibold tracking-tight">Типы встреч</h1>
        {state.status === "loaded" && (
          <Button onClick={() => setEditor({ open: true })}>Создать тип встречи</Button>
        )}
      </div>
      <LoadStatus status={state.status} onRetry={reload} />
      {state.status === "loaded" && (
        <MeetingTypeList
          meetingTypes={state.data}
          onEdit={(meetingType) => setEditor({ open: true, meetingType })}
          onDelete={(meetingType) => setDeletion({ open: true, meetingType })}
        />
      )}
      <MeetingTypeDialog
        {...editor}
        onOpenChange={(open) => setEditor({ ...editor, open })}
        onDone={finishWrite}
      />
      <DeleteMeetingTypeDialog
        {...deletion}
        onOpenChange={(open) => setDeletion({ ...deletion, open })}
        onDone={finishWrite}
      />
    </div>
  );
}

function MeetingTypeList({
  meetingTypes,
  onEdit,
  onDelete,
}: {
  meetingTypes: MeetingType[];
  onEdit: (meetingType: MeetingType) => void;
  onDelete: (meetingType: MeetingType) => void;
}) {
  if (meetingTypes.length === 0) {
    return <p className="text-muted-foreground">{noMeetingTypes}</p>;
  }

  return (
    <ul className="flex flex-col gap-3">
      {meetingTypes.map((meetingType) => (
        <li
          key={meetingType.id}
          className="flex items-start justify-between gap-6 rounded-xl border p-6"
        >
          <div className="flex flex-col gap-1">
            <h2 className="font-medium">{meetingType.name}</h2>
            {meetingType.description && (
              <p className="text-sm whitespace-pre-line text-muted-foreground">
                {meetingType.description}
              </p>
            )}
          </div>
          <div className="flex shrink-0 items-center gap-4">
            <span className="text-sm text-muted-foreground">{formatDuration(meetingType)}</span>
            <Button
              variant="outline"
              size="sm"
              aria-label={`Изменить «${meetingType.name}»`}
              onClick={() => onEdit(meetingType)}
            >
              Изменить
            </Button>
            <Button
              variant="destructive"
              size="sm"
              aria-label={`Удалить «${meetingType.name}»`}
              onClick={() => onDelete(meetingType)}
            >
              Удалить
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}
