using ParserLibrary;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Helpers;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers;
using ParserLibrary.Parsers.Validation;
using ParserTests.Common.Parsers;
using Xunit;

namespace ParserUnitTests;

public class Parser_ValueAwareTypeInferenceTests
{
    private static IParser GetParser() => ParserApp.GetParser<ValueAwareTypeInferenceParser>(TokenizerOptions.Default);

    private static ParserSessionBase GetSession() =>
        (ParserSessionBase)ParserApp.GetParserSession<ValueAwareTypeInferenceParserSession>(TokenizerOptions.Default);

    [Fact]
    public void EvaluateType_ValueDependentFunction_UsesLiteralSelector()
    {
        var parser = GetParser();
        Dictionary<string, object?> variables = new()
        {
            ["i"] = 10,
            ["d"] = 2.5
        };

        Assert.Equal(typeof(int), parser.EvaluateType("pick(1, i, d)", variables));
        Assert.Equal(typeof(double), parser.EvaluateType("pick(2, i, d)", variables));
    }

    [Fact]
    public void EvaluateType_ValueDependentFunction_UsesFallbackType_WhenSelectorRequiresNestedCatalogEvaluation()
    {
        var parser = GetParser();
        Dictionary<string, object?> variables = new()
        {
            ["i"] = 10,
            ["d"] = 2.5
        };

        var resultType = parser.EvaluateType("pick(pick(1, 1, 2), i, d)", variables);

        Assert.Equal(typeof(object), resultType);
    }

    [Fact]
    public void EvaluateType_ValueDependentFunction_UsesFallbackType_WhenSelectorRequiresCustomFunctionEvaluation()
    {
        var parser = GetParser();
        parser.RegisterFunction("mode() = 2");
        Dictionary<string, object?> variables = new()
        {
            ["i"] = 10,
            ["d"] = 2.5
        };

        var resultType = parser.EvaluateType("pick(mode(), i, d)", variables);

        Assert.Equal(typeof(object), resultType);
    }

    [Fact]
    public void EvaluateType_AnyTypeFunction_UsesNullType_WhenArgumentIsNull()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("echoany(a)", new Dictionary<string, object?> { ["a"] = null });

        Assert.Equal(typeof(NullType), resultType);
    }

    [Fact]
    public void EvaluateType_AnyTypeFunction_UsesDouble_WhenArgumentIsDouble()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("echoany(a)", new Dictionary<string, object?> { ["a"] = 2.5 });

        Assert.Equal(typeof(double), resultType);
    }

    [Fact]
    public void EvaluateType_AnyNonNullTypeFunction_UsesDouble_WhenArgumentIsDouble()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("echoanynonnull(a)", new Dictionary<string, object?> { ["a"] = 2.5 });

        Assert.Equal(typeof(double), resultType);
    }

    [Fact]
    public void EvaluateType_AnyNonNullTypeFunction_RejectsNullArgument()
    {
        var parser = GetParser();

        Assert.Throws<InvalidOperationException>(() => parser.EvaluateType("echoanynonnull(a)", new Dictionary<string, object?> { ["a"] = null }));
    }

    [Fact]
    public void EvaluateType_ValueDependentBinaryOperator_UsesLiteralLeftOperand()
    {
        var parser = GetParser();
        Dictionary<string, object?> variables = new()
        {
            ["d"] = 2.5
        };

        Assert.Equal(typeof(int), parser.EvaluateType("0 + d", variables));
        Assert.Equal(typeof(double), parser.EvaluateType("1 + d", variables));
    }

    [Fact]
    public void EvaluateType_ValueDependentBinaryOperator_UsesFallbackType_WhenLeftOperandRequiresNestedEvaluation()
    {
        var parser = GetParser();
        Dictionary<string, object?> variables = new()
        {
            ["d"] = 2.5
        };

        var resultType = parser.EvaluateType("(0 + 1) + d", variables);

        Assert.Equal(typeof(object), resultType);
    }

    [Fact]
    public void EvaluateType_ValueDependentBinaryOperator_UsesNullType_WhenVariableIsNull()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("a + 1", new Dictionary<string, object?> { ["a"] = null });

        Assert.Equal(typeof(NullType), resultType);
    }

    [Fact]
    public void EvaluateType_ValueDependentBinaryOperator_UsesInt_WhenVariableIsInt()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("a + 1", new Dictionary<string, object?> { ["a"] = 0 });

        Assert.Equal(typeof(int), resultType);
    }

    [Fact]
    public void EvaluateType_ValueDependentBinaryOperator_UsesDouble_WhenVariableDrivesDoubleResult()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("a + b", new Dictionary<string, object?>
        {
            ["a"] = 1,
            ["b"] = 2.5
        });

        Assert.Equal(typeof(double), resultType);
    }

    [Fact]
    public void EvaluateType_ValueDependentUnaryOperator_UsesLiteralOperand()
    {
        var parser = GetParser();

        Assert.Equal(typeof(int), parser.EvaluateType("-0"));
        Assert.Equal(typeof(double), parser.EvaluateType("-1"));
    }

    [Fact]
    public void EvaluateType_ValueDependentUnaryOperator_UsesFallbackType_WhenOperandRequiresNestedEvaluation()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("-(0 + 1)");

        Assert.Equal(typeof(object), resultType);
    }

    [Fact]
    public void EvaluateType_ValueDependentUnaryOperator_UsesNullType_WhenVariableIsNull()
    {
        var parser = GetParser();

        var resultType = parser.EvaluateType("-a", new Dictionary<string, object?> { ["a"] = null });

        Assert.Equal(typeof(NullType), resultType);
    }

    [Fact]
    public void SessionEvaluateType_ValueDependentFunction_UsesCurrentVariableValues()
    {
        var session = GetSession();
        session.Expression = "pick(sel, i, d)";
        session.Variables = new()
        {
            ["sel"] = 1,
            ["i"] = 10,
            ["d"] = 2.5
        };

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["sel", "i", "d"] });
        Assert.True(report.IsSuccess);

        var resultType = session.EvaluateType();

        Assert.Equal(typeof(int), resultType);
    }

    [Fact]
    public void SessionEvaluateType_ValueDependentBinaryOperator_UsesCurrentVariableValues()
    {
        var session = GetSession();
        session.Expression = "sel + d";
        session.Variables = new()
        {
            ["sel"] = 0,
            ["d"] = 2.5
        };

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["sel", "d"] });
        Assert.True(report.IsSuccess);

        var resultType = session.EvaluateType();

        Assert.Equal(typeof(int), resultType);
    }
}
