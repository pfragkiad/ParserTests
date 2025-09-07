using FluentValidation.Results;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers.Interfaces;

/// <summary>
/// Optimization strategy selection for stateful parser operations.
/// </summary>
public enum ExpressionOptimizationMode
{
    None = 0,
    StaticTypeMaps,    // Uses supplied variableTypes / functionReturnTypes / ambiguous resolvers
    ParserInference    // Uses parser’s own inference (Evaluate*Type) to classify numeric vs non-numeric
}

public interface IStatefulParser : IParser
{
    string? Expression { get; set; }
    Dictionary<string, object?> Variables { get; set; }

    // Basic (no optimization) evaluation APIs
    object? Evaluate();
    object? Evaluate(Dictionary<string, object?>? variables = null);
    Type EvaluateType();
    Type EvaluateType(Dictionary<string, object?>? variables = null);

    //// Existing simple optimized evaluation (kept for backward compatibility – defaults to StaticTypeMaps)
    //object? EvaluateWithTreeOptimizer(Dictionary<string, object?>? variables = null);

    // New: extended optimized evaluation with explicit maps and selectable mode
    object? EvaluateWithTreeOptimizer(
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.StaticTypeMaps);

    // New: parser-inference convenience API (equivalent to optimizationMode = ParserInference)
    object? EvaluateWithParserInferenceOptimizer(
        string expression,
        Dictionary<string, object?>? variables = null);

    // Optional: expose raw optimization result (before/after counts) if needed

    List<ValidationFailure> Validate(string[]? ignoreIdentifierCaptureGroups = null);
}