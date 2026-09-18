using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace SplitIt.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        if (exception is DbUpdateConcurrencyException)
            _logger.LogWarning(exception, "Concurrency conflict TraceId:{TraceId} Path:{Path}", traceId, httpContext.Request.Path);
        else
            _logger.LogError(exception, "Unhandled exception TraceId:{TraceId} Path:{Path}", traceId, httpContext.Request.Path);

        // Business-rule exceptions carry user-safe messages: always surface them.
        // Unexpected exceptions are only detailed in development.
        var isBusinessRule =
            exception is ArgumentException ||
            exception is KeyNotFoundException ||
            exception is UnauthorizedAccessException;

        var status = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
            DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var title = exception switch
        {
            ArgumentException => "Invalid request.",
            KeyNotFoundException => "Not found.",
            UnauthorizedAccessException => "Not allowed.",
            DbUpdateConcurrencyException => "Conflicting update.",
            _ => "An unexpected error occurred."
        };

        var detail = exception switch
        {
            DbUpdateConcurrencyException => "This record was modified by someone else. Reload and try again.",
            _ when isBusinessRule => exception.Message,
            _ => _env.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred. Please try again later."
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions = { ["traceId"] = traceId, ["message"] = detail }
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
