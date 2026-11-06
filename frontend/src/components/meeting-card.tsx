import type { Meeting } from "@/client";
import { formatDuration, formatTimeRange } from "@/calendar-time";

/**
 * A meeting block, the same on the Owner and Guest screens. Shows the meeting type name and the
 * time saved at booking, never the current meeting type.
 */
export function MeetingCard({ meeting, timeZone }: { meeting: Meeting; timeZone: string }) {
  return (
    <li className="flex items-start justify-between gap-6 rounded-xl border p-6">
      <div className="flex flex-col gap-1">
        <h2 className="font-medium">{meeting.meetingTypeName}</h2>
        <p className="text-sm">{meeting.guestName}</p>
        <p className="text-sm text-muted-foreground">{formatTimeRange(meeting, timeZone)}</p>
      </div>
      <span className="shrink-0 text-sm text-muted-foreground">{formatDuration(meeting)}</span>
    </li>
  );
}
