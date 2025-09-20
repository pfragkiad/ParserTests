using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class ParserValidator : IParserValidator
{
    private readonly ILogger<ParserValidator> _logger;
    private readonly TokenPatterns _patterns;

    public ParserValidator(
        ILogger<ParserValidator> logger,
        TokenPatterns patterns) 
    {
        _logger = logger;
        _patterns = patterns;
    }

    // ---- Granular checks (no tokenization / no tree building) ----

    public FunctionNamesCheckResult CheckFunctionNames(
        List<Token> infixTokens,
        IFunctionDescriptors functionDescriptors)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Function))
        {
            // Consider a function "known" if metadata can provide either a fixed count or a min variable count
            bool known =
                functionDescriptors.GetCustomFunctionFixedArgCount(t.Text) is not null ||
                functionDescriptors.GetMainFunctionFixedArgCount(t.Text) is not null ||
                functionDescriptors.GetMainFunctionMinVariableArgCount(t.Text) is not null;

            if (known) matched.Add(t.Text); else unmatched.Add(t.Text);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched]
        };
    }

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(
        Dictionary<Token, Node<Token>> nodeDictionary,
        IFunctionDescriptors metadata)
    {
        HashSet<FunctionArguments> valid = [];
        HashSet<FunctionArguments> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var node = entry.Value;
            if (node.Value!.TokenType != TokenType.Function) continue;

            string name = node.Value.Text;
            int actual = ((Node<Token>)node).GetFunctionArgumentsCount();

            var fixedCount = metadata.GetCustomFunctionFixedArgCount(name) ??
                             metadata.GetMainFunctionFixedArgCount(name);

            if (fixedCount is not null)
            {
                var r = new FunctionArguments
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
                var r = new FunctionArguments
                {
                    FunctionName = name,
                    Position = node.Value.Index + 1,
                    ActualArgumentsCount = actual,
                    MinExpectedArgumentsCount = minVar.Value
                };
                if (actual < minVar.Value) invalid.Add(r); else valid.Add(r);
                continue;
            }

            invalid.Add(new FunctionArguments
            {
                FunctionName = name,
                Position = node.Value.Index + 1,
                ActualArgumentsCount = actual,
                ExpectedArgumentsCount = 0
            });
        }

        return new FunctionArgumentsCountCheckResult { ValidFunctions = [.. valid], InvalidFunctions = [.. invalid] };
    }

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(
        Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<FunctionArguments> valid = [];
        List<FunctionArguments> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            if (token.TokenType != TokenType.Function) continue;

            var node = (Node<Token>)entry.Value;
            var args = node.GetFunctionArgumentNodes();
            var res = new FunctionArguments { FunctionName = token.Text, Position = token.Index + 1 };

            if (args.Any(n => n.Value!.IsNull)) invalid.Add(res); else valid.Add(res);
        }

        return new EmptyFunctionArgumentsCheckResult { ValidFunctions = [.. valid], InvalidFunctions = [.. invalid] };
    }

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(
        Dictionary<Token, Node<Token>> nodeDictionary)
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

        return new InvalidArgumentSeparatorsCheckResult {
            ValidPositions = [.. valid], InvalidPositions = [.. invalid] };
    }

    public InvalidBinaryOperatorsCheckResult CheckBinaryOperatorOperands(
        Dictionary<Token, Node<Token>> nodeDictionary)
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

        return new InvalidBinaryOperatorsCheckResult
        {
            ValidOperators = [.. valid],
            InvalidOperators = [.. invalid]
        };
    }

    // NEW: unary operator operand validation
    public InvalidUnaryOperatorsCheckResult CheckUnaryOperatorOperands(
        Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<OperatorArgumentCheckResult> valid = [];
        List<OperatorArgumentCheckResult> invalid = [];

        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            if (token.TokenType != TokenType.OperatorUnary) continue;

            var node = entry.Value;
            var isPrefix = _patterns.UnaryOperatorDictionary[token.Text].Prefix;
            var operandNode = ((Node<Token>)node).GetUnaryArgumentNode(isPrefix);

            var check = new OperatorArgumentCheckResult
            {
                Operator = token.Text,
                Position = token.Index + 1
            };

            if (operandNode.Value!.IsNull) invalid.Add(check); else valid.Add(check);
        }

        return new InvalidUnaryOperatorsCheckResult
        {
            ValidOperators = [.. valid],
            InvalidOperators = [.. invalid]
        };
    }
}