using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Common.Results
{
    public class Result : IResult<Result>
    {
        private Result(bool isSuccess, IReadOnlyCollection<Error> errors)
        {
            IsSuccess = isSuccess;
            Errors = errors;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        
        public IReadOnlyCollection<Error> Errors { get; }

        public static Result Success() 
        {
            return new Result(isSuccess: true,
            errors: Array.Empty<Error>());
        }

        public static Result Failure(params Error[] errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            if (errors.Length == 0) 
            {
                throw new ArgumentException(
                    "A failure result must contain at least one error.",
                    nameof(errors)
                );
            }

            return new Result(isSuccess: false, errors: errors);
        }
    }

    public sealed class Result<T> : IResult<Result<T>>
    {
        private readonly T? _value;

        private Result(
            bool isSuccess,
            T? value,
            IReadOnlyCollection<Error> errors)
        {
            IsSuccess = isSuccess;
            _value = value;
            Errors = errors;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        public IReadOnlyCollection<Error> Errors { get; }

        public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException(
            "A failure result does not have a value."
            );

        public static Result<T> Success(T value) 
        {
            return new Result<T>(
                isSuccess: true,
                value: value,
                errors: Array.Empty<Error>()
            );
        }

        public static Result<T> Failure(params Error[] errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            if (errors.Length == 0)
            {
                throw new ArgumentException(
                    "A failure result must contain at least one error.",
                    nameof(errors)
                );
            }

            return new Result<T>(
                isSuccess: false,
                value: default,
                errors: errors
            );
        }
    }
}
