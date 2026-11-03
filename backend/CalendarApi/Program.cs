var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var api = app.MapGroup("/api");

api.MapGet("/health", () => new HealthStatus("ok"));

app.Run();

record HealthStatus(string Status);
