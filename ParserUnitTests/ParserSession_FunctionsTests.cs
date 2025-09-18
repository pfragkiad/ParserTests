using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using ParserLibrary.Parsers.Validation;
using Xunit;
using System;
using System.Linq;

namespace ParserUnitTests;

public class ParserSession_FunctionsTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_FunctionsTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void ZeroArgFunction_Tre_Succeeds_And_Returns100()
    {
        var session = GetItemSession();
        session.Expression = "tre()";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.True(report.IsSuccess);
        Assert.True(report.FunctionArgumentsCountResult!.IsSuccess);
        Assert.True(report.EmptyFunctionArgumentsResult!.IsSuccess);

        var fac = report.FunctionArgumentsCountResult!;
        var treEntry = fac.ValidFunctions.FirstOrDefault(f => string.Equals(f.FunctionName, "tre", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(treEntry.FunctionName);
        Assert.Equal(0, treEntry.ExpectedArgumentsCount);
        Assert.Equal(0, treEntry.ActualArgumentsCount);

        var result = session.Evaluate();
        Assert.IsType<int>(result);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void ZeroArgFunction_Tre_WithInnerSpaces_Succeeds()
    {
        var session = GetItemSession();
        session.Expression = "tre(   )";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.True(report.IsSuccess);
        Assert.True(report.FunctionArgumentsCountResult!.IsSuccess);
        Assert.True(report.EmptyFunctionArgumentsResult!.IsSuccess);

        var result = session.Evaluate();
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void ZeroArgFunction_Tre_CaseInsensitive_Succeeds()
    {
        var session = GetItemSession();
        session.Expression = "TrE()";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);
        Assert.True(report.FunctionArgumentsCountResult!.IsSuccess);

        var result = session.Evaluate();
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void Tre_Invalid_When_EmptyArgumentPresent()
    {
        var session = GetItemSession();
        session.Expression = "tre(,)";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.False(report.EmptyFunctionArgumentsResult!.IsSuccess);
        Assert.False(report.FunctionArgumentsCountResult!.IsSuccess);
        var invalidTre = report.FunctionArgumentsCountResult.InvalidFunctions
            .FirstOrDefault(f => string.Equals(f.FunctionName, "tre", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(invalidTre.FunctionName);
        Assert.Equal(0, invalidTre.ExpectedArgumentsCount);
        Assert.True((invalidTre.ActualArgumentsCount ?? -1) >= 1);
    }

    [Fact]
    public void Tre_Invalid_When_OneArgumentProvided()
    {
        var session = GetItemSession();
        session.Expression = "tre(1)";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.False(report.FunctionArgumentsCountResult!.IsSuccess);
        var invalidTre = report.FunctionArgumentsCountResult.InvalidFunctions
            .FirstOrDefault(f => string.Equals(f.FunctionName, "tre", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(invalidTre.FunctionName);
        Assert.Equal(0, invalidTre.ExpectedArgumentsCount);
        Assert.Equal(1, invalidTre.ActualArgumentsCount);
    }
}