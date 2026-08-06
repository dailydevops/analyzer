namespace NetEvolve.Analyzer;

/// <summary>
/// The standard diagnostic categories used by the .NET platform, mirrored so every
/// <see cref="Microsoft.CodeAnalysis.DiagnosticDescriptor"/> in this package is assigned to a
/// well-known category. The source tree is organized into one folder per category.
/// </summary>
/// <remarks>
/// See <see href="https://learn.microsoft.com/dotnet/fundamentals/code-analysis/categories"/>.
/// </remarks>
internal static class DiagnosticCategories
{
    /// <summary>Rules that support proper library and framework design.</summary>
    public const string Design = nameof(Design);

    /// <summary>Rules related to XML documentation comments.</summary>
    public const string Documentation = nameof(Documentation);

    /// <summary>Rules that ensure correctness in localized applications.</summary>
    public const string Globalization = nameof(Globalization);

    /// <summary>Rules that support interaction with COM clients and native code.</summary>
    public const string Interoperability = nameof(Interoperability);

    /// <summary>Rules related to application maintenance.</summary>
    public const string Maintainability = nameof(Maintainability);

    /// <summary>Rules that enforce naming conventions.</summary>
    public const string Naming = nameof(Naming);

    /// <summary>Rules that help identify performance improvements.</summary>
    public const string Performance = nameof(Performance);

    /// <summary>Rules that flag code that reduces reliability and correctness.</summary>
    public const string Reliability = nameof(Reliability);

    /// <summary>Rules that help identify security vulnerabilities.</summary>
    public const string Security = nameof(Security);

    /// <summary>Rules related to formatting and code style.</summary>
    public const string Style = nameof(Style);

    /// <summary>Rules that flag incorrect use of the .NET platform.</summary>
    public const string Usage = nameof(Usage);
}
