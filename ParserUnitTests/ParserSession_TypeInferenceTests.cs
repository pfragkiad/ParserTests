using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using ParserLibrary.Parsers.Validation;
using ParserTests.Common.Parsers;
using Xunit;
using System;

namespace ParserUnitTests;

public class ParserSession_TypeInferenceTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_TypeInferenceTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void EvaluateType_IntAddition_ReturnsInt()
    {
        var session = GetItemSession();
        session.Expression = "1 + 2";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);

        var t = session.EvaluateType();
        Assert.Equal(typeof(int), t);
    }

    [Fact]
    public void EvaluateType_DoublePlusInt_ReturnsDouble()
    {
        var session = GetItemSession();
        session.Expression = "1.5 + 2";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);

        var t = session.EvaluateType();
        Assert.Equal(typeof(double), t);
    }

    [Fact]
    public void EvaluateType_ItemPlusInt_ReturnsItem()
    {
        var session = GetItemSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = typeof(Item) };

        var report = session.ValidateAndCompile(new VariableNamesOptions { KnownIdentifierNames = ["a"] });
        Assert.True(report.IsSuccess);

        var t = session.EvaluateType();
        Assert.Equal(typeof(Item), t);
    }

    [Fact]
    public void EvaluateType_ZeroArgFunction_Tre_ReturnsInt()
    {
        var session = GetItemSession();
        session.Expression = "tre()";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);

        var t = session.EvaluateType();
        Assert.Equal(typeof(int), t);
    }
}