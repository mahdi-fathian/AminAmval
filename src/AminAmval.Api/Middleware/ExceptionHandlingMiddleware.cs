using System.Net;
using System.Text.Json;
using AminAmval.Application.Exceptions;
using AminAmval.Domain.Exceptions;

namespace AminAmval.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var (status, code, message, details) = exception switch
        {
            ValidationException ve => (HttpStatusCode.BadRequest, "validation_error", ve.Message, (object?)ve.Errors),
            NotFoundException nf => (HttpStatusCode.NotFound, "not_found", nf.Message, null),
            ConflictException cf => (HttpStatusCode.Conflict, "conflict", cf.Message, null),
            DomainException de => (HttpStatusCode.UnprocessableEntity, "domain_error", de.Message, null),
            ApplicationException ae => (HttpStatusCode.BadRequest, "application_error", ae.Message, null),
            _ => (HttpStatusCode.InternalServerError, "internal_error", "خطای غیرمنتظره رخ داد.", null)
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        else
            _logger.LogWarning(exception, "Handled exception {Code}. TraceId={TraceId}", code, traceId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var body = new
        {
            code,
            message,
            details,
            traceId,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
