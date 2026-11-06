import { useEffect, useState } from "react";
import { meetingTypesList, type MeetingType } from "@/client";
import { LoadError } from "@/components/load-error";

type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; meetingTypes: MeetingType[] };

async function fetchMeetingTypes(): Promise<State> {
  // Network and server failures come back as `error` with no `data`.
  const { data } = await meetingTypesList();
  return data ? { status: "loaded", meetingTypes: data } : { status: "error" };
}

/** Owner screen: the Owner's meeting types, as the server sorts them. */
export function MeetingTypesPage() {
  const [state, setState] = useState<State>({ status: "loading" });

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

  return (
    <div className="flex flex-col gap-8 py-20">
      <h1 className="text-3xl font-semibold tracking-tight">Типы встреч</h1>
      {state.status === "loading" && <p className="text-muted-foreground">Загрузка…</p>}
      {state.status === "error" && <LoadError onRetry={retry} />}
      {state.status === "loaded" && <MeetingTypeList meetingTypes={state.meetingTypes} />}
    </div>
  );
}

function MeetingTypeList({ meetingTypes }: { meetingTypes: MeetingType[] }) {
  if (meetingTypes.length === 0) {
    return <p className="text-muted-foreground">Владелец ещё не добавил типы встреч</p>;
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
              <p className="text-sm text-muted-foreground">{meetingType.description}</p>
            )}
          </div>
          <span className="shrink-0 text-sm text-muted-foreground">
            {meetingType.durationMinutes} мин
          </span>
        </li>
      ))}
    </ul>
  );
}
