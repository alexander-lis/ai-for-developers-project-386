import { useEffect, useRef, useState } from "react";
import { ru } from "react-day-picker/locale";
import {
  calendarGet,
  meetingTypesBookingDays,
  meetingTypesList,
  type BookingDay,
  type MeetingType,
  type Slot,
} from "@/client";
import { formatTimeRange, fromPlainDate, toPlainDate } from "@/calendar-time";
import { LoadError } from "@/components/load-error";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { meetingTypeUnavailable, noFreeSlots, noMeetingTypes } from "@/messages";

type PageState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; meetingTypes: MeetingType[]; timeZone: string };

/** Booking days of the selected meeting type; `date` is the selected day, if any has slots. */
type DaysState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "loaded"; days: BookingDay[]; date?: string };

async function fetchPage(): Promise<PageState> {
  // Network and server failures come back as `error` with no `data`.
  const [types, calendar] = await Promise.all([meetingTypesList(), calendarGet()]);
  return types.data && calendar.data
    ? { status: "loaded", meetingTypes: types.data, timeZone: calendar.data.timeZone }
    : { status: "error" };
}

async function fetchDays(meetingTypeId: string): Promise<DaysState | "gone"> {
  const { data, response } = await meetingTypesBookingDays({ path: { id: meetingTypeId } });
  if (response?.status === 404) {
    return "gone";
  }
  // The first day with slots is selected by itself.
  return data
    ? { status: "loaded", days: data, date: data.find((day) => day.slots.length > 0)?.date }
    : { status: "error" };
}

/** Guest screen: meeting types, the days of the booking window and the free slots of a day. */
export function BookPage() {
  const [page, setPage] = useState<PageState>({ status: "loading" });
  const [meetingTypeId, setMeetingTypeId] = useState<string>();
  const [days, setDays] = useState<DaysState>({ status: "loading" });
  const [slotStart, setSlotStart] = useState<string>();
  const [gone, setGone] = useState(false);
  // Only the answer to the latest request may land: types can be switched faster than days load.
  const daysRequest = useRef(0);

  // Fetched on every visit: the page mounts anew on each navigation.
  useEffect(() => {
    let current = true;
    void fetchPage().then((next) => current && setPage(next));
    return () => {
      current = false;
    };
  }, []);

  function reloadPage() {
    setPage({ status: "loading" });
    void fetchPage().then(setPage);
  }

  function loadDays(id: string) {
    const request = ++daysRequest.current;
    setDays({ status: "loading" });
    setSlotStart(undefined);
    void fetchDays(id).then((next) => {
      if (request !== daysRequest.current) {
        return;
      }
      if (next === "gone") {
        setMeetingTypeId(undefined);
        setGone(true);
        reloadPage();
        return;
      }
      setDays(next);
    });
  }

  function selectMeetingType(id: string) {
    setMeetingTypeId(id);
    setGone(false);
    loadDays(id);
  }

  function selectDate(date: string) {
    setDays((state) => (state.status === "loaded" ? { ...state, date } : state));
    setSlotStart(undefined);
  }

  if (page.status !== "loaded") {
    return (
      <Screen>
        {page.status === "loading" && <p className="text-muted-foreground">Загрузка…</p>}
        {page.status === "error" && <LoadError onRetry={reloadPage} />}
      </Screen>
    );
  }

  return (
    <Screen>
      <div className="grid gap-8 lg:grid-cols-[1fr_auto_1fr]">
        <Block title="Тип встречи">
          {gone && <p role="alert">{meetingTypeUnavailable}</p>}
          <MeetingTypeChoice
            meetingTypes={page.meetingTypes}
            selectedId={meetingTypeId}
            onSelect={selectMeetingType}
          />
        </Block>
        {meetingTypeId && (
          <>
            <Block title="День">
              {days.status === "loading" && <p className="text-muted-foreground">Загрузка…</p>}
              {days.status === "error" && <LoadError onRetry={() => loadDays(meetingTypeId)} />}
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
              {days.status === "loaded" && (
                <SlotChoice
                  slots={days.days.find((day) => day.date === days.date)?.slots ?? []}
                  timeZone={page.timeZone}
                  selectedStart={slotStart}
                  onSelect={setSlotStart}
                />
              )}
            </Block>
          </>
        )}
      </div>
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
            <span className="text-sm text-muted-foreground">{meetingType.durationMinutes} мин</span>
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
      {withSlots.size === 0 && <p className="text-muted-foreground">{noFreeSlots}</p>}
    </div>
  );
}

function SlotChoice({
  slots,
  timeZone,
  selectedStart,
  onSelect,
}: {
  slots: Slot[];
  timeZone: string;
  selectedStart?: string;
  onSelect: (start: string) => void;
}) {
  return (
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
  );
}
