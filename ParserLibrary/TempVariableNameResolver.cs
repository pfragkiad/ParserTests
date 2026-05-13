namespace ParserLibrary;

/// <summary>
/// Generates unique temporary variable names for expression compression,
/// guaranteeing no collision with names that already exist in the variable dictionary.
/// </summary>
public interface ITempVariableNameResolver
{
    /// <summary>Prefix used for generated names (e.g. "_T").</summary>
    string Prefix { get; }

    /// <summary>
    /// Returns the next unique temp variable name that is absent from
    /// <paramref name="existingNames"/>.
    /// </summary>
    /// <param name="existingNames">
    /// The current set of names already present in the variable dictionary.
    /// </param>
    string Next(ICollection<string> existingNames);

    /// <summary>
    /// Resets the internal counter.  Call between independent top-level compressions
    /// when you intentionally want to restart numbering (rarely needed).
    /// </summary>
    void Reset();
}

/// <summary>
/// Default implementation of <see cref="ITempVariableNameResolver"/>.
/// Keeps a global counter that never decreases during the lifetime of the instance
/// (typically one DI scope = one HTTP request) so that concurrent lambda and
/// outer-expression compressions never produce the same name.
/// </summary>
public sealed class TempVariableNameResolver : ITempVariableNameResolver
{
    private int _counter;

    public string Prefix { get; }

    /// <param name="prefix">Prefix for generated names, defaults to <c>"_T"</c>.</param>
    public TempVariableNameResolver(string prefix = "_T")
    {
        Prefix = prefix;
    }

    /// <inheritdoc/>
    public string Next(ICollection<string> existingNames)
    {
        string name;
        do
        {
            name = $"{Prefix}{++_counter}";
        }
        while (existingNames.Contains(name, StringComparer.OrdinalIgnoreCase));

        return name;
    }

    /// <inheritdoc/>
    public void Reset() => _counter = 0;
}
