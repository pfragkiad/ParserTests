using FluentValidation.Results;

namespace ParserLibrary;

public static class Helpers
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
}
