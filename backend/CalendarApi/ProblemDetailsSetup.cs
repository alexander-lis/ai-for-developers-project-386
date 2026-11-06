using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace CalendarApi;

/// <summary>Every error response as RFC 9457 Problem Details, as the contract describes them.</summary>
public static class ProblemDetailsSetup
{
    /// <summary>The key of <c>errors</c> for a body that cannot be read at all.</summary>
    public const string BodyKey = "body";

    public static IServiceCollection AddCalendarProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Validation names fields as C# properties; the contract names them as JSON does.
            if (context.ProblemDetails is HttpValidationProblemDetails validation)
            {
                validation.Errors = validation.Errors.ToDictionary(
                    error => JsonNamingPolicy.CamelCase.ConvertName(error.Key), error => error.Value);
            }
        });

        // An unreadable body throws in every environment, so it reaches the handler below
        // instead of an empty 400 outside Development.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddExceptionHandler<UnreadableBodyHandler>();
        return services;
    }

    public static WebApplication UseCalendarProblemDetails(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    /// <summary>Bad JSON is a 400 with <c>errors</c>, like any other invalid body.</summary>
    private sealed class UnreadableBodyHandler(IProblemDetailsService problemDetails) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest })
            {
                return false;
            }

            var path = (exception.InnerException as JsonException)?.Path;
            var field = path is not null && path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : BodyKey;
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new HttpValidationProblemDetails(
                    new Dictionary<string, string[]> { [field] = ["The value cannot be read as JSON."] })
                {
                    Status = StatusCodes.Status400BadRequest,
                },
            });
        }
    }
}
