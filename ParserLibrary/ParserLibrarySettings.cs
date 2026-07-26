namespace ParserLibrary;

/// <summary>
/// Library-wide runtime settings. Set once at application startup before any parsing occurs.
/// </summary>
public static class ParserLibrarySettings
{
    /// <summary>
    /// When <c>false</c> (default), only the exact delegate variant is called — no cross-delegate fallback.<br/>
    /// - <c>ValidateAndCalc</c>: only <c>Calc</c> is tried.<br/>
    /// - <c>ValidateAndCalcAsync</c>: only <c>CalcAsync</c> is tried.<br/>
    /// Set to <c>true</c> to enable fallback (e.g. during migration or mixed sync/async registration).
    /// </summary>
    public static bool WithCalcFallback { get; set; } = false;

    /// <summary>
    /// When <c>false</c> (default), only <c>AdditionalGlobalValidationAsync</c> is called in async paths — no sync fallback.<br/>
    /// - <c>ValidateArgumentTypesAsync</c> / <c>GetValidSyntaxAsync</c>: sync variant is not substituted.<br/>
    /// Set to <c>true</c> to enable fallback (e.g. when mixing sync-only and async-capable definitions).
    /// </summary>
    public static bool WithValidationFallback { get; set; } = false;
}
