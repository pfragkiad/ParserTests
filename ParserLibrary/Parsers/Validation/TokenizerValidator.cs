using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports; // ADDED
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers.Validation;

public class TokenizerValidator : ITokenizerValidator
{
    private readonly ILogger<TokenizerValidator> _logger;

    private readonly TokenPatterns _patterns;
    private readonly HashSet<string> _prefix;
    private readonly HashSet<string> _postfix;

    public TokenizerValidator(ILogger<TokenizerValidator> logger, TokenPatterns patterns)
    {
        _logger = logger;

        _patterns = patterns;
        _prefix = patterns.PrefixUnaryNames;
        _postfix = patterns.PostfixUnaryNames;
    }

    #region Parentheses

    public ParenthesisCheckResult CheckParentheses(string expression)
    {
        if (AreParenthesesMatchedFast(expression)) return ParenthesisCheckResult.Success;

        _logger.LogWarning("Unmatched parentheses detected.");
        return BuildParenthesisCheckDetail(expression);
    }

    // string-only, fast scan
    private bool AreParenthesesMatchedFast(string expression)
    {
        char open = _patterns.OpenParenthesis;
        char close = _patterns.CloseParenthesis;

        int count = 0;
        foreach (char c in expression)
        {
            if (c == open) { count++; continue; }
            if (c != close) continue;
            if (--count < 0) return false;
        }
        return count == 0;
    }

    // detailed positions (only when invalid)
    private ParenthesisCheckResult BuildParenthesisCheckDetail(string expression)
    {
        char open = _patterns.OpenParenthesis;
        char close = _patterns.CloseParenthesis;

        List<int> unmatchedClosed = [];
        List<int> openPositions = [];

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            if (c == open)
            {
                openPositions.Add(i);
                continue;
            }

            if (c != close) continue;

            if (openPositions.Count == 0)
                unmatchedClosed.Add(i);
            else
                openPositions.RemoveAt(openPositions.Count - 1);
        }

        return new ParenthesisCheckResult
        {
            UnmatchedClosed = unmatchedClosed,
            UnmatchedOpen = openPositions
        };
    }

    #endregion

    #region Variable names

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignoreCaptureGroups)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (knownIdentifierNames.Contains(t.Text))
            {
                matched.Add(t.Text);
                continue;
            }

            if (t.CaptureGroup is not null && ignoreCaptureGroups.Contains(t.CaptureGroup))
            {
                ignored.Add(t.Text);
                continue;
            }

            unmatched.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched],
            IgnoredNames = [.. ignored]
        };
    }

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> knownIdentifierNames,
        Regex? ignoreIdentifierPattern)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (knownIdentifierNames.Contains(t.Text))
            {
                matched.Add(t.Text);
                continue;
            }

            if (ignoreIdentifierPattern is not null && ignoreIdentifierPattern.IsMatch(t.Text))
            {
                ignored.Add(t.Text);
                continue;
            }

            unmatched.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched],
            IgnoredNames = [.. ignored]
        };
    }

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignorePrefixes,
        HashSet<string> ignorePostfixes)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (knownIdentifierNames.Contains(t.Text))
            {
                matched.Add(t.Text);
                continue;
            }

            bool ignoredByPrefix = ignorePrefixes.Any(p => t.Text.StartsWith(p));
            bool ignoredByPostfix = ignorePostfixes.Any(s => t.Text.EndsWith(s));
            if (ignoredByPrefix || ignoredByPostfix)
            {
                ignored.Add(t.Text);
                continue;
            }

            unmatched.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched],
            IgnoredNames = [.. ignored]
        };
    }

    // Aggregator for the post stage (infix-only).
    public VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, VariableNamesOptions options)
    {
        if (options.IgnoreCaptureGroups is { Count: > 0 })
            return CheckVariableNames(infixTokens, options.KnownIdentifierNames, options.IgnoreCaptureGroups);

        if (options.IgnoreIdentifierPattern is not null)
            return CheckVariableNames(infixTokens, options.KnownIdentifierNames, options.IgnoreIdentifierPattern);

        if ((options.IgnorePrefixes is not null && options.IgnorePrefixes.Count > 0) ||
            (options.IgnorePostfixes is not null && options.IgnorePostfixes.Count > 0))
            return CheckVariableNames(
                infixTokens,
                options.KnownIdentifierNames,
                options.IgnorePrefixes ?? [],
                options.IgnorePostfixes ?? []);

        // No ignore rules: strict matching (no ignores)
        return CheckVariableNames(infixTokens, options.KnownIdentifierNames, []);
    }

    #endregion

    #region Unexpected operator/operands

    // Helpers for unexpected operands (fast, allocation-free)
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsLeftOperandOrStart(TokenType type) =>
        type == TokenType.Literal ||
        type == TokenType.Identifier ||
        type == TokenType.ClosedParenthesis;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool RightStartsOperandOrGroup(TokenType type, string text) =>
        type == TokenType.Literal ||
        type == TokenType.Identifier ||
        type == TokenType.Function ||
        type == TokenType.OpenParenthesis ||
        (type == TokenType.OperatorUnary && _prefix.Contains(text)); // a!  or )! or 123!

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsBinaryOperator(TokenType type) => type == TokenType.Operator;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool IsUnaryPrefix(TokenType type, string text) =>
        type == TokenType.OperatorUnary && _prefix.Contains(text);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool IsUnaryPostfix(TokenType type, string text) =>
        type == TokenType.OperatorUnary && _postfix.Contains(text);

    public UnexpectedOperatorOperandsCheckResult CheckUnexpectedOperatorOperands(List<Token> infixTokens)
    {
        var violations = new List<AdjacentOperandsViolation>();

        for (int i = 1; i < infixTokens.Count; i++)
        {
            Token left = infixTokens[i - 1];
            Token right = infixTokens[i];

            // Cache types/text once (avoid repeated property lookups and set/dictionary checks)
            TokenType lt = left.TokenType;
            TokenType rt = right.TokenType;
            string ltxt = left.Text;
            string rtxt = right.Text;
            bool leftStarts = IsLeftOperandOrStart(lt);
            bool rightStarts = RightStartsOperandOrGroup(rt, rtxt);
            bool leftIsOp = IsBinaryOperator(lt);
            bool rightIsOp = IsBinaryOperator(rt);
            bool leftIsPrefix = IsUnaryPrefix(lt, ltxt);
            bool leftIsPostfix = IsUnaryPostfix(lt, ltxt);
            bool rightIsPostfix = IsUnaryPostfix(rt, rtxt);

            bool isInvalid =
                leftStarts && rightStarts ||
                //also check for invalid operator combinations op|op, unaryOp (pre)|op, op|unaryOp (post)
                (leftIsOp && rightIsOp) ||                                           //**
                (leftIsPrefix && rightIsOp) ||                                       //!+
                (leftIsPostfix && rt == TokenType.OpenParenthesis) ||                //%(
                (leftIsOp && rightIsPostfix);                                        //+%

            if (isInvalid)
            {
                violations.Add(new AdjacentOperandsViolation
                {
                    LeftToken = left.Text,
                    LeftPosition = left.Index + 1,
                    RightToken = right.Text,
                    RightPosition = right.Index + 1
                });
            }
        }

        //if (violations.Count > 0)
        //    _logger.LogWarning("Adjacent operands without operator: {pairs}",
        //        string.Join(", ", violations.Select(v => $"{v.LeftPosition}-{v.RightPosition}")));

        return new UnexpectedOperatorOperandsCheckResult { Violations = violations };
    }

    #endregion


    // Function names check moved to TokenizerValidator
    public FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens, IFunctionDescriptors functionDescriptors)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Function))
        {
            string name = t.Text;
            bool known =
                functionDescriptors.GetCustomFunctionFixedArgCount(name).HasValue ||
                functionDescriptors.GetMainFunctionFixedArgCount(name).HasValue ||
                functionDescriptors.GetMainFunctionMinVariableArgCount(name).HasValue ||
                functionDescriptors.GetMainFunctionMinMaxVariableArgCount(name).HasValue ||

                //alternative metadata implementation
                functionDescriptors.IsKnownFunction(name);

            if (known) matched.Add(name);
            else unmatched.Add(name);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched]
        };
    }



    // NEW: Single-pass over infix that collects VariableNames, FunctionNames, and UnexpectedOperatorOperands
    public TokenizerValidationReport ValidateInfixStage(
        List<Token> infixTokens,
        VariableNamesOptions options,
        IFunctionDescriptors? functionDescriptors = null,
        bool earlyReturnOnErrors = false)
    {
        // Variable names accumulation
        var known = options.KnownIdentifierNames;
        HashSet<string> matchedVars = [];
        HashSet<string> unmatchedVars = [];
        HashSet<string> ignoredVars = [];

        // Function names
        IgnoreMode mode = options.IgnoreMode;
        var ignoreGroups = options.IgnoreCaptureGroups ?? [];
        var ignorePattern = options.IgnoreIdentifierPattern;
        var ignorePrefixes = options.IgnorePrefixes is { Count: > 0 } ? options.IgnorePrefixes : [];
        var ignorePostfixes = options.IgnorePostfixes is { Count: > 0 } ? options.IgnorePostfixes : [];
        HashSet<string> ignoreFunctionNames = options.IgnoreFunctionNames is { Count: > 0 } ? options.IgnoreFunctionNames : [];

        HashSet<string> matchedFuncs = [];
        HashSet<string> unmatchedFuncs = [];
        HashSet<string> ignoredFuncs = [];

        bool ignoreFunctions = options.IgnoreFunctions.HasValue && options.IgnoreFunctions.Value;

        // Unexpected operands
        List<AdjacentOperandsViolation> violations = [];

        for (int i = 0; i < infixTokens.Count; i++)
        {
            var t = infixTokens[i];

            #region Variables
            if (t.TokenType == TokenType.Identifier)
            {
                string name = t.Text;
                if (known.Contains(name))
                {
                    matchedVars.Add(name);
                }
                else
                {
                    bool isIgnored = mode switch
                    {
                        IgnoreMode.CaptureGroups => t.CaptureGroup is not null && ignoreGroups.Contains(t.CaptureGroup),
                        IgnoreMode.Pattern => ignorePattern is not null && ignorePattern.IsMatch(name),
                        IgnoreMode.PrefixPostfix =>
                            ignorePrefixes.Count > 0 && ignorePrefixes.Any(p => t.Text.StartsWith(p)) ||
                            ignorePostfixes.Count > 0 && ignorePostfixes.Any(s => t.Text.EndsWith(s)),
                        _ => false
                    };
                    if (isIgnored) ignoredVars.Add(name);
                    else
                    {
                        unmatchedVars.Add(name);

                        if (earlyReturnOnErrors) //we will not return other errors since we are exiting early
                            return new TokenizerValidationReport
                            {
                                VariableNamesResult = new VariableNamesCheckResult
                                {
                                    MatchedNames = [.. matchedVars],
                                    UnmatchedNames = [.. unmatchedVars],
                                    IgnoredNames = [.. ignoredVars]
                                }
                            };
                    }
                }
            }
            #endregion

            #region Function names
            if (functionDescriptors is not null && t.TokenType == TokenType.Function)
            {
                string fname = t.Text;
                bool knownFunc =
                    functionDescriptors.GetCustomFunctionFixedArgCount(fname).HasValue ||
                    functionDescriptors.GetMainFunctionFixedArgCount(fname).HasValue ||
                    functionDescriptors.GetMainFunctionMinVariableArgCount(fname).HasValue ||
                    functionDescriptors.GetMainFunctionMinMaxVariableArgCount(fname).HasValue ||

                    //alternative metadata implementation
                    functionDescriptors.IsKnownFunction(fname);

                if (knownFunc) matchedFuncs.Add(fname);
                else if (ignoreFunctionNames.Contains(fname))
                    ignoredFuncs.Add(fname);
                else
                {
                    unmatchedFuncs.Add(fname);
                    if (earlyReturnOnErrors && !ignoreFunctions) //we will not return other errors since we are exiting early
                        return new TokenizerValidationReport
                        {
                            FunctionNamesResult = new FunctionNamesCheckResult
                            {
                                MatchedNames = [.. matchedFuncs],
                                UnmatchedNames = [.. unmatchedFuncs],
                                IgnoredNames = [.. ignoredFuncs]
                            }
                        };
                }
            }
            #endregion

            #region Unexpected operands

            if (i == 0) continue;
            var left = infixTokens[i - 1];
            var right = t;
            // Cache types/text once (avoid repeated property lookups and set/dictionary checks)
            TokenType lt = left.TokenType;
            TokenType rt = right.TokenType;
            string ltxt = left.Text;
            string rtxt = right.Text;
            bool leftStarts = IsLeftOperandOrStart(lt);
            bool rightStarts = RightStartsOperandOrGroup(rt, rtxt);
            bool leftIsOp = IsBinaryOperator(lt);
            bool rightIsOp = IsBinaryOperator(rt);
            bool leftIsPrefix = IsUnaryPrefix(lt, ltxt);
            bool leftIsPostfix = IsUnaryPostfix(lt, ltxt);
            bool rightIsPostfix = IsUnaryPostfix(rt, rtxt);
            bool isInvalid =
                leftStarts && rightStarts ||
                //also check for invalid operator combinations op|op, unaryOp (pre)|op, op|unaryOp (post)
                (leftIsOp && rightIsOp) ||                                           //**
                (leftIsPrefix && rightIsOp) ||                                       //!+
                (leftIsPostfix && rt == TokenType.OpenParenthesis) ||                //%(
                (leftIsOp && rightIsPostfix);                                        //+%
            if (isInvalid)
            {
                violations.Add(new AdjacentOperandsViolation
                {
                    LeftToken = left.Text,
                    LeftPosition = left.Index + 1,
                    RightToken = right.Text,
                    RightPosition = right.Index + 1
                });

                if (earlyReturnOnErrors) //we will not return other errors since we are exiting early
                    return new TokenizerValidationReport
                    {
                        UnexpectedOperatorOperandsResult = new UnexpectedOperatorOperandsCheckResult
                        {
                            Violations = violations
                        }
                    };
            }

            #endregion

        }

        var variableNamesResult =
            options.IgnoreVariables.HasValue && options.IgnoreVariables.Value
            ? new VariableNamesCheckResult() :
            new VariableNamesCheckResult
            {
                MatchedNames = [.. matchedVars],
                UnmatchedNames = [.. unmatchedVars],
                IgnoredNames = [.. ignoredVars]
            };

        var functionNamesResult =
            ignoreFunctions ? new FunctionNamesCheckResult() :
            new FunctionNamesCheckResult
            {
                MatchedNames = [.. matchedFuncs],
                UnmatchedNames = [.. unmatchedFuncs],
                IgnoredNames = [.. ignoredFuncs]
            };

        var unexpectedResult = new UnexpectedOperatorOperandsCheckResult { Violations = violations };

        return new TokenizerValidationReport
        {
            VariableNamesResult = variableNamesResult,
            FunctionNamesResult = functionNamesResult,
            UnexpectedOperatorOperandsResult = unexpectedResult
        };
    }

}