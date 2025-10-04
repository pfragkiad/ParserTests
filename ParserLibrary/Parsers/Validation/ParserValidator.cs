using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;

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
                             metadata.GetMainFunctionFixedArgCount(name) ??
                             //new interface method to get fixed count when functioninformation data is available
                             metadata.GetFunctionFixedArgCount(name);

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

            var minCount = metadata.GetMainFunctionMinVariableArgCount(name);
            if (minCount is not null)
            {
                var r = new FunctionArguments
                {
                    FunctionName = name,
                    Position = node.Value.Index + 1,
                    ActualArgumentsCount = actual,
                    MinExpectedArgumentsCount = minCount.Value
                };
                if (actual < minCount.Value) invalid.Add(r); else valid.Add(r);
                continue;
            }

            var minMaxCount = metadata.GetMainFunctionMinMaxVariableArgCount(name)
                //new implementation to get min-max count when functioninformation data is available
                ?? metadata.GetFunctionMinMaxVariableArgCount(name);
            if (minMaxCount is not null)
            {
                var r = new FunctionArguments
                {
                    FunctionName = name,
                    Position = node.Value.Index + 1,
                    ActualArgumentsCount = actual,
                    MinExpectedArgumentsCount = minMaxCount.Value.Item1,
                    MaxExpectedArgumentsCount = minMaxCount.Value.Item2
                };
                if (actual < minMaxCount.Value.Item1 || actual > minMaxCount.Value.Item2)
                    invalid.Add(r);
                else
                    valid.Add(r);
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

        return new InvalidArgumentSeparatorsCheckResult
        {
            ValidPositions = [.. valid],
            InvalidPositions = [.. invalid]
        };
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


    // NEW: Unified single-pass validator for the Postfix/Tree stage
    public ParserValidationReport ValidateTreePostfixStage(
        Dictionary<Token, Node<Token>> nodeDictionary,
        IFunctionDescriptors? functionDescriptors = null,
        bool earlyReturnOnErrors = false)
    {
        // Function argument count
        HashSet<FunctionArguments> funcCountValid = [];
        HashSet<FunctionArguments> funcCountInvalid = [];

        // Empty function arguments
        List<FunctionArguments> emptyArgsValid = [];
        List<FunctionArguments> emptyArgsInvalid = [];

        // Binary operators
        List<OperatorArgumentCheckResult> binValid = [];
        List<OperatorArgumentCheckResult> binInvalid = [];

        // Unary operators
        List<OperatorArgumentCheckResult> unValid = [];
        List<OperatorArgumentCheckResult> unInvalid = [];

        // Argument separators: collect all and which have valid parents
        var separatorNodes = new HashSet<Node<Token>>();
        var separatorsWithValidParents = new HashSet<Node<Token>>();
        var allSeparators = new List<(Node<Token> Node, Token Tok)>();

        foreach (var entry in nodeDictionary)
        {
            Token token = entry.Key;
            Node<Token> node = entry.Value;

            // Track all separators (positions reported after we collect parents)
            if (token.TokenType == TokenType.ArgumentSeparator)
            {
                var n = (Node<Token>)node;
                separatorNodes.Add(n);
                allSeparators.Add((n, token));
            }

            // As a parent, mark children that are argument separators with valid parent type
            bool parentCanOwnSep = token.TokenType == TokenType.Function || token.TokenType == TokenType.ArgumentSeparator;
            if (parentCanOwnSep)
            {
                if (node.Left is Node<Token> ln && separatorNodes.Contains(ln))
                    separatorsWithValidParents.Add(ln);
                if (node.Right is Node<Token> rn && separatorNodes.Contains(rn))
                    separatorsWithValidParents.Add(rn);
            }

            // Function-based checks
            if (token.TokenType == TokenType.Function)
            {
                // Empty-args
                Node<Token>[] args = node.GetFunctionArgumentNodes();
                var res = new FunctionArguments { FunctionName = token.Text, Position = token.Index + 1 };
                if (args.Any(n => n.Value!.IsNull)) emptyArgsInvalid.Add(res); else emptyArgsValid.Add(res);

                if (earlyReturnOnErrors && emptyArgsInvalid.Count > 0)
                {
                    return new ParserValidationReport
                    {
                        EmptyFunctionArgumentsResult = new EmptyFunctionArgumentsCheckResult
                        {
                            ValidFunctions = [.. emptyArgsValid],
                            InvalidFunctions = [.. emptyArgsInvalid]
                        }
                    };
                }

                // Count check (only if metadata provided)
                if (functionDescriptors is not null)
                {
                    string name = token.Text;
                    //int actual = ((Node<Token>)node).GetFunctionArgumentsCount();
                    int actual = args.Length;

                    int? fixedCount =
                        functionDescriptors.GetCustomFunctionFixedArgCount(name) ??
                        functionDescriptors.GetMainFunctionFixedArgCount(name) ??
                        //new interface method to get fixed count when functioninformation data is available
                        functionDescriptors.GetFunctionFixedArgCount(name);

                    int? minCount = functionDescriptors.GetMainFunctionMinVariableArgCount(name);
                    (int, int)? minMaxCount = functionDescriptors.GetMainFunctionMinMaxVariableArgCount(name);

                    if (fixedCount is not null)
                    {
                        var r = new FunctionArguments
                        {
                            FunctionName = name,
                            Position = token.Index + 1,
                            ActualArgumentsCount = actual,
                            ExpectedArgumentsCount = fixedCount.Value
                        };
                        if (actual != fixedCount.Value) funcCountInvalid.Add(r); else funcCountValid.Add(r);
                    }
                    else if (minCount is not null)
                    {
                        var r = new FunctionArguments
                        {
                            FunctionName = name,
                            Position = token.Index + 1,
                            ActualArgumentsCount = actual,
                            MinExpectedArgumentsCount = minCount.Value
                        };
                        if (actual < minCount.Value) funcCountInvalid.Add(r); else funcCountValid.Add(r);
                    }
                    else if (minMaxCount is not null)
                    {
                        var r = new FunctionArguments
                        {
                            FunctionName = name,
                            Position = token.Index + 1,
                            ActualArgumentsCount = actual,
                            MinExpectedArgumentsCount = minMaxCount.Value.Item1,
                            MaxExpectedArgumentsCount = minMaxCount.Value.Item2
                        };
                        if (actual < minMaxCount.Value.Item1 || actual > minMaxCount.Value.Item2)
                            funcCountInvalid.Add(r);
                        else
                            funcCountValid.Add(r);
                    }
                    else
                    {
                        funcCountInvalid.Add(new FunctionArguments
                        {
                            FunctionName = name,
                            Position = token.Index + 1,
                            ActualArgumentsCount = actual,
                            ExpectedArgumentsCount = 0
                        });
                    }
                }

                if (earlyReturnOnErrors && funcCountInvalid.Count > 0)
                {
                    return new ParserValidationReport
                    {
                        FunctionArgumentsCountResult = new FunctionArgumentsCountCheckResult
                        {
                            ValidFunctions = [.. funcCountValid],
                            InvalidFunctions = [.. funcCountInvalid]
                        }
                    };
                }
            }


            // Binary operators
            if (token.TokenType == TokenType.Operator)
            {
                var (l, r) = ((Node<Token>)node).GetBinaryArgumentNodes();
                var check = new OperatorArgumentCheckResult { Operator = token.Text, Position = token.Index + 1 };
                if (l.Value!.IsNull || r.Value!.IsNull) binInvalid.Add(check); else binValid.Add(check);

                if (earlyReturnOnErrors && binInvalid.Count > 0)
                {
                    return new ParserValidationReport
                    {
                        BinaryOperatorOperandsResult = new InvalidBinaryOperatorsCheckResult
                        {
                            ValidOperators = [.. binValid],
                            InvalidOperators = [.. binInvalid]
                        }
                    };
                }
            }

            // Unary operators
            if (token.TokenType == TokenType.OperatorUnary)
            {
                var isPrefix = _patterns.UnaryOperatorDictionary[token.Text].Prefix;
                var operandNode = ((Node<Token>)node).GetUnaryArgumentNode(isPrefix);

                var check = new OperatorArgumentCheckResult
                {
                    Operator = token.Text,
                    Position = token.Index + 1
                };

                if (operandNode.Value!.IsNull) unInvalid.Add(check); else unValid.Add(check);

                if (earlyReturnOnErrors && unInvalid.Count > 0)
                {
                    return new ParserValidationReport
                    {
                        UnaryOperatorOperandsResult = new InvalidUnaryOperatorsCheckResult
                        {
                            ValidOperators = [.. unValid],
                            InvalidOperators = [.. unInvalid]
                        }
                    };
                }
            }
        }

        // Finalize argument separators (need full pass to know parents)
        var validPos = new List<int>(allSeparators.Count);
        var invalidPos = new List<int>();
        foreach (var (n, tok) in allSeparators)
        {
            if (separatorsWithValidParents.Contains(n)) validPos.Add(tok.Index + 1);
            else invalidPos.Add(tok.Index + 1);
        }

        if (earlyReturnOnErrors && invalidPos.Count > 0)
        {
            return new ParserValidationReport
            {
                OrphanArgumentSeparatorsResult = new InvalidArgumentSeparatorsCheckResult
                {
                    ValidPositions = validPos,
                    InvalidPositions = invalidPos
                }
            };
        }

        // Build all results
        FunctionArgumentsCountCheckResult? funcCountResult =
            functionDescriptors is null ? null :
            new FunctionArgumentsCountCheckResult
            {
                ValidFunctions = [.. funcCountValid],
                InvalidFunctions = [.. funcCountInvalid]
            };

        var emptyArgsResult = new EmptyFunctionArgumentsCheckResult
        {
            ValidFunctions = [.. emptyArgsValid],
            InvalidFunctions = [.. emptyArgsInvalid]
        };

        var binOpsResult = new InvalidBinaryOperatorsCheckResult
        {
            ValidOperators = [.. binValid],
            InvalidOperators = [.. binInvalid]
        };

        var unOpsResult = new InvalidUnaryOperatorsCheckResult
        {
            ValidOperators = [.. unValid],
            InvalidOperators = [.. unInvalid]
        };

        var sepResult = new InvalidArgumentSeparatorsCheckResult
        {
            ValidPositions = validPos,
            InvalidPositions = invalidPos
        };

        return new ParserValidationReport
        {
            FunctionArgumentsCountResult = funcCountResult,
            EmptyFunctionArgumentsResult = emptyArgsResult,
            BinaryOperatorOperandsResult = binOpsResult,
            UnaryOperatorOperandsResult = unOpsResult,
            OrphanArgumentSeparatorsResult = sepResult
        };
    }
}