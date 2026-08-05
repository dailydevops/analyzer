namespace NetEvolve.Analyzer.Helpers;

using System;
using System.Collections.Immutable;

/// <summary>
/// The complete set of C# keywords, mirroring
/// <see href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/"/>, split into
/// <see cref="ReservedKeywords"/> (always keywords) and <see cref="ContextualKeywords"/> (keywords only in
/// specific syntactic positions, and otherwise valid identifiers).
/// </summary>
internal static class CSharpKeywords
{
    /// <summary>
    /// Keywords that are reserved everywhere; an identifier with the same spelling requires the <c>@</c>
    /// prefix (<c>@class</c>). Native type names (<c>bool</c>, <c>byte</c>, <c>char</c>, ...) are
    /// intentionally excluded — referencing a type by its bare name in a doc comment is idiomatic and not
    /// the keyword-in-<c>&lt;c&gt;</c> mistake this list exists to catch. <see langword="void"/> is kept, since it
    /// denotes the absence of a return type rather than an actual type, and is commonly referenced the
    /// same way as <see langword="true"/>/<see langword="false"/>/<see langword="null"/>.
    /// </summary>
    public static readonly ImmutableHashSet<string> ReservedKeywords = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "abstract",
        "as",
        "base",
        "break",
        "case",
        "catch",
        "checked",
        "class",
        "const",
        "continue",
        "default",
        "delegate",
        "do",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "interface",
        "internal",
        "is",
        "lock",
        "namespace",
        "new",
        "null",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sealed",
        "sizeof",
        "stackalloc",
        "static",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "unchecked",
        "unsafe",
        "using",
        "virtual",
        "void",
        "volatile",
        "while"
    );

    /// <summary>
    /// Keywords that carry special meaning only in specific syntactic contexts (query clauses, patterns,
    /// declarations); the same spelling is a valid identifier everywhere else. Native type names
    /// (<c>nint</c>, <c>nuint</c>) are intentionally excluded for the same reason as in
    /// <see cref="ReservedKeywords"/>.
    /// </summary>
    public static readonly ImmutableHashSet<string> ContextualKeywords = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "add",
        "alias",
        "and",
        "ascending",
        "args",
        "async",
        "await",
        "by",
        "descending",
        "dynamic",
        "equals",
        "file",
        "from",
        "get",
        "global",
        "group",
        "init",
        "into",
        "join",
        "let",
        "managed",
        "nameof",
        "not",
        "notnull",
        "on",
        "or",
        "orderby",
        "partial",
        "record",
        "remove",
        "required",
        "scoped",
        "select",
        "set",
        "unmanaged",
        "value",
        "var",
        "when",
        "where",
        "with",
        "yield"
    );

    /// <summary>
    /// Native/predefined C# type names, deliberately excluded from <see cref="ReservedKeywords"/> and
    /// <see cref="ContextualKeywords"/>. <see langword="void"/> is intentionally excluded here too — it denotes the
    /// absence of a return type rather than an actual type, and stays classified under
    /// <see cref="ReservedKeywords"/>.
    /// </summary>
    public static readonly ImmutableHashSet<string> NativeTypeKeywords = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "float",
        "int",
        "long",
        "object",
        "sbyte",
        "short",
        "string",
        "uint",
        "ulong",
        "ushort",
        "dynamic",
        "nint",
        "nuint"
    );

    /// <summary>
    /// Common BCL value types that, like <see cref="NativeTypeKeywords"/>, are frequently misdocumented as
    /// <c>&lt;c&gt;type&lt;/c&gt;</c> instead of <c>&lt;see cref="type"/&gt;</c>. These are ordinary types, not
    /// C# keywords — a bare reference resolves via <c>&lt;see cref="..."/&gt;</c> the same way any other type
    /// name does, as long as it is in scope. <c>DateOnly</c>/<c>TimeOnly</c> are deliberately excluded here —
    /// see <see cref="ConditionalBclTypeMinimumVersions"/> — since they only exist on the target frameworks
    /// that ship them.
    /// </summary>
    public static readonly ImmutableHashSet<string> WellKnownBclTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Guid",
        "DateTime",
        "DateTimeOffset",
        "TimeSpan"
    );

    /// <summary>
    /// BCL value types that, unlike <see cref="WellKnownBclTypeNames"/>, are not available on every target
    /// framework the analyzer itself must run on (<c>netstandard2.0</c>) — each key maps to the lowest unified
    /// TFM major version (see <see cref="TargetFrameworkMonikers"/>) that ships it, e.g. <c>DateOnly</c> and
    /// <c>TimeOnly</c> were introduced in .NET 6. A consumer targeting an older framework has no such type to
    /// <c>cref</c> in the first place, so callers must only include a key once the compilation is confirmed to
    /// have the type in scope (e.g. via <c>Compilation.GetTypeByMetadataName</c>), rather than adding it
    /// unconditionally. Different entries are free to name different minimum versions — nothing here assumes
    /// they all shipped together.
    /// </summary>
    public static readonly ImmutableDictionary<string, int> ConditionalBclTypeMinimumVersions = ImmutableDictionary
        .Create<string, int>(StringComparer.Ordinal)
        .Add("DateOnly", 6)
        .Add("TimeOnly", 6);
}
