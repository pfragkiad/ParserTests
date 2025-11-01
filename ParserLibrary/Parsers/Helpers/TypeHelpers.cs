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

}
