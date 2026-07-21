using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ParserLibrary;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers;
using System.Collections.Generic;

namespace ParserBenchmarks;

/// <summary>
/// Benchmarks the tokenizer and parser hot paths before/after the ReadOnlyMemory&lt;char&gt; Token patch.
///
/// Before the patch: every token materialized a new string via Match.Value / Substring.
/// After the patch : tokens store (inputMemory, start, length); strings are only allocated
///                   if/when Token.Text is accessed.
///
/// Run in Release mode:
///   dotnet run --project ParserBenchmarks -c Release
/// </summary>
[SimpleJob(RuntimeMoniker.HostProcess)]
[MemoryDiagnoser]
[HideColumns("StdDev", "Median", "RatioSD")]
public class TokenizerBenchmark
{
    private IParser _parser = null!;

    // ── representative expression workload ───────────────────────────────────

    /// <summary>Short expression (few tokens).</summary>
    [Params(
        "1 + 2 * 3",
        "sin(x) + cos(y) * tan(z) - 1.5",
        "(a + b) * (c - d) / (e + f * g - h) + pi",
        "sqrt(x^2 + y^2) + log(abs(a - b) * 3.14) / (n + 1)"
    )]
    public string Expression { get; set; } = "1 + 2 * 3";

    [GlobalSetup]
    public void Setup()
    {
        _parser = ParserApp.GetDoubleParser();
    }

    // ── benchmark: tokenize only (GetInfixTokens) ───────────────────────────

    [Benchmark(Description = "GetInfixTokens")]
    public int GetInfixTokens()
    {
        var tokens = _parser.GetInfixTokens(Expression);
        return tokens.Count; // prevent dead-code elimination
    }

    // ── benchmark: tokenize + postfix ───────────────────────────────────────

    [Benchmark(Description = "GetPostfixTokens")]
    public int GetPostfixTokens()
    {
        var tokens = _parser.GetPostfixTokens(Expression);
        return tokens.Count;
    }

    // ── benchmark: full evaluate ─────────────────────────────────────────────

    [Benchmark(Description = "Evaluate (double)")]
    public object? Evaluate()
    {
        var vars = new Dictionary<string, object?> { ["x"] = 1.5, ["y"] = 2.0, ["z"] = 0.5,
                                                      ["a"] = 3.0, ["b"] = 1.0, ["c"] = 4.0,
                                                      ["d"] = 2.0, ["e"] = 1.0, ["f"] = 0.5,
                                                      ["g"] = 2.0, ["h"] = 0.5, ["n"] = 10.0 };
        return _parser.Evaluate(Expression, vars);
    }

    // ── benchmark: token text access (materializes string) ──────────────────
    // This represents the cost when callers DO need the managed string.

    [Benchmark(Description = "Token.Text access")]
    public int TokenTextAccess()
    {
        var tokens = _parser.GetInfixTokens(Expression);
        int len = 0;
        foreach (var t in tokens)
            len += t.Text.Length; // forces lazy materialization
        return len;
    }

    // ── benchmark: token span access (no string allocation) ─────────────────

    [Benchmark(Description = "Token.Span access")]
    public int TokenSpanAccess()
    {
        var tokens = _parser.GetInfixTokens(Expression);
        int len = 0;
        foreach (var t in tokens)
            len += t.Span.Length; // zero-allocation hot path
        return len;
    }
}
