namespace NetEvolve.Analyzer;

using Microsoft.CodeAnalysis;

/// <summary>
/// Central registry of every <see cref="DiagnosticDescriptor"/> shipped by this package, one field per rule.
/// Descriptors reference their <see cref="DiagnosticIds">identifier</see> and
/// <see cref="DiagnosticCategories">category</see>, so the identifier, category and folder stay in lockstep.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>NE0001 — declare one top-level type per file, with a matching file name.</summary>
    public static readonly DiagnosticDescriptor OneTypePerFile = new(
        id: DiagnosticIds.NE0001,
        title: "Declare one type per file with a matching file name",
        messageFormat: "Type '{0}' should be declared in its own file named '{1}.cs'",
        category: DiagnosticCategories.Maintainability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each top-level type should live in its own file whose name matches the type. Generic "
            + "overloads are encoded by arity unless overload grouping is enabled.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0001)
    );

    /// <summary>NE0002 — the declared namespace should match the folder structure relative to <c>RootNamespace</c>.</summary>
    public static readonly DiagnosticDescriptor NamespaceMatchesFolder = new(
        id: DiagnosticIds.NE0002,
        title: "Namespace should match the folder structure",
        messageFormat: "Namespace '{0}' should be '{1}' to match the folder structure",
        category: DiagnosticCategories.Maintainability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Anchored at the RootNamespace MSBuild property, the declared namespace should equal "
            + "RootNamespace joined with the file's folder path relative to the project directory, so the "
            + "physical and logical layout stay aligned.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0002)
    );

    /// <summary>NE0003 — a file should declare exactly one namespace.</summary>
    public static readonly DiagnosticDescriptor SingleNamespacePerFile = new(
        id: DiagnosticIds.NE0003,
        title: "Declare a single namespace per file",
        messageFormat: "Declare exactly one namespace per file",
        category: DiagnosticCategories.Maintainability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A file that declares more than one namespace (sibling or nested) hides types from the "
            + "name-to-location mapping the other organization rules establish. Declare exactly one namespace "
            + "per file.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0003)
    );

    /// <summary>NE0004 — prefer the <c>is null</c> pattern over <c>== null</c>.</summary>
    public static readonly DiagnosticDescriptor UseIsNull = new(
        id: DiagnosticIds.NE0004,
        title: "Use the 'is null' pattern instead of '== null'",
        messageFormat: "Use the 'is null' pattern instead of comparing with '== null'",
        category: DiagnosticCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A null comparison with the equality operator can be redefined by a user-defined "
            + "'operator =='; the 'is null' pattern always performs a null check and cannot be overridden, so it "
            + "expresses the intent unambiguously.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0004)
    );

    /// <summary>NE0005 — prefer the <c>is not null</c> pattern over <c>!= null</c>.</summary>
    public static readonly DiagnosticDescriptor UseIsNotNull = new(
        id: DiagnosticIds.NE0005,
        title: "Use the 'is not null' pattern instead of '!= null'",
        messageFormat: "Use the 'is not null' pattern instead of comparing with '!= null'",
        category: DiagnosticCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A null comparison with the inequality operator can be redefined by a user-defined "
            + "'operator !='; the 'is not null' pattern always performs a null check and cannot be overridden, so "
            + "it expresses the intent unambiguously.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0005)
    );

    /// <summary>NE0006 — prefer the <c>is not null</c> pattern over an <c>is object</c> null check.</summary>
    public static readonly DiagnosticDescriptor UseIsNotNullOverIsObject = new(
        id: DiagnosticIds.NE0006,
        title: "Use the 'is not null' pattern instead of 'is object'",
        messageFormat: "Use the 'is not null' pattern instead of 'is object' for a null check",
        category: DiagnosticCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Using 'is object' as a non-null check is easily misread as a type check; the 'is not null' "
            + "pattern states the null check directly.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0006)
    );

    /// <summary>
    /// NE0007 — prefer <c>&lt;see langword="..."/&gt;</c> over <c>&lt;c&gt;...&lt;/c&gt;</c> or
    /// <c>&lt;code&gt;...&lt;/code&gt;</c> for C# keywords.
    /// </summary>
    public static readonly DiagnosticDescriptor UseLangword = new(
        id: DiagnosticIds.NE0007,
        title: "Use <see langword=\"...\"/> instead of <c>...</c> or <code>...</code> for C# keywords",
        messageFormat: "Use <see langword=\"{0}\"/> instead of <{1}>{0}</{1}>",
        category: DiagnosticCategories.Documentation,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A <c> or <code> element whose entire content is a single C# keyword (e.g. 'true', "
            + "'false', 'null') carries no semantic meaning; <see langword=\"...\"/> is the dedicated construct "
            + "for referencing a language keyword and is what IntelliSense and documentation generators "
            + "recognize as such.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0007)
    );

    /// <summary>
    /// NE0008 — prefer <c>&lt;see cref="..."/&gt;</c> over <c>&lt;c&gt;...&lt;/c&gt;</c> or
    /// <c>&lt;code&gt;...&lt;/code&gt;</c> for native type names.
    /// </summary>
    public static readonly DiagnosticDescriptor NativeTypeCref = new(
        id: DiagnosticIds.NE0008,
        title: "Use <see cref=\"...\"/> instead of <c>...</c> or <code>...</code> for native type names",
        messageFormat: "Use <see cref=\"{0}\"/> instead of <{1}>{0}</{1}>",
        category: DiagnosticCategories.Documentation,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A <c> or <code> element whose entire content is a single native type name (e.g. "
            + "'string', 'int') carries no semantic meaning; <see cref=\"...\"/> is the dedicated construct "
            + "for referencing a type and is what IntelliSense and documentation generators recognize as "
            + "such.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0008)
    );

    /// <summary>
    /// NE0009 — a method with a <see cref="System.Threading.CancellationToken"/> parameter should check for
    /// cancellation at the start of its body.
    /// </summary>
    public static readonly DiagnosticDescriptor RequireCancellationCheck = new(
        id: DiagnosticIds.NE0009,
        title: "Check for cancellation at the start of the method body",
        messageFormat: "Method '{0}' has a CancellationToken parameter but does not check for cancellation at "
            + "the start of its body",
        category: DiagnosticCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A method accepting a CancellationToken should honor it as early as possible. As the "
            + "first statement after any leading argument-validation guard clauses, call "
            + "'token.ThrowIfCancellationRequested()' or check 'token.IsCancellationRequested' and return, so "
            + "cancellation is observed before any other work runs.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0009)
    );
}
