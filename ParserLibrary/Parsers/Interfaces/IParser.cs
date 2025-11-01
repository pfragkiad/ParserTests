using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.Interfaces;
using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;
using ParserLibrary.Parsers.Compilation;
using FluentValidation.Results;

namespace ParserLibrary.Parsers.Interfaces;

public interface IParser : ITokenizer
{
    // ---------------- Configuration / Metadata ----------------

    /// <summary>
    /// Public constants available to the parser. Implementations may override and populate.
    /// These are merged with provided variables during Evaluate/EvaluateType.
    /// </summary>
    Dictionary<string, object?> Constants { get; }

    /// <summary>
    /// Register a custom function with a simple definition format:
    /// "name(arg1,arg2,...) = expressionBody"
    /// </summary>
    void RegisterFunction(string definition);

    // ---------------- Expression trees ----------------

    /// <summary>
    /// Builds an expression tree from an expression string.
    /// </summary>
    TokenTree GetExpressionTree(string expression);

    /// <summary>
    /// Builds an expression tree from a postfix token list.
    /// </summary>
    TokenTree GetExpressionTree(List<Token> postfixTokens);

    // ---------------- Evaluation APIs ----------------

    /// <summary>
    /// Evaluate using custom value type V with optional operator/function delegates.
    /// </summary>
    V? Evaluate<V>(
        string expression,
        Func<string, V>? literalParser = null,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V?, V?, V?>>? binaryOperators = null,
        Dictionary<string, Func<V?, V?>>? unaryOperators = null,
        Dictionary<string, Func<V?, V?>>? funcs1Arg = null,
        Dictionary<string, Func<V?, V?, V?>>? funcs2Arg = null,
        Dictionary<string, Func<V?, V?, V?, V?>>? funcs3Arg = null
    );

    /// <summary>
    /// Evaluate to object using the parser's built-in semantics.
    /// </summary>
    object? Evaluate(string expression, Dictionary<string, object?>? variables = null, bool optimizeTree = false);

    /// <summary>
    /// Returns the inferred result Type of the expression using the parser's type rules.
    /// </summary>
    Type EvaluateType(string expression, Dictionary<string, object?>? variables = null);

    // ---------------- Validation / Checks ----------------

    /// <summary>
    /// Checks function names in an expression string against known main/custom functions.
    /// </summary>
    FunctionNamesCheckResult CheckFunctionNames(string expression);

    /// <summary>
    /// Checks function names in an already tokenized infix sequence.
    /// </summary>
    FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens);

    /// <summary>
    /// Returns distinct matched function names from an expression string.
    /// </summary>
    List<string> GetMatchedFunctionNames(string expression);

    /// <summary>
    /// Returns distinct matched function names from an already tokenized sequence.
    /// </summary>
    List<string> GetMatchedFunctionNames(List<Token> tokens);

    /// <summary>
    /// Validates that functions do not contain empty argument placeholders.
    /// </summary>
    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(string expression);

    /// <summary>
    /// Validates function argument counts against metadata.
    /// </summary>
    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression);

    /// <summary>
    /// Validates binary operator operands.
    /// </summary>
    InvalidBinaryOperatorsCheckResult CheckBinaryOperatorOperands(string expression);


    /// <summary>
    /// Validates unary operator operands.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    InvalidUnaryOperatorsCheckResult CheckUnaryOperatorOperands(string expression);


    /// <summary>
    /// Validates argument separators for orphan/invalid placements.
    /// </summary>
    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression);

    /// <summary>
    /// Orchestrates tokenizer and parser validations in a single pass.
    /// </summary>
    ParserValidationReport Validate(
        string expression,
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false);

    /// <summary>
    /// Optimizes using runtime inference of variable types from provided instances.
    /// </summary>
    TreeOptimizerResult GetOptimizedTree(string expression, Dictionary<string, object?>? variables = null);

    /// <summary>
    /// Optimizes an existing tree using runtime type inference.
    /// </summary>
    TreeOptimizerResult GetOptimizedTree(TokenTree tree, Dictionary<string, object?>? variables = null,  bool cloneTree = true);
    UnexpectedOperatorOperandsCheckResult CheckAdjacentOperands(string expression);

    // ---------------- Compilation APIs ----------------

    /// <summary>
    /// Compiles the expression into tokens/postfix/tree (depth derived from optimization mode) and
    /// optionally optimizes the tree.
    /// </summary>
    ParserCompilationResult Compile(
        string expression,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    /// <summary>
    /// Compiles the expression into tokens/postfix/tree based on explicit options (no optimization).
    /// </summary>
    ParserCompilationResult Compile(string expression, ParserCompilationOptions? options = null);

    /// <summary>
    /// Compiles the expression into tokens/postfix/tree based on explicit options and optimization mode.
    /// </summary>
    ParserCompilationResult Compile(
        string expression,
        ParserCompilationOptions options,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    /// <summary>
    /// Compiles from existing infix tokens based on explicit options (no optimization).
    /// </summary>
    ParserCompilationResult Compile(List<Token> infixTokens, ParserCompilationOptions options);

    /// <summary>
    /// Compiles from existing infix tokens based on explicit options and optimization mode.
    /// </summary>
    ParserCompilationResult Compile(
        List<Token> infixTokens,
        ParserCompilationOptions options,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);
    List<Token> GetIdentifiers(string expression, string captureGroup, bool excludeConstantNames = true);
    string GetExpandedExpressionString(string expression, bool spacesAroundOperators = true, int maxDepth = 10);
}