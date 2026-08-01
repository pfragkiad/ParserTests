using ParserLibrary.Definitions;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Parsers;

public partial class ParserBase
{
    protected internal readonly record struct TypeInferenceValue(object? RuntimeValue, Type ResolvedType, bool HasRuntimeValue)
    {
        public object? ToResolverArgument() => HasRuntimeValue ? RuntimeValue : ResolvedType;

        public static TypeInferenceValue FromRuntimeValue(object? value)
            => new(value, OperatorDefinition.GetArgumentType(value), HasRuntimeValue: true);

        public static TypeInferenceValue FromDeclaredType(Type type)
            => new(null, type, HasRuntimeValue: false);

        public static TypeInferenceValue FromVariable(object? value)
            => value is Type type ? FromDeclaredType(TypeHelpers.NormalizeNullMarkerType(type)) : FromRuntimeValue(value);

        public static TypeInferenceValue Unknown => new(null, TypeHelpers.NullArgumentType, HasRuntimeValue: false);
    }

    protected static object?[] ToResolverArguments(TypeInferenceValue[] values)
        => [.. values.Select(static v => v.ToResolverArgument())];
}
