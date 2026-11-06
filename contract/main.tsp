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

  /** Messages by field name */
  errors: Record<string[]>;
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
}
