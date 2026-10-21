using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(
        this ControllerBase controller,
        Result result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok();
        }

        var statusCode = result.Errors
            .Select(error => error.Type)
            .Distinct()
            .OrderBy(GetErrorPriority)
            .Select(GetStatusCode)
            .First();

        var primaryError = result.Errors.First();
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = GetTitle(statusCode),
            Status = statusCode,
            Detail = primaryError.Message,
            Instance = controller.Request.Path
        };

        problemDetails.Extensions["errors"] = result.Errors;

        return controller.StatusCode(statusCode, problemDetails);
    }

    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        var statusCode = result.Errors
            .Select(error => error.Type)
            .Distinct()
            .OrderBy(GetErrorPriority)
            .Select(GetStatusCode)
            .First();

        var primaryError = result.Errors.First();
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = GetTitle(statusCode),
            Status = statusCode,
            Detail = primaryError.Message,
            Instance = controller.Request.Path
        };

        problemDetails.Extensions["errors"] = result.Errors;

        return controller.StatusCode(statusCode, problemDetails);
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        _ => "An error occurred"
    };

    private static int GetErrorPriority(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Unauthorized => 0,
            ErrorType.Forbidden => 1,
            ErrorType.NotFound => 2,
            ErrorType.Conflict => 3,
            ErrorType.TooManyRequests => 4,
            ErrorType.Validation => 5,
            ErrorType.Failure => 6,
            _ => 7
        };
    }

    private static int GetStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.Failure => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
    }
}