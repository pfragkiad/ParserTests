using System.Text.RegularExpressions;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizer
{
    // ---------------- Options ----------------

    /// <summary>
    /// The tokenizer options (patterns, case sensitivity, etc.).
    /// </summary>
    TokenizerOptions TokenizerOptions { get; }

    // ---------------- Tokenization ----------------

    /// <summary>
    /// Produces infix tokens from an expression string.
    /// </summary>
    List<Token> GetInfixTokens(string expression);

    /// <summary>
    /// Converts infix tokens to postfix tokens (Shunting-yard).
    /// </summary>
    List<Token> GetPostfixTokens(List<Token> infixTokens);

    /// <summary>
    /// Convenience overload to produce postfix tokens directly from an expression.
    /// </summary>
    List<Token> GetPostfixTokens(string expression);

    // ---------------- Parentheses validation ----------------

    /// <summary>
    /// Validates only parentheses balance/matching in the expression (string-only).
    /// </summary>
    ParenthesisCheckResult CheckParentheses(string expression);

    // ---------------- Variable names helpers ----------------

    /// <summary>
    /// Returns distinct identifier names from an expression string.
    /// </summary>
    List<string> GetVariableNames(string expression);

    /// <summary>
    /// Returns distinct identifier names from an infix token list.
    /// </summary>
    List<string> GetVariableNames(List<Token> infixTokens);

    // ---------------- Variable names checks (string-based convenience) ----------------

    /// <summary>
    /// Checks variable names using known names and prefix/postfix ignore lists.
    /// </summary>
    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignorePrefixes,
        HashSet<string> ignorePostfixes);

    /// <summary>
    /// Checks variable names using known names and an optional ignore regex pattern.
    /// </summary>
    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> knownIdentifierNames,
        Regex? ignoreIdentifierPattern = null);

    /// <summary>
    /// Checks variable names using known names and capture group names to ignore.
    /// </summary>
    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignoreCaptureGroups);

    // ---------------- Variable names checks (options-based) ----------------

    /// <summary>
    /// Checks variable names using a single options object (preferred).
    /// </summary>
    VariableNamesCheckResult CheckVariableNames(string expression, VariableNamesOptions variableNameOptions);

    // ---------------- Full tokenizer validation ----------------

    /// <summary>
    /// Runs tokenizer-level validation (parentheses and variable names) and produces a report.
    /// </summary>
    TokenizerValidationReport Validate(string expression, VariableNamesOptions variableNameOptions,
        IFunctionDescriptors? functionDescriptors = null,
        bool earlyReturnOnErrors = false);
    UnexpectedOperatorOperandsCheckResult CheckUnexpectedOperatorOperands(string expression);
    FunctionNamesCheckResult CheckFunctionNames(string expression, IFunctionDescriptors functionDescriptors);
    List<Token> GetIdentifiers(string expression, string captureGroup);
}