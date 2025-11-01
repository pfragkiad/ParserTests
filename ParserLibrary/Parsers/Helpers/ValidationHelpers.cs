using FluentValidation.Results;

namespace ParserLibrary.Parsers.Helpers;

public static class ValidationHelpers
{
    public static string ToOrdinal(int n)
    {
        int rem100 = n % 100;
        if (rem100 is >= 11 and <= 13) return $"{n}th";
        return (n % 10) switch
        {
            1 => $"{n}st",
            2 => $"{n}nd",
            3 => $"{n}rd",
            _ => $"{n}th"
        };
    }

    public static ValidationFailure GetFailure(string propertyName, string? errorMessage, object? attemptedValue)
    {
        if (errorMessage is not null && attemptedValue is not null)
            return new ValidationFailure(propertyName, errorMessage, attemptedValue);

        if (string.IsNullOrWhiteSpace(errorMessage))
            errorMessage = "Validation failed.";

        return new ValidationFailure(propertyName, errorMessage);
    }

    public static ValidationResult GetFailureResult(string propertyName, string? errorMessage, object? attemptedValue)
    {
        ValidationFailure f = GetFailure(propertyName, errorMessage, attemptedValue);
        return new ValidationResult([f]);
    }

    public static ValidationResult UnknownFunctionResult(string functionName) =>
        new([new ValidationFailure("function", $"Function '{functionName}' is not supported.")]);

    public static ValidationResult UnknownOperatorResult(string operatorName) =>
        new([new ValidationFailure("operator", $"Operator '{operatorName}' is not supported.")]);


    public static bool IsValidationFailureFunctionNotSupported(ValidationResult result)
    {
        return result.Errors.Count == 1 &&
               result.Errors[0].PropertyName == "function" &&
               result.Errors[0].ErrorMessage.Contains("is not supported.") &&
               result.Errors[0].ErrorMessage.StartsWith("Function ");
    }

    public readonly static ValidationResult Success = new();


}
