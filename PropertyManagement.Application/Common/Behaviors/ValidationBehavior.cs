using FluentValidation;
using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : notnull
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(validator =>
                validator.ValidateAsync(context, cancellationToken)));

        var errors = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => new Error(
                Code: $"Validation.{failure.ErrorCode}",
                Message: failure.ErrorMessage,
                PropertyName: failure.PropertyName))
            .ToArray();

        if (errors.Length > 0)
        {
            return Result<TResponse>.Failure(errors);
        }

        return await next(cancellationToken);
    }
}
