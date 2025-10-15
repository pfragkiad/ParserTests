using OneOf;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;
using ParserLibrary.Parsers.Compilation;

namespace ParserLibrary.Parsers.Interfaces;

public enum ExpressionOptimizationMode
{
    None = 0,
    StaticTypeMaps,
    ParserInference
}

/// <summary>
/// Stateful parser session that caches infix/postfix/tree between calls,
/// and exposes validation, compilation, optimization and evaluation helpers for the current Expression.
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

    void Reset();

    ParserSessionState State { get; }

    // ---------------- Evaluation APIs (session) ----------------

    /// <summary>
    /// Evaluate current prepared state (tree or postfix), using current Variables.
    /// </summary>
    object? Evaluate();

    /// <summary>
    /// Re-prepare current Expression with provided variables, then evaluate.
    /// </summary>
    object? Evaluate(
       Dictionary<string, object?>? variables = null,
       bool runValidation = false,
       bool optimize = false);

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
    ParenthesisCheckResult CheckParentheses();

    /// <summary>
    /// Extracts identifier tokens (variable names) from cached infix.
    /// </summary>
    List<string> GetVariableNames();

    VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignorePrefixes,
        HashSet<string> ignorePostfixes);

    VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        Regex? ignoreIdentifierPattern = null);

    VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignoreCaptureGroups);

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
    UnexpectedOperatorOperandsCheckResult CheckAdjacentOperands();
    ParserValidationReport Validate(VariableNamesOptions? nameOptions = null, bool earlyReturnOnErrors = false);

    ParserCompilationResult Compile(bool reset = false, bool optimize = false, bool forceTreeBuild = false);
    List<string> GetIdentifierNames(string captureGroup, bool excludeConstantNames = true);
    List<string> GetIdentifierNames();
    List<string> GetFunctionNames();
}