namespace NetEvolve.Analyzer;

/// <summary>
/// The standard diagnostic categories used by the .NET platform, mirrored so every
/// <see cref="Microsoft.CodeAnalysis.DiagnosticDescriptor"/> in this package is assigned to a
/// well-known category. The source tree is organized into one folder per category.
/// </summary>
/// <remarks>
/// See <see href="https://learn.microsoft.com/dotnet/fundamentals/code-analysis/categories"/>.
/// </remarks>
internal enum DiagnosticCategories
{
    /// <summary>Rules that support proper library and framework design.</summary>
    Design,

    /// <summary>Rules related to XML documentation comments.</summary>
    Documentation,

    /// <summary>Rules that ensure correctness in localized applications.</summary>
    Globalization,

    /// <summary>Rules that support interaction with COM clients and native code.</summary>
    Interoperability,

    /// <summary>Rules related to application maintenance.</summary>
    Maintainability,

    /// <summary>Rules that enforce naming conventions.</summary>
    Naming,

    /// <summary>Rules that help identify performance improvements.</summary>
    Performance,

    /// <summary>Rules that flag code that reduces reliability and correctness.</summary>
    Reliability,

    /// <summary>Rules that help identify security vulnerabilities.</summary>
    Security,

    /// <summary>Rules related to formatting and code style.</summary>
    Style,

    /// <summary>Rules that flag incorrect use of the .NET platform.</summary>
    Usage,
}
