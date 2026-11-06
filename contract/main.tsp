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

@route("/meeting-types")
@tag("Meeting types")
interface MeetingTypes {
  /** List every meeting type, sorted by name */
  @get list(): MeetingType[];
}
