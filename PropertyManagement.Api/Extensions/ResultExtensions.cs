using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Api.Extensions;

public static class ResultExtensions
{
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


        return new ObjectResult(new
        {
            errors = result.Errors
        })
        {
            StatusCode = statusCode
        };
    }

    private static int GetErrorPriority(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Unauthorized => 0,
            ErrorType.Forbidden => 1,
            ErrorType.NotFound => 2,
            ErrorType.Conflict => 3,
            ErrorType.Validation => 4,
            ErrorType.Failure => 5,
            _ => 6
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
            ErrorType.Failure => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
