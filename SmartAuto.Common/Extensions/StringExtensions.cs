namespace SmartAuto.Common.Extensions;

public static class StringExtensions
{
    /// <summary>Returns true when the string is null or whitespace.</summary>
    public static bool IsNullOrWhiteSpace(this string? s) => string.IsNullOrWhiteSpace(s);

    /// <summary>Masks sensitive content for logging.</summary>
    public static string MaskSensitive(this string input) =>
        string.IsNullOrEmpty(input) ? input : "<MASKED>";

    /// <summary>Interpolate {{VarName}} placeholders with dictionary values.</summary>
    public static string InterpolateVariables(this string template, IReadOnlyDictionary<string, string> vars)
    {
        var result = template;
        foreach (var (key, value) in vars)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return result;
    }
}
