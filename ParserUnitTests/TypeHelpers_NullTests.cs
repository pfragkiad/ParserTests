using ParserLibrary.Parsers.Helpers;
using Xunit;

namespace ParserUnitTests;

public class TypeHelpers_NullTests
{
    [Fact]
    public void NormalizeNullValue_Returns_NullTypeSingleton_ForNull()
    {
        var normalized = TypeHelpers.NormalizeNullValue(null);

        Assert.Same(NullType.Instance, normalized);
    }

    [Fact]
    public void IsNullValue_Recognizes_Null_And_NullType()
    {
        Assert.True(TypeHelpers.IsNullValue(null));
        Assert.True(TypeHelpers.IsNullValue(NullType.Instance));
        Assert.False(TypeHelpers.IsNullValue(0));
        Assert.False(TypeHelpers.IsNullValue("x"));
    }

    [Fact]
    public void ResolveRuntimeArgumentType_Returns_CanonicalNullArgumentType_ForNullMarkers()
    {
        var fromNull = TypeHelpers.ResolveRuntimeArgumentType(null);
        var fromNullType = TypeHelpers.ResolveRuntimeArgumentType(NullType.Instance);

        Assert.Equal(TypeHelpers.NullArgumentType, fromNull);
        Assert.Equal(TypeHelpers.NullArgumentType, fromNullType);
    }

    [Fact]
    public void TypeMatchesWithNullAwareness_AcceptsCanonicalAndLegacyNullMarkers()
    {
        Assert.True(TypeHelpers.TypeMatchesWithNullAwareness(typeof(NullType), typeof(NullType), allowParentTypes: true));
        Assert.True(TypeHelpers.TypeMatchesWithNullAwareness(typeof(NullType), typeof(object), allowParentTypes: true));
        Assert.True(TypeHelpers.TypeMatchesWithNullAwareness(typeof(object), typeof(NullType), allowParentTypes: true));

        Assert.False(TypeHelpers.TypeMatchesWithNullAwareness(typeof(bool), typeof(NullType), allowParentTypes: true));
        Assert.False(TypeHelpers.TypeMatchesWithNullAwareness(typeof(NullType), typeof(bool), allowParentTypes: true));
    }

    [Fact]
    public void TryPropagateNullBinary_Propagates_WhenAnyOperandIsNull()
    {
        Assert.True(TypeHelpers.TryPropagateNullBinary(null, true, out var r1));
        Assert.Same(NullType.Instance, r1);

        Assert.True(TypeHelpers.TryPropagateNullBinary(1, NullType.Instance, out var r2));
        Assert.Same(NullType.Instance, r2);

        Assert.False(TypeHelpers.TryPropagateNullBinary(1, 2, out var r3));
        Assert.Null(r3);
    }

    [Fact]
    public void NullValue_Equals_Null_WithOperatorAndEquals()
    {
        Assert.True(NullType.Instance == null);
        Assert.True(null == NullType.Instance);
        Assert.True(NullType.Instance.Equals(null));
        Assert.False(NullType.Instance != null);
    }

    [Fact]
    public void NullType_OrOperator_WithTrue_ReturnsTrue()
    {
        Assert.True(NullType.Instance | true);
        Assert.True(true | NullType.Instance);
    }
}
