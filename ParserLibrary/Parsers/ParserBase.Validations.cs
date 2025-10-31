using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;

namespace ParserLibrary.Parsers;

public partial class ParserBase
{

    public FunctionNamesCheckResult CheckFunctionNames(string expression) =>
        CheckFunctionNames(expression, this);

    public UnexpectedOperatorOperandsCheckResult CheckAdjacentOperands(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckUnexpectedOperatorOperands(tokens);
    }

    public FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens)
    {
        HashSet<string> matchedNames = [];
        HashSet<string> unmatchedNames = [];
        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Function))
        {
            if (CustomFunctions.ContainsKey(t.Text) || MainFunctionsWithFixedArgumentsCount.ContainsKey(t.Text))
            { matchedNames.Add(t.Text); continue; }
            unmatchedNames.Add(t.Text);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matchedNames],
            UnmatchedNames = [.. unmatchedNames]
        };
    }

    public List<string> GetMatchedFunctionNames(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return GetMatchedFunctionNames(tokens);
    }

    public List<string> GetMatchedFunctionNames(List<Token> tokens)
    {
        return [.. tokens
            .Where(t => t.TokenType == TokenType.Function &&
                        (CustomFunctions.ContainsKey(t.Text) || MainFunctionsWithFixedArgumentsCount.ContainsKey(t.Text)))
            .Select(t => t.Text)
            .Distinct()];
    }

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckFunctionArgumentsCount(tree.NodeDictionary, (IFunctionDescriptors)this);
    }

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckEmptyFunctionArguments(tree.NodeDictionary);
    }

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckOrphanArgumentSeparators(tree.NodeDictionary);
    }

    public InvalidBinaryOperatorsCheckResult CheckBinaryOperatorOperands(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckBinaryOperatorOperands(tree.NodeDictionary);
    }

    public InvalidUnaryOperatorsCheckResult CheckUnaryOperatorOperands(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckUnaryOperatorOperands(tree.NodeDictionary);
    }

    // Orchestrates two-step validation without doing any tokenization or tree building.
    // - Always pre-validates parentheses via tokenizer. Tokenizer errors are critical.
    // - If matched and inputs are provided, runs parser-level checks against node dictionary.
    public ParserValidationReport Validate(
        string expression,
        VariableNamesOptions nameOptions,
        bool earlyReturnOnErrors = false)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new() { Expression = expression };

        var tokenizerReport = base.Validate(expression, nameOptions, functionDescriptors: this, earlyReturnOnErrors);
        ParserValidationReport report = ParserValidationReport.FromTokenizerReport(tokenizerReport);
        if (!tokenizerReport.IsSuccess)
            //always return after tokenizer errors
            return report;

        List<Token> infixTokens = report.InfixTokens!;
        try
        {
            List<Token> postfixTokens;
            Dictionary<Token, Node<Token>> nodeDictionary;
            try
            {
                postfixTokens = report.PostfixTokens = GetPostfixTokens(infixTokens);
            }
            catch (Exception ex)
            {
                report.Exception = ParserCompileException.PostfixException(ex);
                return report; //on exception we cannot continue because something unexpected has happened
            }

            TokenTree tree;
            try
            {
                tree = report.Tree = GetExpressionTree(postfixTokens!);
            }
            catch (Exception ex)
            {
                report.Exception = ParserCompileException.TreeBuildException(ex);
                return report;
            }

            nodeDictionary = report.NodeDictionary = tree.NodeDictionary;
            var postfixReport = _parserValidator.ValidateTreePostfixStage(
                nodeDictionary,
                nameOptions,
                this,
                earlyReturnOnErrors);
            report.FunctionArgumentsCountResult = postfixReport.FunctionArgumentsCountResult;
            report.EmptyFunctionArgumentsResult = postfixReport.EmptyFunctionArgumentsResult;
            report.OrphanArgumentSeparatorsResult = postfixReport.OrphanArgumentSeparatorsResult;
            report.BinaryOperatorOperandsResult = postfixReport.BinaryOperatorOperandsResult;
            report.UnaryOperatorOperandsResult = postfixReport.UnaryOperatorOperandsResult;
            return report;
        }
        catch (Exception ex) //unexpected parser error
        {
            report.Exception = ParserCompileException.ParserException(ex);
            return report;
        }
    }
}
