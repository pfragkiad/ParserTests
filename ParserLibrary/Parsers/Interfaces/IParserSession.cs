using FluentValidation.Results;
using ParserLibrary.Parsers.Validation;

namespace ParserLibrary.Parsers.Interfaces;

public enum ExpressionOptimizationMode
{
    None = 0,
    StaticTypeMaps,
    ParserInference
}

public interface IParserSession : IParser
{
    string Expression { get; set; }
    Dictionary<string, object?> Variables { get; set; }

    object? Evaluate();
    object? Evaluate(Dictionary<string, object?>? variables = null);
    Type EvaluateType();
    Type EvaluateType(Dictionary<string, object?>? variables = null);

    object? EvaluateWithTreeOptimizer(
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.StaticTypeMaps);

    object? EvaluateWithParserInferenceOptimizer(
        string expression,
        Dictionary<string, object?>? variables = null);

    // ---------------- Validation ----------------

    /// <summary>
    /// Runs tokenizer and parser validations against the current expression.
    /// If variableNamesOptions.KnownIdentifierNames is null/empty, Variables.Keys are used.
    /// </summary>
    ParserValidationReport Validate(
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false);
}