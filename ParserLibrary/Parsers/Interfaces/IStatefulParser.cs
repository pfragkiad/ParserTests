using FluentValidation.Results;
using ParserLibrary.Parsers.Validation;

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
    // ---------------- State & configuration ----------------

    /// <summary>
    /// Current expression held by the stateful parser.
    /// </summary>
    string Expression { get; set; }

    /// <summary>
    /// Current variables map (merged with parser Constants on set).
    /// </summary>
    Dictionary<string, object?> Variables { get; set; }

    // ---------------- Basic evaluation (no optimization) ----------------

    /// <summary>
    /// Evaluates the current expression using the current state (Expression, Variables).
    /// </summary>
    object? Evaluate();

    /// <summary>
    /// Evaluates the current expression using the provided variables (overrides current Variables for this call).
    /// </summary>
    object? Evaluate(Dictionary<string, object?>? variables = null);

    /// <summary>
    /// Infers the result Type of the current expression using the current state.
    /// </summary>
    Type EvaluateType();

    /// <summary>
    /// Infers the result Type of the current expression using the provided variables.
    /// </summary>
    Type EvaluateType(Dictionary<string, object?>? variables = null);

    // ---------------- Optimized evaluation ----------------

    /// <summary>
    /// Evaluates after preparing tokens/tree with the requested optimization mode.
    /// For StaticTypeMaps, you may provide variableTypes/functionReturnTypes/ambiguousFunctionReturnTypes.
    /// For ParserInference, types are inferred from provided variables.
    /// </summary>
    object? EvaluateWithTreeOptimizer(
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.StaticTypeMaps);

    /// <summary>
    /// Convenience API to evaluate with parser-inference optimizer (equivalent to optimizationMode = ParserInference).
    /// </summary>
    object? EvaluateWithParserInferenceOptimizer(
        string expression,
        Dictionary<string, object?>? variables = null);

    // ---------------- Validation ----------------

    /// <summary>
    /// Runs tokenizer and parser validations against the current expression.
    /// If variableNamesOptions.KnownIdentifierNames is null/empty, Variables.Keys are used.
    /// </summary>
    List<ValidationFailure> Validate(
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false);
}