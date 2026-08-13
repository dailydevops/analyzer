namespace NetEvolve.Analyzer.Helpers;

using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Shared logic for detecting Unicode "Format" category (<see cref="UnicodeCategory.Format"/>) characters —
/// zero-width spaces/joiners, byte-order marks, bidirectional text-direction overrides, and similar — in raw
/// identifier source text. Such characters are legal identifier-part characters per the C# language
/// specification, so an identifier containing one still compiles; per the same specification, <see
/// cref="Microsoft.CodeAnalysis.SyntaxToken.ValueText"/> already has them removed, which is exactly the name
/// NE0014's code fix renames the declaration to. Detection therefore runs against <see
/// cref="Microsoft.CodeAnalysis.SyntaxToken.Text"/> (the raw, un-stripped source text) instead. Used by
/// NE0014's analyzer and code fix.
/// </summary>
internal static class InvisibleCharacters
{
    /// <summary>
    /// Scans <paramref name="text"/> for Unicode "Format" category characters and, when at least one is
    /// found, returns their distinct code points in <paramref name="codePoints"/>, in order of first
    /// appearance.
    /// </summary>
    public static bool TryFind(string text, out ImmutableArray<int> codePoints)
    {
        ImmutableArray<int>.Builder? builder = null;

        var index = 0;
        while (index < text.Length)
        {
            var length =
                char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                    ? 2
                    : 1;

            if (CharUnicodeInfo.GetUnicodeCategory(text, index) == UnicodeCategory.Format)
            {
                var codePoint = char.ConvertToUtf32(text, index);
                builder ??= ImmutableArray.CreateBuilder<int>();
                if (!builder.Contains(codePoint))
                {
                    builder.Add(codePoint);
                }
            }

            index += length;
        }

        codePoints = builder is null ? ImmutableArray<int>.Empty : builder.ToImmutable();
        return !codePoints.IsEmpty;
    }

    /// <summary>Formats code points as a comma-separated <c>U+XXXX</c> list for a diagnostic message.</summary>
    public static string Format(ImmutableArray<int> codePoints) =>
        string.Join(", ", codePoints.Select(codePoint => $"U+{codePoint:X4}"));

    /// <summary>
    /// Removes every Unicode "Format" category character from <paramref name="text"/>. Used by NE0014's
    /// code fix for the trivia case, where — unlike an identifier's <see
    /// cref="Microsoft.CodeAnalysis.SyntaxToken.ValueText"/> — nothing has already computed the clean
    /// result.
    /// </summary>
    public static string Strip(string text)
    {
        var builder = new StringBuilder(text.Length);

        var index = 0;
        while (index < text.Length)
        {
            var length =
                char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                    ? 2
                    : 1;

            if (CharUnicodeInfo.GetUnicodeCategory(text, index) != UnicodeCategory.Format)
            {
                _ = builder.Append(text, index, length);
            }

            index += length;
        }

        return builder.ToString();
    }
}
