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
    /// the keyword-in-<c>&lt;c&gt;</c> mistake this list exists to catch. <c>void</c> is kept, since it
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
    /// <see cref="ContextualKeywords"/>. <c>void</c> is intentionally excluded here too — it denotes the
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
    /// name does, as long as it is in scope.
    /// </summary>
    public static readonly ImmutableHashSet<string> WellKnownBclTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Guid",
        "DateTime",
        "DateTimeOffset",
        "DateOnly",
        "TimeOnly",
        "TimeSpan"
    );
}
