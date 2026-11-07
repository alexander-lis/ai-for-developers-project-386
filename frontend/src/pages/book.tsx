import { useRef, useState } from "react";
import { ru } from "react-day-picker/locale";
import { toast } from "sonner";
import {
  calendarGet,
  meetingTypesBookingDays,
  meetingTypesList,
  type BookingDay,
  type MeetingType,
  type Slot,
} from "@/client";
import { formatDuration, formatTimeRange, fromPlainDate, toPlainDate } from "@/calendar-time";
import {
  BookingDialog,
  type BookingOutcome,
  type BookingTarget,
} from "@/components/booking-dialog";
import { LoadStatus } from "@/components/load-status";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { bookingConflict, meetingTypeUnavailable, noFreeSlots, noMeetingTypes } from "@/messages";
import { useLoad } from "@/use-load";

/** Booking days of the selected meeting type; `date` is the selected day, if any has slots. */
type DaysState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; days: BookingDay[]; date?: string };

async function fetchPage() {
  // Network and server failures come back as `error` with no `data`.
  const [types, calendar] = await Promise.all([meetingTypesList(), calendarGet()]);
  return types.data && calendar.data
    ? { meetingTypes: types.data, timeZone: calendar.data.timeZone }
    : undefined;
}

/** Days of a meeting type; `keepDate` stays selected while it still has slots. */
async function fetchDays(
  meetingTypeId: string,
  keepDate?: string,
): Promise<DaysState | "meetingTypeDeleted"> {
  const { data, response } = await meetingTypesBookingDays({ path: { id: meetingTypeId } });
  if (response?.status === 404) {
    return "meetingTypeDeleted";
  }
  if (!data) {
    return { status: "error" };
  }
  const withSlots = data.filter((day) => day.slots.length > 0);
  // Otherwise the first day with slots is selected by itself.
  const date = withSlots.some((day) => day.date === keepDate) ? keepDate : withSlots[0]?.date;
  return { status: "loaded", days: data, date };
}

/** The booking dialog keeps its target while closing, so the closing animation shows the same text. */
type BookingState = { open: boolean; target?: BookingTarget };

/** Guest screen: meeting types, the days of the booking window and the free slots of a day. */
export function BookPage() {
  const { state: page, reload: reloadPage } = useLoad(fetchPage);
  const [meetingTypeId, setMeetingTypeId] = useState<string>();
  const [days, setDays] = useState<DaysState>({ status: "loading" });
  const [slotStart, setSlotStart] = useState<string>();
  const [typeDeleted, setTypeDeleted] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [booking, setBooking] = useState<BookingState>({ open: false });
  // Only the answer to the latest request may land: types can be switched faster than days load.
  const daysRequest = useRef(0);

  /**
   * Days of a meeting type. With `keepDate` (after a booking) the days on screen stay until the
   * fresh ones arrive, and the day stays selected while it has slots.
   */
  function loadDays(id: string, keepDate?: string) {
    const request = ++daysRequest.current;
    if (!keepDate) {
      setDays({ status: "loading" });
    }
    setSlotStart(undefined);
    void fetchDays(id, keepDate).then((next) => {
      if (request !== daysRequest.current) {
        return;
      }
      if (next === "meetingTypeDeleted") {
        startOverWithoutDeletedType();
        return;
      }
      setDays(next);
    });
  }

  /** The selected meeting type was deleted: start over from a fresh list of types. */
  function startOverWithoutDeletedType() {
    setMeetingTypeId(undefined);
    setSlotStart(undefined);
    setConflict(false);
    setTypeDeleted(true);
    reloadPage();
  }

  function selectMeetingType(id: string) {
    setMeetingTypeId(id);
    setTypeDeleted(false);
    setConflict(false);
    loadDays(id);
  }

  function selectSlot(start: string) {
    setSlotStart(start);
    setConflict(false);
  }

  function finishBooking(outcome: BookingOutcome) {
    setBooking((state) => ({ ...state, open: false }));
    if (outcome.status === "meetingTypeDeleted") {
      startOverWithoutDeletedType();
      return;
    }
    if (outcome.status === "booked" && page.status === "loaded") {
      const { meeting } = outcome;
      toast(
        `Вы записаны: ${meeting.meetingTypeName}, ${formatTimeRange(meeting, page.data.timeZone)}`,
      );
    }
    setConflict(outcome.status === "conflict");
    if (meetingTypeId) {
      loadDays(meetingTypeId, days.status === "loaded" ? days.date : undefined);
    }
  }

  function selectDate(date: string) {
    setDays((state) => (state.status === "loaded" ? { ...state, date } : state));
    setSlotStart(undefined);
  }

  if (page.status !== "loaded") {
    return (
      <Screen>
        <LoadStatus status={page.status} onRetry={reloadPage} />
      </Screen>
    );
  }
  const { meetingTypes, timeZone } = page.data;

  return (
    <Screen>
      <div className="grid gap-8 lg:grid-cols-[1fr_auto_1fr]">
        <Block title="Тип встречи">
          {typeDeleted && <p role="alert">{meetingTypeUnavailable}</p>}
          <MeetingTypeChoice
            meetingTypes={meetingTypes}
            selectedId={meetingTypeId}
            onSelect={selectMeetingType}
          />
        </Block>
        {meetingTypeId && (
          <>
            <Block title="День">
              <LoadStatus status={days.status} onRetry={() => loadDays(meetingTypeId)} />
              {days.status === "loaded" && (
                <BookingCalendar
                  key={meetingTypeId}
                  days={days.days}
                  date={days.date}
                  onSelect={selectDate}
                />
              )}
            </Block>
            <Block title="Время">
              {conflict && <p role="alert">{bookingConflict}</p>}
              {days.status === "loaded" && (
                <SlotChoice
                  slots={days.days.find((day) => day.date === days.date)?.slots ?? []}
                  timeZone={timeZone}
                  selectedStart={slotStart}
                  onSelect={selectSlot}
                  onBook={(slot) =>
                    setBooking({
                      open: true,
                      target: {
                        meetingTypeId,
                        meetingTypeName:
                          meetingTypes.find((type) => type.id === meetingTypeId)?.name ?? "",
                        slot,
                      },
                    })
                  }
                />
              )}
            </Block>
          </>
        )}
      </div>
      <BookingDialog
        {...booking}
        timeZone={timeZone}
        onOpenChange={(open) => setBooking({ ...booking, open })}
        onDone={finishBooking}
      />
    </Screen>
  );
}

function Screen({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-8 py-20">
      <h1 className="text-3xl font-semibold tracking-tight">Запись</h1>
      {children}
    </div>
  );
}

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section aria-label={title} className="flex flex-col gap-3">
      <h2 className="text-sm font-medium text-muted-foreground">{title}</h2>
      {children}
    </section>
  );
}

function MeetingTypeChoice({
  meetingTypes,
  selectedId,
  onSelect,
}: {
  meetingTypes: MeetingType[];
  selectedId?: string;
  onSelect: (id: string) => void;
}) {
  if (meetingTypes.length === 0) {
    return <p className="text-muted-foreground">{noMeetingTypes}</p>;
  }

  return (
    <ul className="flex flex-col gap-2">
      {meetingTypes.map((meetingType) => (
        <li key={meetingType.id}>
          <button
            type="button"
            aria-pressed={meetingType.id === selectedId}
            onClick={() => onSelect(meetingType.id)}
            className="flex w-full flex-col items-start gap-1 rounded-xl border p-4 text-left transition-colors outline-none hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 aria-pressed:border-primary aria-pressed:bg-muted"
          >
            <span className="font-medium">{meetingType.name}</span>
            {meetingType.description && (
              <span className="text-sm whitespace-pre-line text-muted-foreground">
                {meetingType.description}
              </span>
            )}
            <span className="text-sm text-muted-foreground">{formatDuration(meetingType)}</span>
          </button>
        </li>
      ))}
    </ul>
  );
}

/**
 * Exactly the days the server returned: days outside them are muted, days without slots are
 * struck through, both disabled. Months outside the booking window are out of reach.
 */
function BookingCalendar({
  days,
  date,
  onSelect,
}: {
  days: BookingDay[];
  date?: string;
  onSelect: (date: string) => void;
}) {
  const inWindow = new Set(days.map((day) => day.date));
  const withSlots = new Set(days.filter((day) => day.slots.length > 0).map((day) => day.date));
  const first = fromPlainDate(days[0].date);

  return (
    <div className="flex flex-col gap-3">
      <Calendar
        mode="single"
        locale={ru}
        className="rounded-xl border"
        selected={date ? fromPlainDate(date) : undefined}
        onSelect={(picked) => picked && onSelect(toPlainDate(picked))}
        // "Today" is the server's: the browser may already be on another day.
        today={first}
        defaultMonth={date ? fromPlainDate(date) : first}
        startMonth={first}
        endMonth={fromPlainDate(days[days.length - 1].date)}
        disabled={(day) => !withSlots.has(toPlainDate(day))}
        modifiers={{
          outsideWindow: (day) => !inWindow.has(toPlainDate(day)),
          noSlots: (day) => inWindow.has(toPlainDate(day)) && !withSlots.has(toPlainDate(day)),
        }}
        modifiersClassNames={{
          outsideWindow: "opacity-40",
          noSlots: "[&_button]:line-through",
        }}
      />
      {withSlots.size === 0 && <p className="text-muted-foreground">{noFreeSlots(days.length)}</p>}
    </div>
  );
}

function SlotChoice({
  slots,
  timeZone,
  selectedStart,
  onSelect,
  onBook,
}: {
  slots: Slot[];
  timeZone: string;
  selectedStart?: string;
  onSelect: (start: string) => void;
  onBook: (slot: Slot) => void;
}) {
  if (slots.length === 0) {
    return null;
  }

  const selected = slots.find((slot) => slot.start === selectedStart);
  return (
    <div className="flex flex-col gap-4">
      <ul className="flex flex-col gap-2">
        {slots.map((slot) => (
          <li key={slot.start}>
            <Button
              variant={slot.start === selectedStart ? "default" : "outline"}
              aria-pressed={slot.start === selectedStart}
              className="w-full"
              onClick={() => onSelect(slot.start)}
            >
              {formatTimeRange(slot, timeZone)}
            </Button>
          </li>
        ))}
      </ul>
      <Button disabled={!selected} onClick={() => selected && onBook(selected)}>
        Записаться
      </Button>
    </div>
  );
}
