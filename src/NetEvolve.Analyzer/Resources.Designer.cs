namespace NetEvolve.Analyzer;

using System.Resources;

/// <summary>
/// Strongly-typed access to the <see cref="ResourceManager"/> backing this assembly's embedded
/// <c>Resources.resx</c>, which holds the localizable title, message format, and description strings for
/// every diagnostic, keyed as <c>{DiagnosticId}_Title</c>, <c>{DiagnosticId}_MessageFormat</c>, and
/// <c>{DiagnosticId}_Description</c>.
/// </summary>
internal static class Resources
{
    private static ResourceManager resourceManager;

    /// <summary>The <see cref="ResourceManager"/> backing this assembly's embedded <c>Resources.resx</c>.</summary>
    internal static ResourceManager ResourceManager
    {
        get
        {
            if (resourceManager is null)
            {
                resourceManager = new ResourceManager("NetEvolve.Analyzer.Resources", typeof(Resources).Assembly);
            }

            return resourceManager;
        }
    }
}
