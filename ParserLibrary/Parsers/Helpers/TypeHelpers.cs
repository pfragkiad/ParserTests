namespace ParserLibrary.Parsers.Helpers;

public static class TypeHelpers
{
    public static readonly Type NullArgumentType = typeof(NullType);
    public static readonly Type LegacyNullArgumentType = typeof(object);

    // Common type-compatibility helper (exact or superclass/interface when allowed)
    public static bool TypeMatches(Type actual, Type expected, bool allowParentTypes)
    {
        // Accept exact match, or when 'actual' is a superclass/interface of 'expected'
        // (i.e., broader received type is allowed)
        return ReferenceEquals(actual, expected) || allowParentTypes && actual.IsAssignableFrom(expected);
    }

    public static bool IsNullArgumentType(Type t) =>
        ReferenceEquals(t, NullArgumentType) || ReferenceEquals(t, LegacyNullArgumentType);

    public static bool IsNullValue(object? value) => value is null || value is NullType;

    public static bool IsNonNullValue(object? value) => !IsNullValue(value);

    public static object NormalizeNullValue(object? value) => IsNullValue(value) ? NullType.Instance : value!;

    public static Type NormalizeNullMarkerType(Type type) => IsNullArgumentType(type) ? NullArgumentType : type;

    public static Type ResolveRuntimeArgumentType(object? value)
    {
        var normalized = NormalizeNullValue(value);
        return normalized is NullType ? NullArgumentType : normalized.GetType();
    }

    public static bool TryPropagateNullBinary(object? left, object? right, out object? result)
    {
        if (IsNullValue(left) || IsNullValue(right))
        {
            result = NullType.Instance;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Null-aware type matching: treats null-marker types specially.
    /// - canonical marker is NullType
    /// - legacy marker object is also accepted for compatibility
    /// - a null-marker actual matches only null-marker expected and vice versa
    /// </summary>
    public static bool TypeMatchesWithNullAwareness(Type actual, Type expected, bool allowParentTypes)
    {
        // Case 1: actual is null marker (NullType or legacy object)
        if (IsNullArgumentType(actual))
        {
            // Null-marker ONLY matches if expected is also a null-marker
            return IsNullArgumentType(expected);
        }

        // Case 2: expected is null marker (meaning the syntax allows null)
        if (IsNullArgumentType(expected))
        {
            // Non-null types NEVER match null-marker types
            return false;
        }

        // Case 3: both are real types - use standard matching (exact or parent type)
        return TypeMatches(actual, expected, allowParentTypes);
    }

    public static bool MatchesAnyExpectedWithNullAwareness(Type actual, IEnumerable<Type> expectedTypes, bool allowParentTypes)
    {
        var expected = expectedTypes as Type[] ?? [.. expectedTypes];

        if (expected.Any(t => ReferenceEquals(t, typeof(AnyType))))
            return true;

        if (expected.Any(t => ReferenceEquals(t, typeof(AnyNonNullType))))
            return !IsNullArgumentType(actual);

        return expected.Any(t => TypeMatchesWithNullAwareness(actual, t, allowParentTypes));
    }
}
