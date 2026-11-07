import "@typespec/http";

using Http;

@service(#{ title: "Calendar API" })
@route("/api")
namespace CalendarApi;

model HealthStatus {
  status: "ok";
}

@route("/health")
@tag("Health")
interface Health {
  /** Check that the service is up */
  @get check(): HealthStatus;
}

/** RFC 9457 Problem Details, the body of every error response */
model Problem {
  @header contentType: "application/problem+json";
  type?: string;

  /** Technical text in English; the frontend writes its own messages by status */
  title?: string;

  detail?: string;
  instance?: string;
}

/** The request body breaks the field rules */
@error
model ValidationProblem extends Problem {
  @statusCode statusCode: 400;
  status: 400;

  /**
   * Messages by field name. JSON that cannot be read is reported under the field where reading
   * stopped, or under `body`.
   */
  errors: Record<string[]>;
}

/** The resource in the path does not exist, or its id is not a GUID */
@error
model NotFoundProblem extends Problem {
  @statusCode statusCode: 404;
  status: 404;
}

/**
 * The slot is no longer free: another meeting crosses it, or it has already started.
 * The reason is only in `detail`.
 */
@error
model ConflictProblem extends Problem {
  @statusCode statusCode: 409;
  status: 409;
}

/** Settings of the single calendar that the frontend needs */
model CalendarSettings {
  /** IANA name of the calendar time zone; every time in the interface is shown in it */
  timeZone: string;
}

@route("/calendar")
@tag("Calendar")
interface Calendar {
  /** Get the calendar settings */
  @get get(): CalendarSettings;
}

/** A template the Owner offers to Guests: what the call is about and how long it lasts */
model MeetingType {
  /** Assigned by the server */
  @format("uuid")
  id: string;

  @minLength(1)
  @maxLength(100)
  name: string;

  @maxLength(500)
  description?: string;

  /** Whole minutes */
  @minValue(5)
  @maxValue(480)
  durationMinutes: int32;
}

/** What the Owner sends to create a meeting type or to replace all of its fields */
model MeetingTypeInput {
  /**
   * Stored without leading and trailing whitespace. The server is stricter than the schema:
   * a name of only whitespace is rejected with 400.
   */
  @minLength(1)
  @maxLength(100)
  name: string;

  /** Stored without leading and trailing whitespace; a blank description is stored as absent */
  @maxLength(500)
  description?: string;

  /** Whole minutes */
  @minValue(5)
  @maxValue(480)
  durationMinutes: int32;
}

/** A span of time a Guest can book with a meeting type; computed, so it has no id */
model Slot {
  start: utcDateTime;

  /** Start plus the meeting type duration */
  end: utcDateTime;
}

/** One day of the booking window with its free slots */
model BookingDay {
  /** The day in the calendar time zone */
  date: plainDate;

  /** Sorted by start; empty on days off and on days with no free slot */
  slots: Slot[];
}

@route("/meeting-types")
@tag("Meeting types")
interface MeetingTypes {
  /** List every meeting type, sorted by name */
  @get list(): MeetingType[];

  /** Create a meeting type */
  @post create(@body input: MeetingTypeInput):
    | {
        @statusCode statusCode: 201;
        @body meetingType: MeetingType;
      }
    | ValidationProblem;

  /** Replace all fields of a meeting type */
  @put update(@path @format("uuid") id: string, @body input: MeetingTypeInput):
    | MeetingType
    | ValidationProblem
    | NotFoundProblem;

  /**
   * Free slots of a meeting type for each day of the booking window: exactly 14 days in order,
   * starting today in the calendar time zone
   */
  @get
  @route("{id}/booking-days")
  bookingDays(@path @format("uuid") id: string): BookingDay[] | NotFoundProblem;

  /** Delete a meeting type; meetings already booked keep their snapshot of it */
  @delete delete(@path @format("uuid") id: string):
    | NoContentResponse
    | NotFoundProblem;
}

/**
 * A Guest booked on a slot with a meeting type. Keeps the meeting type name and its own time as
 * they were at booking: later edits or deletion of the meeting type do not change it.
 */
model Meeting {
  /** Assigned by the server */
  @format("uuid")
  id: string;

  /** Only a reference: the meeting type may have been deleted since */
  @format("uuid")
  meetingTypeId: string;

  /** The meeting type name at booking */
  @minLength(1)
  @maxLength(100)
  meetingTypeName: string;

  @minLength(1)
  @maxLength(100)
  guestName: string;

  start: utcDateTime;

  /** Start plus the meeting type duration at booking */
  end: utcDateTime;
}

/** What a Guest sends to book a slot */
model MeetingInput {
  @format("uuid")
  meetingTypeId: string;

  /**
   * Stored without leading and trailing whitespace. The server is stricter than the schema:
   * a name of only whitespace is rejected with 400.
   */
  @minLength(1)
  @maxLength(100)
  guestName: string;

  /**
   * The start of a slot the server offers for the meeting type, with any offset. A start off the
   * grid, outside working hours, on a day off or outside the booking window is a 400 under
   * `start`; a start that is not later than now is a 409.
   */
  start: utcDateTime;
}

@route("/meetings")
@tag("Meetings")
interface Meetings {
  /**
   * List upcoming meetings of every Guest and meeting type: those that end later than now,
   * sorted by start
   */
  @get list(): Meeting[];

  /** Book a slot; of simultaneous requests for crossing slots exactly one succeeds */
  @post create(@body input: MeetingInput):
    | {
        @statusCode statusCode: 201;
        @body meeting: Meeting;
      }
    | ValidationProblem
    | NotFoundProblem
    | ConflictProblem;
}
