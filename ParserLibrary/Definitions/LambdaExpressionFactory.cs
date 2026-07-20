namespace ParserLibrary.Definitions;

public sealed class LambdaExpressionFactory(TokenPatterns tokenPatterns)
{
    private readonly TokenPatterns _tokenPatterns = tokenPatterns ?? throw new ArgumentNullException(nameof(tokenPatterns));

    public bool CaseSensitive => _tokenPatterns.CaseSensitive;

    public LambdaExpression Create(string[] parameters, string body) =>
        LambdaExpression.Create(parameters, body, _tokenPatterns.Comparer);

    public LambdaExpression? TryParse(string lambdaText)
    {
        if (string.IsNullOrWhiteSpace(lambdaText))
            return null;

        var text = lambdaText.Trim();
        var span = text.AsSpan();
        var arrow = _tokenPatterns.LambdaArrow;

        if (string.IsNullOrWhiteSpace(arrow))
            return null;

        var arrowIndex = span.IndexOf(arrow, _tokenPatterns.Comparison);
        if (arrowIndex < 0)
            return null;

        var rawParameters = span[..arrowIndex].ToString().Trim();
        var body = span[(arrowIndex + arrow.Length)..].ToString().Trim();

        string[] parameters = string.IsNullOrWhiteSpace(rawParameters)
            ? []
            : [.. rawParameters
                .Split(_tokenPatterns.ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        return Create(parameters, body);
    }
}
