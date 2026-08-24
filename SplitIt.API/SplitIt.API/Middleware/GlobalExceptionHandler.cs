using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
        _logger.LogError(exception, "Unhandled exception TraceId:{TraceId} Path:{Path}", traceId, httpContext.Request.Path);

        // Do not leak internal details in production
        var isDev = _env.IsDevelopment();
        var title = exception is UnauthorizedAccessException ? "Unauthorized" : "An unexpected error occurred.";
        var status = exception is UnauthorizedAccessException ? (int)HttpStatusCode.Unauthorized : (int)HttpStatusCode.InternalServerError;
        var detail = isDev ? exception.Message : "An unexpected error occurred. Please try again later.";

        // Map known exceptions to status codes
        if (exception is ArgumentException) status = 400;
        if (exception is KeyNotFoundException) status = 404;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions = { ["traceId"] = traceId }
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
