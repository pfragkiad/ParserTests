using OneOf;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Interfaces;

public enum ExpressionOptimizationMode
{
    None = 0,
    StaticTypeMaps,
    ParserInference
}

/// <summary>
/// Stateful parser session that caches infix/postfix/tree between calls,
/// and exposes validation, optimization and evaluation helpers for the current Expression.
/// Inherits core APIs from IParser.
/// </summary>
public interface IParserSession : IParser
{
    // ---------------- Session state ----------------

    /// <summary>
    /// Current expression for this session. Setting it resets cached state.
    /// </summary>
    string Expression { get; set; }

    /// <summary>
    /// Variables used during evaluation/type inference. Constants (if any) are merged internally.
    /// </summary>
    Dictionary<string, object?> Variables { get; set; }

    // ---------------- Validation + Optimization ----------------

    /// <summary>
    /// Validates (optional) and optimizes (optional) the given expression and updates session caches.
    /// Returns the full validation report (success if validation is disabled and no errors).
    /// </summary>
    ParserValidationReport ValidateAndOptimize(
        string expression,
        Dictionary<string, object?>? variables = null,
        VariableNamesOptions? variableNamesOptions = null,
        bool runValidation = true,
        bool earlyReturnOnValidationErrors = false,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    /// <summary>
    /// Optimization only. Updates the cached infix/postfix/tree/node-dictionary and returns the optimization result.
    /// ParserInference uses current Variables if variable types are not provided.
    /// </summary>
    TreeOptimizerResult GetOptimizedTree(
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    /// <summary>
    /// Session-scoped validation using the current Expression (no expression parameter).
    /// Runs tokenizer + parser validations and returns a consolidated report.
    /// </summary>
    ParserValidationReport Validate(
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false);

    // ---------------- Evaluation APIs (session) ----------------

    /// <summary>
    /// Evaluate current prepared state (tree or postfix), using current Variables.
    /// </summary>
    object? Evaluate();

    /// <summary>
    /// Re-prepare current Expression with provided variables (no validation/optimization), then evaluate.
    /// </summary>
    OneOf<object?, ParserValidationReport> Evaluate(
       Dictionary<string, object?>? variables = null,
       bool runValidation = false,
       ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None);


    /// <summary>
    /// Type-evaluate current prepared state (tree or postfix), using current Variables.
    /// </summary>
    Type EvaluateType();

    /// <summary>
    /// Re-prepare current Expression with provided variables (no validation/optimization), then type-evaluate.
    /// </summary>
    Type EvaluateType(Dictionary<string, object?>? variables = null);

    // ---------------- Utility validation helpers (tokenizer-based) ----------------

    /// <summary>
    /// Parentheses check for current Expression (string-only).
    /// </summary>
    ParenthesisCheckResult ValidateParentheses();

    /// <summary>
    /// Extracts identifier tokens (variable names) from cached infix.
    /// </summary>
    List<string> GetVariableNames();

    VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes);

    VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        Regex? ignoreIdentifierPattern = null);

    VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        string[] ignoreCaptureGroups);

    VariableNamesCheckResult CheckVariableNames(VariableNamesOptions variableNameOptions);

    // ---------------- Utility validation helpers (parser-based) ----------------

    /// <summary>
    /// Checks that all function names used are known by metadata (custom or main).
    /// </summary>
    FunctionNamesCheckResult CheckFunctionNames();

    /// <summary>
    /// Checks binary operator operands (reports operators with null operands).
    /// </summary>
    InvalidBinaryOperatorsCheckResult CheckBinaryOperators();

    /// <summary>
    /// Checks unary operator operands (reports unary operators with null operand).
    /// </summary>
    InvalidUnaryOperatorsCheckResult CheckUnaryOperators();

    /// <summary>
    /// Checks that argument separators belong to a function or a separator chain.
    /// </summary>
    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators();

    /// <summary>
    /// Checks function arguments count (fixed or min-variable based on metadata).
    /// </summary>
    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount();

    /// <summary>
    /// Checks for empty function arguments (NULL placeholders).
    /// </summary>
    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments();

    /// <summary>
    /// Checks for adjacent operands without an operator in between (e.g. "2 3" or "a (b + c)").
    /// </summary>
    /// <returns></returns>
    AdjacentOperandsCheckResult CheckAdjacentOperands();
}