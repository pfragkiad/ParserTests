using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers.Compilation;

public readonly record struct ParserCompilationOptions
{
    public bool BuildPostfix { get; init; }
    public bool BuildTree { get; init; }

    /// <summary>
    /// When <c>true</c> and <see cref="BuildTree"/> is also <c>true</c>, the parent-map
    /// (<see cref="Tree{T}.ParentMap"/>) is built immediately after the tree is constructed.
    /// </summary>
    public bool BuildParentMap { get; init; }

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

    public static readonly ParserCompilationOptions FullWithParentMap = new()
    {
        BuildPostfix = true,
        BuildTree = true,
        BuildParentMap = true
    };

    public static ParserCompilationOptions FromOptimizationMode(ExpressionOptimizationMode mode) => mode switch
    {
        ExpressionOptimizationMode.None => InfixAndPostfix,
        _ => Full
    };
}