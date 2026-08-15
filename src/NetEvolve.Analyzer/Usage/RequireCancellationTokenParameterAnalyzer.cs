namespace NetEvolve.Analyzer.Usage;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0010 — reports a method whose return type is <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>,
/// <c>ValueTask&lt;T&gt;</c>, or <c>IAsyncEnumerable&lt;T&gt;</c> and that declares no
/// <see cref="System.Threading.CancellationToken"/> parameter. An <see langword="override"/> and an explicit
/// interface implementation are left alone because their signature is fixed by the member they override or
/// implement; a method that implicitly implements an interface member is left alone for the same reason. An
/// interface's own method declaration is still flagged, since it is the contract other members are
/// constrained by. Partial methods are reported only once, on the implementing declaration. The
/// compilation's entry point (<c>Main</c>, including top-level statements' synthesized one) is left alone
/// too, since its signature is dictated by the CLR/host, not by this codebase.
///
/// A method that has a body is only flagged when that body contains at least one call site a
/// <see cref="System.Threading.CancellationToken"/> could actually be passed to (see
/// <see cref="CancellationTokenCallSites"/>); otherwise the parameter the code fix would add could never
/// be used, leaving nothing but an unused-parameter warning in its place. A call that already has a genuine
/// token passed straight into it (e.g. an ambient <c>SomeContext.Current.CancellationToken</c>) is not such a
/// call site — it already has working cancellation support. A hardcoded <c>CancellationToken.None</c> is the
/// exception: it signals "no cancellation" rather than a real token, so it still counts as evidence the method
/// needs its own parameter. A method without a body — abstract, interface, or extern — has no such body to
/// leave with an unused parameter, so it is always flagged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequireCancellationTokenParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RequireCancellationTokenParameter);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(AnalyzeCompilationStart);
    }

    private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;

        var cancellationTokenType = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        if (cancellationTokenType is null)
        {
            // Without the CancellationToken type in scope there is nothing to require.
            return;
        }

        var wellKnownReturnTypes = new WellKnownReturnTypes(
            compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"),
            compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1")
        );

        // A method with no body (abstract, interface, extern) is reported straight from the symbol action; a
        // method with a body is only reported once the code block action below has confirmed the body actually
        // has somewhere to use the token — that action supplies a SemanticModel without this one having to call
        // Compilation.GetSemanticModel() itself (see RS1030).
        // The compilation's entry point (e.g. `Main`) has a signature dictated by the CLR/host, not by this
        // codebase, so it's excluded the same way an override or explicit interface implementation is.
        var entryPoint = compilation.GetEntryPoint(context.CancellationToken);

        context.RegisterSymbolAction(
            symbolContext =>
                AnalyzeMethodSymbol(symbolContext, cancellationTokenType, wellKnownReturnTypes, entryPoint),
            SymbolKind.Method
        );
        context.RegisterCodeBlockAction(codeBlockContext =>
            AnalyzeMethodBody(codeBlockContext, cancellationTokenType, wellKnownReturnTypes, entryPoint)
        );
    }

    private static void AnalyzeMethodSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol cancellationTokenType,
        WellKnownReturnTypes wellKnownReturnTypes,
        IMethodSymbol? entryPoint
    )
    {
        var method = (IMethodSymbol)context.Symbol;
        if (
            SymbolEqualityComparer.Default.Equals(method, entryPoint)
            || !IsCandidate(method, cancellationTokenType, wellKnownReturnTypes, out var returnTypeDescription)
        )
        {
            return;
        }

        var declaration = GetDeclaration(method, context.CancellationToken);
        if (declaration is { Body: not null } or { ExpressionBody: not null })
        {
            // Has a body; AnalyzeMethodBody reports it, once it has confirmed the body can use the token.
            return;
        }

        Report(context.ReportDiagnostic, method, returnTypeDescription!);
    }

    private static void AnalyzeMethodBody(
        CodeBlockAnalysisContext context,
        INamedTypeSymbol cancellationTokenType,
        WellKnownReturnTypes wellKnownReturnTypes,
        IMethodSymbol? entryPoint
    )
    {
        if (
            context.OwningSymbol is not IMethodSymbol method
            || context.CodeBlock is not MethodDeclarationSyntax declaration
        )
        {
            return;
        }

        if (
            SymbolEqualityComparer.Default.Equals(method, entryPoint)
            || !IsCandidate(method, cancellationTokenType, wellKnownReturnTypes, out var returnTypeDescription)
        )
        {
            return;
        }

        var hasUsableCancellationToken = CancellationTokenCallSites.HasUsableCancellationToken(
            context.SemanticModel,
            declaration
        );
        var hasLocalFunctionNeedingToken =
            !hasUsableCancellationToken
            && CancellationTokenCallSites
                .CollectLocalFunctionsNeedingCancellationToken(context.SemanticModel, declaration)
                .Count > 0;

        if (!hasUsableCancellationToken && !hasLocalFunctionNeedingToken)
        {
            return;
        }

        Report(context.ReportDiagnostic, method, returnTypeDescription!);
    }

    // The checks every candidate method must pass regardless of whether it's reported from the symbol action
    // (no body) or the code block action (has a body): return type, override/explicit-interface/partial-definition
    // exclusions, implicit interface implementation, and whether it already has a CancellationToken parameter.
    private static bool IsCandidate(
        IMethodSymbol method,
        INamedTypeSymbol cancellationTokenType,
        WellKnownReturnTypes wellKnownReturnTypes,
        out string? returnTypeDescription
    )
    {
        returnTypeDescription = null;

        // An explicit interface implementation's MethodKind is ExplicitInterfaceImplementation, never Ordinary,
        // so it's already excluded by the check above — there's no case where a method reaches this point with
        // a non-empty ExplicitInterfaceImplementations list.
        if (method.MethodKind != MethodKind.Ordinary || method.IsOverride || method.IsPartialDefinition)
        {
            return false;
        }

        returnTypeDescription = DescribeReturnType(method.ReturnType, wellKnownReturnTypes);
        if (returnTypeDescription is null)
        {
            return false;
        }

        if (ImplementsInterfaceMemberImplicitly(method))
        {
            return false;
        }

        var hasCancellationToken = method.Parameters.Any(parameter =>
            SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType)
        );
        return !hasCancellationToken;
    }

    private static MethodDeclarationSyntax? GetDeclaration(IMethodSymbol method, CancellationToken cancellationToken) =>
        method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) as MethodDeclarationSyntax;

    private static void Report(Action<Diagnostic> reportDiagnostic, IMethodSymbol method, string returnTypeDescription)
    {
        var location = method.Locations.IsEmpty ? Location.None : method.Locations[0];
        reportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.RequireCancellationTokenParameter,
                location,
                method.Name,
                returnTypeDescription
            )
        );
    }

    private static bool ImplementsInterfaceMemberImplicitly(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null || containingType.TypeKind == TypeKind.Interface)
        {
            // Interfaces don't implement their own members; their declarations are flagged directly.
            return false;
        }

        return containingType
            .AllInterfaces.SelectMany(@interface => @interface.GetMembers())
            .OfType<IMethodSymbol>()
            .Any(interfaceMethod =>
                SymbolEqualityComparer.Default.Equals(
                    containingType.FindImplementationForInterfaceMember(interfaceMethod),
                    method
                )
            );
    }

    private static string? DescribeReturnType(ITypeSymbol returnType, WellKnownReturnTypes wellKnownReturnTypes)
    {
        if (SymbolEqualityComparer.Default.Equals(returnType, wellKnownReturnTypes.Task))
        {
            return "Task";
        }

        if (SymbolEqualityComparer.Default.Equals(returnType, wellKnownReturnTypes.ValueTask))
        {
            return "ValueTask";
        }

        if (returnType is not INamedTypeSymbol { IsGenericType: true } namedReturnType)
        {
            return null;
        }

        var originalDefinition = namedReturnType.OriginalDefinition;

        if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownReturnTypes.TaskOfT))
        {
            return "Task<T>";
        }

        if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownReturnTypes.ValueTaskOfT))
        {
            return "ValueTask<T>";
        }

        if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownReturnTypes.AsyncEnumerableOfT))
        {
            return "IAsyncEnumerable<T>";
        }

        return null;
    }

    private readonly struct WellKnownReturnTypes
    {
        public WellKnownReturnTypes(
            INamedTypeSymbol? task,
            INamedTypeSymbol? taskOfT,
            INamedTypeSymbol? valueTask,
            INamedTypeSymbol? valueTaskOfT,
            INamedTypeSymbol? asyncEnumerableOfT
        )
        {
            Task = task;
            TaskOfT = taskOfT;
            ValueTask = valueTask;
            ValueTaskOfT = valueTaskOfT;
            AsyncEnumerableOfT = asyncEnumerableOfT;
        }

        public INamedTypeSymbol? Task { get; }

        public INamedTypeSymbol? TaskOfT { get; }

        public INamedTypeSymbol? ValueTask { get; }

        public INamedTypeSymbol? ValueTaskOfT { get; }

        public INamedTypeSymbol? AsyncEnumerableOfT { get; }
    }
}
