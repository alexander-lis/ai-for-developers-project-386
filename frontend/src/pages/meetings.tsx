import { calendarGet, meetingsList } from "@/client";
import { LoadStatus } from "@/components/load-status";
import { MeetingCard } from "@/components/meeting-card";
import { noMeetings } from "@/messages";
import { useLoad } from "@/use-load";

async function fetchMeetings() {
  // Network and server failures come back as `error` with no `data`.
  const [meetings, calendar] = await Promise.all([meetingsList(), calendarGet()]);
  return meetings.data && calendar.data
    ? { meetings: meetings.data, timeZone: calendar.data.timeZone }
    : undefined;
}

/**
 * Owner "Встречи" and Guest "Мои встречи": the same upcoming meetings of every Guest, as the server
 * sorts them by start.
 */
export function MeetingsPage({ title }: { title: string }) {
  const { state, reload } = useLoad(fetchMeetings);

  return (
    <div className="flex flex-col gap-8 py-20">
      <h1 className="text-3xl font-semibold tracking-tight">{title}</h1>
      <LoadStatus status={state.status} onRetry={reload} />
      {state.status === "loaded" &&
        (state.data.meetings.length === 0 ? (
          <p className="text-muted-foreground">{noMeetings}</p>
        ) : (
          <ul aria-label="Предстоящие встречи" className="flex flex-col gap-3">
            {state.data.meetings.map((meeting) => (
              <MeetingCard key={meeting.id} meeting={meeting} timeZone={state.data.timeZone} />
            ))}
          </ul>
        ))}
    </div>
  );
}
