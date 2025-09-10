using Microsoft.Extensions.Logging;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers.Validation;

public sealed class ParserValidator : IParserValidator
{
    private readonly ILogger<ParserValidator> _logger;
    private readonly ITokenizerValidator _tokValidator;
    private readonly TokenPatterns _patterns;

    public ParserValidator(ILogger<ParserValidator> logger, ITokenizerValidator tokenizerValidator, TokenPatterns patterns)
    {
        _logger = logger;
        _tokValidator = tokenizerValidator;
        _patterns = patterns;
    }

    // Orchestrates two-step validation without doing any tokenization or tree building.
    // - Always pre-validates parentheses via tokenizer validator (string-only).
    // - If matched and inputs are provided, runs parser-level checks against infix and/or node dictionary.
    public ParserValidationReport Validate(
        string expression,
        List<Token>? infixTokens = null,
        TokenTree? tree = null,
        IParserFunctionMetadata? metadata = null,
        bool stopAtTokenizerErrors = true)
    {
        var ok = _tokValidator.PreValidateParentheses(expression, out var parenDetail);

        var report = new ParserValidationReport
        {
            Expression = expression,
            ParenthesesMatched = ok,
            ParenthesesDetail = ok ? null : parenDetail
        };

        if (!ok && stopAtTokenizerErrors)
            return report;

        // Function name checks (require infixTokens + metadata)
        if (ok && infixTokens is not null && metadata is not null)
        {
            var fn = CheckFunctionNames(infixTokens, metadata);
            report.FunctionNames = fn;
            if (!fn.IsSuccess) _logger.LogWarning("Unmatched function names in formula: {expr}", expression);
        }

        // Node-dictionary-based checks (require a built tree)
        var nodeDict = tree?.NodeDictionary;
        if (nodeDict is not null)
        {
            var ops = CheckOperators(nodeDict);
            report.Operators = ops;
            if (!ops.IsSuccess) _logger.LogWarning("Invalid operators in formula: {expr}", expression);

            var seps = CheckOrphanArgumentSeparators(nodeDict);
            report.ArgumentSeparators = seps;
            if (!seps.IsSuccess) _logger.LogWarning("Invalid argument separators in formula: {expr}", expression);

            if (metadata is not null)
            {
                var argcnt = CheckFunctionArgumentsCount(nodeDict, metadata, _patterns);
                report.FunctionArgumentsCount = argcnt;
                if (!argcnt.IsSuccess) _logger.LogWarning("Unmatched function arguments in formula: {expr}", expression);
            }

            var empty = CheckEmptyFunctionArguments(nodeDict, _patterns);
            report.EmptyFunctionArguments = empty;
            if (!empty.IsSuccess) _logger.LogWarning("Empty function arguments in formula: {expr}", expression);
        }

        return report;
    }

    // ---- Granular checks (no tokenization / no tree building) ----

    public FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens, IParserFunctionMetadata metadata)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Function))
        {
            // Consider a function "known" if metadata can provide either a fixed count or a min variable count
            bool known =
                metadata.GetCustomFunctionFixedArgCount(t.Text) is not null ||
                metadata.GetMainFunctionFixedArgCount(t.Text) is not null ||
                metadata.GetMainFunctionMinVariableArgCount(t.Text) is not null;

            if (known) matched.Add(t.Text); else unmatched.Add(t.Text);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched]
        };
    }

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(
        Dictionary<Token, Node<Token>> nodeDictionary,
        TokenPatterns patterns)
    {
        List<FunctionArgumentCheckResult> valid = [];
        List<FunctionArgumentCheckResult> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            if (token.TokenType != TokenType.Function) continue;

            var node = entry.Value;
            var args = node.GetFunctionArgumentNodes(patterns.ArgumentSeparator);
            var res = new FunctionArgumentCheckResult { FunctionName = token.Text, Position = token.Index + 1 };

            if (args.Any(n => n.Value!.IsNull)) invalid.Add(res); else valid.Add(res);
        }

        return new EmptyFunctionArgumentsCheckResult { ValidFunctions = [.. valid], InvalidFunctions = [.. invalid] };
    }
   
    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(
        Dictionary<Token, Node<Token>> nodeDictionary,
        IParserFunctionMetadata metadata,
        TokenPatterns patterns)
    {
        HashSet<FunctionArgumentCheckResult> valid = [];
        HashSet<FunctionArgumentCheckResult> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var node = entry.Value;
            if (node.Value!.TokenType != TokenType.Function) continue;

            string name = node.Value.Text;
            int actual = node.GetFunctionArgumentsCount(patterns.ArgumentSeparator.ToString());

            var fixedCount = metadata.GetCustomFunctionFixedArgCount(name) ??
                             metadata.GetMainFunctionFixedArgCount(name);

            if (fixedCount is not null)
            {
                var r = new FunctionArgumentCheckResult
                {
                    FunctionName = name,
                    Position = node.Value.Index + 1,
                    ActualArgumentsCount = actual,
                    ExpectedArgumentsCount = fixedCount.Value
                };
                if (actual != fixedCount.Value) invalid.Add(r); else valid.Add(r);
                continue;
            }

            var minVar = metadata.GetMainFunctionMinVariableArgCount(name);
            if (minVar is not null)
            {
                var r = new FunctionArgumentCheckResult
                {
                    FunctionName = name,
                    Position = node.Value.Index + 1,
                    ActualArgumentsCount = actual,
                    MinExpectedArgumentsCount = minVar.Value
                };
                if (actual < minVar.Value) invalid.Add(r); else valid.Add(r);
                continue;
            }

            invalid.Add(new FunctionArgumentCheckResult
            {
                FunctionName = name,
                Position = node.Value.Index + 1,
                ActualArgumentsCount = actual,
                ExpectedArgumentsCount = 0
            });
        }

        return new FunctionArgumentsCountCheckResult { ValidFunctions = [.. valid], InvalidFunctions = [.. invalid] };
    }

    public InvalidOperatorsCheckResult CheckOperators(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<OperatorArgumentCheckResult> valid = [];
        List<OperatorArgumentCheckResult> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            if (token.TokenType != TokenType.Operator) continue;

            var node = entry.Value;
            var (l, r) = node.GetBinaryArgumentNodes();
            var check = new OperatorArgumentCheckResult { Operator = token.Text, Position = token.Index + 1 };
            if (l.Value!.IsNull || r.Value!.IsNull) invalid.Add(check); else valid.Add(check);
        }

        return new InvalidOperatorsCheckResult { ValidOperators = [.. valid], InvalidOperators = [.. invalid] };
    }

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<int> valid = [];
        List<int> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            if (token.TokenType != TokenType.ArgumentSeparator) continue;

            var node = entry.Value;
            bool parentFound = false;

            foreach (var p in nodeDictionary)
            {
                var pn = p.Value;
                if ((pn.Left == node || pn.Right == node) &&
                    (p.Key.TokenType == TokenType.Function || p.Key.TokenType == TokenType.ArgumentSeparator))
                {
                    valid.Add(token.Index + 1);
                    parentFound = true;
                    break;
                }
            }
            if (!parentFound) invalid.Add(token.Index + 1);
        }

        return new InvalidArgumentSeparatorsCheckResult { ValidPositions = [.. valid], InvalidPositions = [.. invalid] };
    }

}