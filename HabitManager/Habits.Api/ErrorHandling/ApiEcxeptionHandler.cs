using Habits.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Habits.Api.ErrorHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            HabitNotFoundException => (StatusCodes.Status404NotFound, "Habit not found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid data"),           // tukš nosaukums
            InvalidOperationException => (StatusCodes.Status409Conflict, "Action not allowed"), // arhivēts/duplicēts nosaukums
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Domain kļūdas nerāda klijentam - sensetive info servera daļā.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Something went wrong on the server."
                    : exception.Message
            }
        });
    }
}
