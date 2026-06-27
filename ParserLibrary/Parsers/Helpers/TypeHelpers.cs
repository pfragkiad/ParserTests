namespace ParserLibrary.Parsers.Helpers;

public static class TypeHelpers
{
    // Common type-compatibility helper (exact or superclass/interface when allowed)
    public static bool TypeMatches(Type actual, Type expected, bool allowParentTypes)
    {
        // Accept exact match, or when 'actual' is a superclass/interface of 'expected'
        // (i.e., broader received type is allowed)
        return ReferenceEquals(actual, expected) || allowParentTypes && actual.IsAssignableFrom(expected);
    }

    /// <summary>
    /// Null-aware type matching: treats object type (representing null) specially.
    /// - If actual is object (null was passed), it ONLY matches if expected is exactly object.
    /// - If actual is a real type, it NEVER matches if expected is object (object is for null only).
    /// This ensures explicit null handling: object in allowed types = null is allowed here.
    /// </summary>
    public static bool TypeMatchesWithNullAwareness(Type actual, Type expected, bool allowParentTypes)
    {
        // Case 1: actual is null (object)
        if (ReferenceEquals(actual, typeof(object)))
        {
            // Null ONLY matches if expected is exactly object
            return ReferenceEquals(expected, typeof(object));
        }

        // Case 2: expected is object (meaning the syntax only allows null)
        if (ReferenceEquals(expected, typeof(object)))
        {
            // Non-null types NEVER match object (object is for null only)
            return false;
        }

        // Case 3: both are real types - use standard matching (exact or parent type)
        return ReferenceEquals(actual, expected) || 
               allowParentTypes && actual.IsAssignableFrom(expected);
    }

}
