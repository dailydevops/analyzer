namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Computes the namespace a file should declare from its location relative to the project directory, anchored
/// at the <c>RootNamespace</c> MSBuild property. When <c>RootNamespace</c> is absent or empty the namespace is
/// composed purely from the folder segments below the project directory. Shared by
/// <c>NamespaceMatchesFolderAnalyzer</c> (NE0002) and the NE0003 nested-namespace flatten fix, so both derive
/// the same folder-anchored value.
/// </summary>
internal static class FolderNamespace
{
    private static readonly char[] PathSeparators = { '/' };

    /// <summary>
    /// Resolves the folder-derived namespace for <paramref name="filePath"/>. Returns <see langword="false"/>
    /// when <c>ProjectDir</c> is missing, the file lives outside the project directory, a folder segment is not
    /// a valid C# identifier, or the file sits in the project root with no <c>RootNamespace</c> anchor — in all
    /// of which cases no reliable mapping exists and the caller should stay silent. <c>RootNamespace</c> is
    /// optional: when it is absent or empty the returned namespace is the folder segments joined on their own.
    /// </summary>
    /// <param name="globalOptions">The global analyzer-config options exposing the build properties.</param>
    /// <param name="filePath">The absolute (or project-relative) path of the source file.</param>
    /// <param name="expected">The folder-derived namespace when the method returns <see langword="true"/>.</param>
    public static bool TryResolve(AnalyzerConfigOptions globalOptions, string filePath, out string expected)
    {
        expected = string.Empty;

        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        if (!TryGetNonEmpty(globalOptions, BuildProperty.ProjectDir, out var projectDir))
        {
            return false;
        }

        // RootNamespace is optional: an absent or empty value means the namespace is composed purely from the
        // folder segments below the project directory.
        _ = globalOptions.TryGetValue(BuildProperty.RootNamespace, out var rawRootNamespace);
        var rootNamespace = rawRootNamespace ?? string.Empty;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            // The file has no directory component, so it maps to the root namespace exactly — but with no
            // RootNamespace anchor there is nothing to compose from, so stay silent.
            return TryUseRootNamespace(rootNamespace, ref expected);
        }

        if (!TryGetRelativeSegments(projectDir, directory!, out var segments))
        {
            return false;
        }

        if (segments.Count == 0)
        {
            // The file sits directly in the project directory: it maps to the root namespace exactly, or stays
            // silent when there is no RootNamespace anchor to compose from.
            return TryUseRootNamespace(rootNamespace, ref expected);
        }

        if (segments.Any(segment => !SyntaxFacts.IsValidIdentifier(segment)))
        {
            return false;
        }

        var folderNamespace = string.Join(".", segments);
        expected = rootNamespace.Length == 0 ? folderNamespace : rootNamespace + "." + folderNamespace;
        return true;
    }

    private static bool TryUseRootNamespace(string rootNamespace, ref string expected)
    {
        if (rootNamespace.Length == 0)
        {
            return false;
        }

        expected = rootNamespace;
        return true;
    }

    private static bool TryGetNonEmpty(AnalyzerConfigOptions options, string key, out string value)
    {
        if (options.TryGetValue(key, out var raw) && !string.IsNullOrEmpty(raw))
        {
            value = raw;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetRelativeSegments(string projectDir, string directory, out List<string> segments)
    {
        segments = new List<string>();

        var root = Normalize(projectDir);
        var target = Normalize(directory);

        if (string.Equals(root, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root + "/";
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            // The file lives outside the project directory: no reliable folder-to-namespace mapping.
            return false;
        }

        segments = target
            .Substring(prefix.Length)
            .Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        return true;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');
}
