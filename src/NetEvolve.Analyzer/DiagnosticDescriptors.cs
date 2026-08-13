namespace NetEvolve.Analyzer;

using Microsoft.CodeAnalysis;

/// <summary>
/// Central registry of every <see cref="DiagnosticDescriptor"/> shipped by this package, one field per rule.
/// Descriptors reference their <see cref="DiagnosticIds">identifier</see> and
/// <see cref="DiagnosticCategories">category</see>, so the identifier, category and folder stay in lockstep.
/// Title, message format, and description are sourced from <c>Resources.resx</c>, keyed as
/// <c>{id}_Title</c>, <c>{id}_MessageFormat</c>, and <c>{id}_Description</c>.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>NE0001 — declare one top-level type per file, with a matching file name.</summary>
    public static readonly DiagnosticDescriptor OneTypePerFile = Create(
        DiagnosticIds.NE0001,
        DiagnosticCategories.Maintainability
    );

    /// <summary>NE0002 — the declared namespace should match the folder structure relative to <c>RootNamespace</c>.</summary>
    public static readonly DiagnosticDescriptor NamespaceMatchesFolder = Create(
        DiagnosticIds.NE0002,
        DiagnosticCategories.Maintainability
    );

    /// <summary>NE0003 — a file should declare exactly one namespace.</summary>
    public static readonly DiagnosticDescriptor SingleNamespacePerFile = Create(
        DiagnosticIds.NE0003,
        DiagnosticCategories.Maintainability
    );

    /// <summary>NE0004 — prefer the <c>is null</c> pattern over <c>== null</c>.</summary>
    public static readonly DiagnosticDescriptor UseIsNull = Create(DiagnosticIds.NE0004, DiagnosticCategories.Usage);

    /// <summary>NE0005 — prefer the <c>is not null</c> pattern over <c>!= null</c>.</summary>
    public static readonly DiagnosticDescriptor UseIsNotNull = Create(DiagnosticIds.NE0005, DiagnosticCategories.Usage);

    /// <summary>NE0006 — prefer the <c>is not null</c> pattern over an <c>is object</c> null check.</summary>
    public static readonly DiagnosticDescriptor UseIsNotNullOverIsObject = Create(
        DiagnosticIds.NE0006,
        DiagnosticCategories.Usage
    );

    /// <summary>
    /// NE0007 — prefer <c>&lt;see langword="..."/&gt;</c> over <c>&lt;c&gt;...&lt;/c&gt;</c> or
    /// <c>&lt;code&gt;...&lt;/code&gt;</c> for C# keywords.
    /// </summary>
    public static readonly DiagnosticDescriptor UseLangword = Create(
        DiagnosticIds.NE0007,
        DiagnosticCategories.Documentation
    );

    /// <summary>
    /// NE0008 — prefer <c>&lt;see cref="..."/&gt;</c> over <c>&lt;c&gt;...&lt;/c&gt;</c> or
    /// <c>&lt;code&gt;...&lt;/code&gt;</c> for native type names.
    /// </summary>
    public static readonly DiagnosticDescriptor NativeTypeCref = Create(
        DiagnosticIds.NE0008,
        DiagnosticCategories.Documentation
    );

    /// <summary>
    /// NE0009 — a method with a <see cref="System.Threading.CancellationToken"/> parameter should check for
    /// cancellation at the start of its body.
    /// </summary>
    public static readonly DiagnosticDescriptor RequireCancellationCheck = Create(
        DiagnosticIds.NE0009,
        DiagnosticCategories.Usage
    );

    /// <summary>
    /// NE0010 — a method returning <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>,
    /// <c>ValueTask&lt;T&gt;</c>, or <c>IAsyncEnumerable&lt;T&gt;</c> should accept a
    /// <c>CancellationToken</c> parameter.
    /// </summary>
    public static readonly DiagnosticDescriptor RequireCancellationTokenParameter = Create(
        DiagnosticIds.NE0010,
        DiagnosticCategories.Usage
    );

    /// <summary>
    /// NE0011 — avoid <c>#region</c>/<c>#endregion</c> directives; reported as a warning when nested inside a
    /// member body, and as a suggestion everywhere else.
    /// </summary>
    public static readonly DiagnosticDescriptor AvoidRegionDirectives = Create(
        DiagnosticIds.NE0011,
        DiagnosticCategories.Style
    );

    /// <summary>NE0012 — a numeric literal should carry the canonical, upper-case suffix matching the type it's
    /// converted to.</summary>
    public static readonly DiagnosticDescriptor RequireNumericLiteralSuffix = Create(
        DiagnosticIds.NE0012,
        DiagnosticCategories.Style,
        DiagnosticSeverity.Info
    );

    /// <summary>
    /// NE0013 — a numeric literal immediately cast to a suffixable numeric type should use the literal
    /// suffix instead of the cast.
    /// </summary>
    public static readonly DiagnosticDescriptor UseNumericLiteralSuffixOverCast = Create(
        DiagnosticIds.NE0013,
        DiagnosticCategories.Style,
        DiagnosticSeverity.Info
    );

    /// <summary>
    /// NE0014 — a declared identifier must not contain a non-representable Unicode "Format" category
    /// character, such as a zero-width space/joiner, a byte-order mark, or a bidirectional text-direction
    /// override.
    /// </summary>
    public static readonly DiagnosticDescriptor AvoidInvisibleCharacters = Create(
        DiagnosticIds.NE0014,
        DiagnosticCategories.Naming
    );

    /// <summary>
    /// Builds a <see cref="DiagnosticDescriptor"/> for <paramref name="id"/>. Title, message format, and
    /// description are sourced from <c>Resources.resx</c> (keys <c>{id}_Title</c>,
    /// <c>{id}_MessageFormat</c>, <c>{id}_Description</c>), with its help link built via
    /// <see cref="DiagnosticIds.HelpLink"/>.
    /// </summary>
    /// <param name="id">The diagnostic identifier, e.g. <c>NE0001</c>.</param>
    /// <param name="category">The owning <see cref="DiagnosticCategories">category</see>.</param>
    /// <param name="defaultSeverity">The default severity; defaults to <see cref="DiagnosticSeverity.Warning"/>.</param>
    /// <param name="isEnabledByDefault">Whether the rule is enabled by default; defaults to <see langword="true"/>.</param>
    private static DiagnosticDescriptor Create(
        string id,
        string category,
        DiagnosticSeverity defaultSeverity = DiagnosticSeverity.Warning,
        bool isEnabledByDefault = true
    ) =>
        new(
            id: id,
            title: new LocalizableResourceString($"{id}_Title", Resources.ResourceManager, typeof(Resources)),
            messageFormat: new LocalizableResourceString(
                $"{id}_MessageFormat",
                Resources.ResourceManager,
                typeof(Resources)
            ),
            category: category,
            defaultSeverity: defaultSeverity,
            isEnabledByDefault: isEnabledByDefault,
            description: new LocalizableResourceString(
                $"{id}_Description",
                Resources.ResourceManager,
                typeof(Resources)
            ),
            helpLinkUri: DiagnosticIds.HelpLink(id, category)
        );
}
