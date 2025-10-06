using System.Diagnostics;

using FairShare.ViewModels;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FairShare.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController(
    ILogger<ErrorController> logger,
    ProblemDetailsFactory problemFactory) : Controller
{
    private readonly ILogger<ErrorController> _logger = logger;
    private readonly ProblemDetailsFactory _problemFactory = problemFactory;

    // Unhandled exceptions (500)
    [Route("error")]
    public IActionResult Error()
    {
        IExceptionHandlerFeature? feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        Exception? ex = feature?.Error;

        string traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

        AddCommonHeaders(traceId);

        bool isDev = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
        string? detail = isDev ? ex?.ToString() : null;

        if (WantsJson())
        {
            ProblemDetails problem = _problemFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "An unexpected error occurred.",
                detail: detail,
                instance: GetOriginalRequestPath() ?? HttpContext.Request.Path
            );

            problem.Type = "https://httpstatuses.com/500";
            problem.Extensions["traceId"] = traceId;
            return StatusCode(problem.Status!.Value, problem);
        }

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        Response.GetTypedHeaders().CacheControl = new() { NoStore = true };

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            StatusCode = 500,
            Title = "Something went wrong",
            Message = "We hit a snag processing your request.",
            TraceId = traceId,
            Detail = detail
        });
    }

    // Status-code errors (404, 400, 403, …)
    [Route("error/{statusCode:int}")]
    public IActionResult HttpError(int statusCode)
    {
        string traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (statusCode >= 500)
        {
            _logger.LogError("HTTP {StatusCode}. TraceId={TraceId}", statusCode, traceId);
        }
        else
        {
            _logger.LogWarning("HTTP {StatusCode}. TraceId={TraceId}", statusCode, traceId);
        }

        AddCommonHeaders(traceId);

        (string title, string message) mapped = MapStatus(statusCode);

        if (WantsJson())
        {
            ProblemDetails problem = _problemFactory.CreateProblemDetails(
                HttpContext,
                statusCode: statusCode,
                title: mapped.title,
                detail: mapped.message,
                instance: GetOriginalRequestPath() ?? HttpContext.Request.Path
            );

            problem.Type = $"https://httpstatuses.com/{statusCode}";
            problem.Extensions["traceId"] = traceId;
            return StatusCode(statusCode, problem);
        }

        Response.StatusCode = statusCode;
        Response.GetTypedHeaders().CacheControl = new() { NoStore = true };

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = mapped.title,
            Message = mapped.message,
            TraceId = traceId
        });
    }

    private static (string title, string message) MapStatus(int code) => code switch
    {
        400 => ("Bad Request", "The request was invalid or cannot be served."),
        401 => ("Unauthorized", "You need to sign in to access this resource."),
        403 => ("Forbidden", "You do not have permission to access this resource."),
        404 => ("Not Found", "We couldn’t find what you were looking for."),
        408 => ("Request Timeout", "The request took too long to complete."),
        409 => ("Conflict", "The request conflicts with the current state."),
        429 => ("Too Many Requests", "You have sent too many requests. Please slow down."),
        500 => ("Server Error", "An unexpected error occurred on the server."),
        502 => ("Bad Gateway", "Invalid response from an upstream server."),
        503 => ("Service Unavailable", "The service is temporarily unavailable."),
        504 => ("Gateway Timeout", "Upstream server timed out."),
        _ => ("Error", "Something went wrong while processing your request.")
    };

    private bool WantsJson()
    {
        // Query override
        if (Request.Query.TryGetValue("format", out var fmt) && fmt == "json")
        {
            return true;
        }

        IList<MediaTypeHeaderValue> accept = Request.GetTypedHeaders().Accept;

        if (accept is { Count: > 0 }
            && accept.Any(h => h.MediaType.Value?.Contains("json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        if (Request.Headers.TryGetValue("X-Requested-With", out StringValues v) && v == "XMLHttpRequest")
        {
            return true;
        }

        if (Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void AddCommonHeaders(string traceId)
    {
        Response.Headers["X-Trace-Id"] = traceId;
    }

    private string? GetOriginalRequestPath()
    {
        // Available when using ReExecute (status code pages)
        IStatusCodeReExecuteFeature? reExecute = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        return reExecute?.OriginalPath;
    }
}
