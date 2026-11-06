import { useEffect, useState } from "react";
import { calendarGet, meetingsList, type Meeting } from "@/client";
import { LoadError } from "@/components/load-error";
import { MeetingCard } from "@/components/meeting-card";
import { noMeetings } from "@/messages";

type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; meetings: Meeting[]; timeZone: string };

async function fetchMeetings(): Promise<State> {
  // Network and server failures come back as `error` with no `data`.
  const [meetings, calendar] = await Promise.all([meetingsList(), calendarGet()]);
  return meetings.data && calendar.data
    ? { status: "loaded", meetings: meetings.data, timeZone: calendar.data.timeZone }
    : { status: "error" };
}

/**
 * Owner "Встречи" and Guest "Мои встречи": the same upcoming meetings of every Guest, as the server
 * sorts them by start.
 */
export function MeetingsPage({ title }: { title: string }) {
  const [state, setState] = useState<State>({ status: "loading" });

  // Fetched on every visit, with no polling or cache: the page mounts anew on each navigation.
  useEffect(() => {
    let current = true;
    void fetchMeetings().then((next) => current && setState(next));
    return () => {
      current = false;
    };
  }, []);

  function retry() {
    setState({ status: "loading" });
    void fetchMeetings().then(setState);
  }

  return (
    <div className="flex flex-col gap-8 py-20">
      <h1 className="text-3xl font-semibold tracking-tight">{title}</h1>
      {state.status === "loading" && <p className="text-muted-foreground">Загрузка…</p>}
      {state.status === "error" && <LoadError onRetry={retry} />}
      {state.status === "loaded" &&
        (state.meetings.length === 0 ? (
          <p className="text-muted-foreground">{noMeetings}</p>
        ) : (
          <ul aria-label="Предстоящие встречи" className="flex flex-col gap-3">
            {state.meetings.map((meeting) => (
              <MeetingCard key={meeting.id} meeting={meeting} timeZone={state.timeZone} />
            ))}
          </ul>
        ))}
    </div>
  );
}
