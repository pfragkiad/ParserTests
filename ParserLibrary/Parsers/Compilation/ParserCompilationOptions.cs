using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers.Compilation;

public readonly record struct ParserCompilationOptions
{
    public bool BuildPostfix { get; init; }
    public bool BuildTree { get; init; }

    public static readonly ParserCompilationOptions InfixOnly = new()
    {
        BuildPostfix = false,
        BuildTree = false
    };

    public static readonly ParserCompilationOptions InfixAndPostfix = new()
    {
        BuildPostfix = true,
        BuildTree = false
    };

    public static readonly ParserCompilationOptions Full = new()
    {
        BuildPostfix = true,
        BuildTree = true
    };

    public static ParserCompilationOptions FromOptimizationMode(ExpressionOptimizationMode mode) => mode switch
    {
        ExpressionOptimizationMode.None => InfixAndPostfix,
        _ => Full
    };
}