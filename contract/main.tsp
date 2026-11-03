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
