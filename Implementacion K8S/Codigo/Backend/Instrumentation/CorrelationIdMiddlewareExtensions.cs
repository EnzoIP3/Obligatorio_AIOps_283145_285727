using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Instrumentation;

public static class CorrelationIdMiddlewareExtensions
{
    public const string HttpContextItemKey = "YarpCorrelationId";

    /// <summary>
    /// Ensures X-Correlation-ID exists, exposes it on the response, and stores it for YARP outbound transforms.
    /// </summary>
    public static IApplicationBuilder UseGatewayCorrelationId(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var header = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
            var correlationId = string.IsNullOrWhiteSpace(header)
                ? Guid.NewGuid().ToString("D")
                : header.Trim();
            context.Items[HttpContextItemKey] = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var hostEnvironment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var log = loggerFactory.CreateLogger("Request");
            using (log.BeginScope(CreateRequestScope(correlationId, hostEnvironment)))
                await next();
        });
    }

    /// <summary>
    /// Adds correlation_id to log scopes (from X-Correlation-ID when the gateway forwarded it, else TraceIdentifier).
    /// </summary>
    public static IApplicationBuilder UseServiceCorrelationScope(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var header = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
            var correlationId = string.IsNullOrWhiteSpace(header)
                ? context.TraceIdentifier
                : header.Trim();
            context.Items[HttpContextItemKey] = correlationId;

            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var hostEnvironment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var log = loggerFactory.CreateLogger("Request");
            using (log.BeginScope(CreateRequestScope(correlationId, hostEnvironment)))
                await next();
        });
    }

    /// <summary>
    /// Emits one searchable JSON event per request without logging query strings, headers or request bodies.
    /// </summary>
    public static IApplicationBuilder UseStructuredRequestLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                await next();
            }
            finally
            {
                var elapsedMilliseconds = (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;
                var log = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");
                log.LogInformation(
                    "HTTP request completed {http_method} {path} with status {status_code} in {duration_ms} ms",
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    context.Response.StatusCode,
                    Math.Round(elapsedMilliseconds, 2));
            }
        });
    }

    private static Dictionary<string, object> CreateRequestScope(
        string correlationId,
        IHostEnvironment hostEnvironment)
    {
        var scope = new Dictionary<string, object>
        {
            ["correlation_id"] = correlationId,
            ["service"] = hostEnvironment.ApplicationName,
            ["environment"] = hostEnvironment.EnvironmentName
        };

        if (Activity.Current is { } activity)
        {
            scope["trace_id"] = activity.TraceId.ToString();
            scope["span_id"] = activity.SpanId.ToString();
        }

        return scope;
    }
}
