namespace NetEvolve.Analyzer.Helpers;

using Microsoft.CodeAnalysis;

/// <summary>
/// Shared logic for NE0012 and NE0013: the canonical, upper-case literal suffix a numeric literal needs for a
/// given converted type, and splitting a literal's raw token text into its digit portion and current suffix.
/// </summary>
internal static class NumericLiteralSuffix
{
    /// <summary>
    /// The canonical suffix required for a numeric literal converted to <paramref name="type"/> — <c>L</c>,
    /// <c>UL</c>, <c>U</c>, <c>F</c>, <c>D</c>, or <c>M</c> — or <see langword="null"/> if <paramref name="type"/>
    /// isn't one of the six numeric types that can be marked with a literal suffix. <see cref="Nullable{T}"/>
    /// (e.g. <c>long?</c>) is unwrapped to its underlying type first.
    /// </summary>
    public static string? RequiredSuffix(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        return type?.SpecialType switch
        {
            SpecialType.System_Int64 => "L",
            SpecialType.System_UInt64 => "UL",
            SpecialType.System_UInt32 => "U",
            SpecialType.System_Single => "F",
            SpecialType.System_Double => "D",
            SpecialType.System_Decimal => "M",
            _ => null,
        };
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a hexadecimal (<c>0x</c>/<c>0X</c>) or binary (<c>0b</c>/<c>0B</c>)
    /// integer literal. Such a literal cannot carry an <c>F</c>/<c>D</c>/<c>M</c> suffix — that format has no
    /// floating-point or decimal representation.
    /// </summary>
    public static bool IsHexOrBinary(string text) =>
        text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B';

    /// <summary>
    /// Splits a numeric literal's raw token text into its digit portion and its current suffix, normalized to
    /// upper case (<c>UL</c> regardless of whether the source wrote <c>ul</c>, <c>UL</c>, <c>lu</c>, or
    /// <c>LU</c>). Returns an empty suffix when the literal carries none.
    /// </summary>
    public static (string Digits, string Suffix) SplitSuffix(string text)
    {
        var end = text.Length;

        // A single f/d/m suffix — never ambiguous with a hex digit, since F/D/M aren't valid integer suffixes
        // and IsHexOrBinary already rules out hex digits reaching here as a false suffix.
        if (!IsHexOrBinary(text) && end > 0 && text[end - 1] is 'f' or 'F' or 'd' or 'D' or 'm' or 'M')
        {
            return (text.Substring(0, end - 1), char.ToUpperInvariant(text[end - 1]).ToString());
        }

        // U/L in either order — neither letter is a valid hex digit, so this is unambiguous for hex/binary too.
        var hasU = false;
        var hasL = false;
        while (end > 0 && text[end - 1] is 'u' or 'U' or 'l' or 'L')
        {
            if (text[end - 1] is 'u' or 'U')
            {
                hasU = true;
            }
            else
            {
                hasL = true;
            }

            end--;
        }

        var suffix = (hasU, hasL) switch
        {
            (true, true) => "UL",
            (true, false) => "U",
            (false, true) => "L",
            _ => "",
        };

        return (text.Substring(0, end), suffix);
    }
}
