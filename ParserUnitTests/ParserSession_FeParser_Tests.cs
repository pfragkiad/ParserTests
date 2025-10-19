using Microsoft.Extensions.Hosting;
using ParserLibrary;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers;
using Xunit;

namespace ParserUnitTests;


public class FeParser(ILogger<FeParser> logger, ParserServices dependencies) : DoubleParserSession(logger, dependencies)
{
    public override Dictionary<string, object?> Constants =>
        new(base.Constants) { ["tol"] = 1e-5 };

    protected override object EvaluateLiteral(string s, string? group) =>
        group switch
        {
            "int" => int.Parse(s, CultureInfo.InvariantCulture),
            "float" => double.Parse(s, CultureInfo.InvariantCulture),
            "string" => s,
            _ => base.EvaluateLiteral(s, group),
        };

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        if (leftOperand is null || rightOperand is null) return false;
        if ((leftOperand is double dl && double.IsNaN(dl)) || (rightOperand is double dr && double.IsNaN(dr))) return false;

        dynamic left = leftOperand!;
        dynamic right = rightOperand!;

        return operatorName.ToLowerInvariant() switch
        {
            // bitwise
            "&" => left & right,
            "|" => left | right,
            "^" => left ^ right,
            "xor" => left ^ right, //boolean
            ">>" => left >> right,
            "<<" => left << right,

            // boolean words
            "and" => left && right,  //boolean
            "or" => left || right,  //boolean

            // inequalities
            "<" => left < right,
            "<=" => left <= right,
            ">" => left > right,
            ">=" => left >= right,
            "=" => left == right,

            _ => base.EvaluateOperator(operatorName, leftOperand, rightOperand)
        };
    }

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        dynamic v = operand!;
        return operatorName.ToLowerInvariant() switch
        {
            "!" => ~v,   // bitwise NOT
            "not" => !v,   // logical NOT
            _ => base.EvaluateUnaryOperator(operatorName, operand),
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        // Switch expression for easy extensibility
        return functionName.ToLowerInvariant() switch
        {
            // if(<bool>, <expr1>, <expr2>) – first is strictly bool, others can be any type
            "if" => ((bool)args[0]!) ? (dynamic)args[1]! : (dynamic)args[2]!,

            // add more functions here as new cases...

            _ => base.EvaluateFunction(functionName, args),
        };
    }

    protected override Dictionary<string, byte> MainFunctionsWithFixedArgumentsCount =>
        new(base.MainFunctionsWithFixedArgumentsCount)
        {
            {"if", 3}
        };
}

internal static class MyTokenPatterns
{
    public readonly static TokenizerOptions TokenizerOptions = new()
    {
        Version = "1.1",
        TokenPatterns = new TokenPatterns
        {
            CaseSensitive = false,

            NamedIdentifiers =
            [
                new SinglePattern { Name = "variable", Value = @"(?<!\.)(?<variable>\b(?!\[)[A-Za-z_]\w*(?!\]))\b" },
            ],
            NamedLiterals =
            [
                new SinglePattern { Name = "string", Value = @"""(?<string>.*?)""" },
                new SinglePattern { Name = "float",  Value = @"(?<float>\b\d+\.\d*\b|\b\.\d+\b)" },
                new SinglePattern { Name = "int",    Value = @"(?<int>\b\d+\b)" },
            ],

            OpenParenthesis = '(',
            CloseParenthesis = ')',
            ArgumentSeparator = ',',

            // Unary operators (include word-based logical NOT)
            Unary =
            [
                new UnaryOperator { Name = "-",   Priority = 7, Prefix = true },
                new UnaryOperator { Name = "+",   Priority = 7, Prefix = true },
                new UnaryOperator { Name = "!",   Priority = 7, Prefix = true },
                new UnaryOperator { Name = "not", Priority = 7, Prefix = true },
            ],

            // Binary operators
            Operators =
            [
                new Operator { Name = ".",   Priority = 9, LeftToRight = true },

                // Logical/bitwise precedence mapping:
                // or (|) lowest, xor (^) middle, and (&) highest (among them)
                new Operator { Name = "or",  Priority = 1, LeftToRight = true },
                new Operator { Name = "|",   Priority = 1, LeftToRight = true },
                new Operator { Name = "^",   Priority = 2, LeftToRight = true },   // XOR
                new Operator { Name = "xor", Priority = 2, LeftToRight = true },   // word form
                new Operator { Name = "and", Priority = 3, LeftToRight = true },
                new Operator { Name = "&",   Priority = 3, LeftToRight = true },

                // Comparisons
                new Operator { Name = "=",   Priority = 4, LeftToRight = true },
                new Operator { Name = "<",   Priority = 4, LeftToRight = true },
                new Operator { Name = "<=",  Priority = 4, LeftToRight = true },
                new Operator { Name = ">",   Priority = 4, LeftToRight = true },
                new Operator { Name = ">=",  Priority = 4, LeftToRight = true },

                // Shifts
                new Operator { Name = ">>",  Priority = 4, LeftToRight = true },
                new Operator { Name = "<<",  Priority = 4, LeftToRight = true },

                // Arithmetic
                new Operator { Name = "+",   Priority = 5, LeftToRight = true },
                new Operator { Name = "-",   Priority = 5, LeftToRight = true },
                new Operator { Name = "*",   Priority = 6, LeftToRight = true },
                new Operator { Name = "/",   Priority = 6, LeftToRight = true },
                new Operator { Name = "%",   Priority = 6, LeftToRight = true },
            ],
        }
    };
}


public sealed class FeSessionFixture : IDisposable
{
    public IHost Host { get; }

    public FeSessionFixture()
    {
        // Build a host with FeParser and the provided custom TokenizerOptions
        Host = ParserApp.GetParserSessionApp<FeParser>(MyTokenPatterns.TokenizerOptions);
    }

    public ParserSessionBase CreateSession() => (ParserSessionBase)Host.GetParserSession();

    public void Dispose() => Host.Dispose();
}

public class ParserSession_FeParser_Tests : IClassFixture<FeSessionFixture>
{
    private readonly FeSessionFixture _fixture;
    public ParserSession_FeParser_Tests(FeSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetSession() => _fixture.CreateSession();

    [Fact]
    public void Bitwise_Chain_Evaluates_And_Validates()
    {
        var session = GetSession();
        session.Expression = "a & b | c ^ d >> 2 << 1";

        // Validation with known variables
        var report = session.Validate(new VariableNamesOptions
        {
            KnownIdentifierNames = ["a", "b", "c", "d"]
        });
        Assert.True(report.IsSuccess);

        // Provide integer inputs
        session.Variables = new()
        {
            ["a"] = 13,
            ["b"] = 11,
            ["c"] = 6,
            ["d"] = 9
        };

        var result = session.Evaluate();
        var value = Assert.IsType<int>(result);


        int a = (int)session.Variables["a"]!;
        int b = (int)session.Variables["b"]!;
        int c = (int)session.Variables["c"]!;
        int d = (int)session.Variables["d"]!;
        int expected = (a & b) | (c ^ ((d >> 2) << 1));

        Assert.Equal(expected, value);

        // Also assert the binary representation
        Assert.Equal("1011", Convert.ToString(value, 2));
    }

    [Fact]
    public void Boolean_If_Function_Evaluates_And_Validates()
    {
        var session = GetSession();
        session.Expression = "if( (a > 0 and b > 0) or (not c) , 1 , 0 )";
        var _ = session.Compile(forceTreeBuild: false);

        // Validation with known variables
        var report = session.Validate(new VariableNamesOptions
        {
            KnownIdentifierNames = ["a", "b", "c"]

        });
        Assert.True(report.IsSuccess);

        session.Variables = new()
        {
            ["a"] = 13,
            ["b"] = -1,
            ["c"] = false
        };
        int a = (int)session.Variables["a"]!;
        int b = (int)session.Variables["b"]!;
        bool c = (bool)session.Variables["c"]!;

        bool cond = ((a > 0) && (b > 0)) || (!c);
        var expected = cond ? 1 : 0;

        var result = session.Evaluate();
        var value = Assert.IsType<int>(result);
        Assert.Equal(expected, value);
    }
}