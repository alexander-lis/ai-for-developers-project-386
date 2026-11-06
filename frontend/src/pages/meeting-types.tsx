import { useEffect, useState } from "react";
import { toast } from "sonner";
import { meetingTypesList, type MeetingType } from "@/client";
import { LoadError } from "@/components/load-error";
import {
  DeleteMeetingTypeDialog,
  MeetingTypeDialog,
  type WriteOutcome,
} from "@/components/meeting-type-dialogs";
import { Button } from "@/components/ui/button";
import { meetingTypeAlreadyDeleted, noMeetingTypes } from "@/messages";

type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; meetingTypes: MeetingType[] };

/** A dialog keeps its meeting type while closing, so the closing animation shows the same text. */
type DialogState = { open: boolean; meetingType?: MeetingType };

async function fetchMeetingTypes(): Promise<State> {
  // Network and server failures come back as `error` with no `data`.
  const { data } = await meetingTypesList();
  return data ? { status: "loaded", meetingTypes: data } : { status: "error" };
}

/** Owner screen: the Owner's meeting types, as the server sorts them, with create, edit, delete. */
export function MeetingTypesPage() {
  const [state, setState] = useState<State>({ status: "loading" });
  const [editor, setEditor] = useState<DialogState>({ open: false });
  const [deletion, setDeletion] = useState<DialogState>({ open: false });

  // Fetched on every visit: the page mounts anew on each navigation.
  useEffect(() => {
    let current = true;
    void fetchMeetingTypes().then((next) => current && setState(next));
    return () => {
      current = false;
    };
  }, []);

  function retry() {
    setState({ status: "loading" });
    void fetchMeetingTypes().then(setState);
  }

  // After a write the list stays on screen until the fresh one arrives.
  function finishWrite(outcome: WriteOutcome) {
    setEditor((dialog) => ({ ...dialog, open: false }));
    setDeletion((dialog) => ({ ...dialog, open: false }));
    if (outcome === "meetingTypeDeleted") {
      toast(meetingTypeAlreadyDeleted);
    }
    void fetchMeetingTypes().then(setState);
  }

  return (
    <div className="flex flex-col gap-8 py-20">
      <div className="flex items-center justify-between gap-6">
        <h1 className="text-3xl font-semibold tracking-tight">Типы встреч</h1>
        {state.status === "loaded" && (
          <Button onClick={() => setEditor({ open: true })}>Создать тип встречи</Button>
        )}
      </div>
      {state.status === "loading" && <p className="text-muted-foreground">Загрузка…</p>}
      {state.status === "error" && <LoadError onRetry={retry} />}
      {state.status === "loaded" && (
        <MeetingTypeList
          meetingTypes={state.meetingTypes}
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
            <span className="text-sm text-muted-foreground">{meetingType.durationMinutes} мин</span>
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
