using System.Text.Json;
using eSale.Api.Common;
using eSale.Application.Common.Exceptions;
using FluentValidation;

namespace eSale.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception for request {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, exception);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = exception switch
        {
            ValidationException validationException => new ApiExceptionResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "One or more validation failures occurred.",
                Errors = validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray()),
                TraceId = context.TraceIdentifier
            },
            NotFoundException notFoundException => new ApiExceptionResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = notFoundException.Message,
                TraceId = context.TraceIdentifier
            },
            BadHttpRequestException badHttpRequestException => new ApiExceptionResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = badHttpRequestException.Message,
                TraceId = context.TraceIdentifier
            },
            _ => new ApiExceptionResponse
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = "An unexpected error occurred.",
                TraceId = context.TraceIdentifier
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
