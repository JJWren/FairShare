using System.Diagnostics;
using FairShare.ViewModels;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FairShare.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController(ILogger<ErrorController> logger, ProblemDetailsFactory problemFactory) : Controller
{
    private readonly ILogger<ErrorController> _logger = logger;
    private readonly ProblemDetailsFactory _problemFactory = problemFactory;

    // Unhandled exceptions (500)
    [Route("error")]
    public IActionResult Error()
    {
        IExceptionHandlerFeature? feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        Exception? ex = feature?.Error;

        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

        if (WantsJson())
        {
            ProblemDetails? problem = _problemFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "An unexpected error occurred.",
                detail: HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true
                    ? ex?.ToString()
                    : null,
                instance: HttpContext.Request?.Path.Value
            );

            problem.Extensions["traceId"] = traceId;
            return StatusCode(problem.Status!.Value, problem);
        }

        Response.StatusCode = StatusCodes.Status500InternalServerError;

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            StatusCode = 500,
            Title = "Something went wrong",
            Message = "We hit a snag processing your request.",
            TraceId = traceId,
            Detail = ex?.ToString()
        });
    }

    // Status-code errors (404, 400, 403, …)
    [Route("error/{statusCode:int}")]
    public IActionResult HttpError(int statusCode)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (statusCode >= 500)
        {
            _logger.LogError("HTTP {StatusCode}. TraceId={TraceId}", statusCode, traceId);
        }
        else
        {
            _logger.LogWarning("HTTP {StatusCode}. TraceId={TraceId}", statusCode, traceId);
        }

        if (WantsJson())
        {
            (var title, var message) = MapStatus(statusCode);
            ProblemDetails? problem = _problemFactory.CreateProblemDetails(
                HttpContext,
                statusCode: statusCode,
                title: title,
                detail: message,
                instance: HttpContext.Request?.Path.Value
            );
            problem.Extensions["traceId"] = traceId;
            return StatusCode(statusCode, problem);
        }

        Response.StatusCode = statusCode;
        (string title, string message) mapped = MapStatus(statusCode);

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = mapped.title,
            Message = mapped.message,
            TraceId = traceId,
            Detail = null
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
        429 => ("Too Many Requests", "You made too many requests. Slow down, speed racer."),
        500 => ("Server Error", "An unexpected error occurred on the server."),
        502 => ("Bad Gateway", "Invalid response from upstream server."),
        503 => ("Service Unavailable", "The service is temporarily unavailable."),
        504 => ("Gateway Timeout", "Upstream server timed out."),
        _ => ("Error", "Something went wrong while processing your request.")
    };

    private bool WantsJson()
    {
        // If the client explicitly asks for JSON or it's an AJAX/fetch call, return ProblemDetails JSON
        IList<MediaTypeHeaderValue> accept = Request.GetTypedHeaders().Accept;
        if (accept is { Count: > 0 } && accept.Any(h => h.MediaType.Value != null && h.MediaType.Value.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Heuristic for AJAX
        if (Request.Headers.TryGetValue("X-Requested-With", out StringValues v) && v.ToString() == "XMLHttpRequest")
        {
            return true;
        }

        // If it's an API route (optional: adjust as needed)
        if (Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
