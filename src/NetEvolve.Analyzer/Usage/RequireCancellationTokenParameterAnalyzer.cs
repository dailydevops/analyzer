namespace NetEvolve.Analyzer.Usage;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NE0010 — reports a method whose return type is <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>,
/// <c>ValueTask&lt;T&gt;</c>, or <c>IAsyncEnumerable&lt;T&gt;</c> and that declares no
/// <see cref="System.Threading.CancellationToken"/> parameter. An <see langword="override"/> and an explicit
/// interface implementation are left alone because their signature is fixed by the member they override or
/// implement; a method that implicitly implements an interface member is left alone for the same reason. An
/// interface's own method declaration is still flagged, since it is the contract other members are
/// constrained by. Partial methods are reported only once, on the implementing declaration.
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

        context.RegisterSymbolAction(
            symbolContext => AnalyzeMethod(symbolContext, cancellationTokenType, wellKnownReturnTypes),
            SymbolKind.Method
        );
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol cancellationTokenType,
        WellKnownReturnTypes wellKnownReturnTypes
    )
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.MethodKind != MethodKind.Ordinary || method.IsOverride || method.IsPartialDefinition)
        {
            return;
        }

        if (!method.ExplicitInterfaceImplementations.IsEmpty)
        {
            return;
        }

        var returnTypeDescription = DescribeReturnType(method.ReturnType, wellKnownReturnTypes);
        if (returnTypeDescription is null)
        {
            return;
        }

        if (ImplementsInterfaceMemberImplicitly(method))
        {
            return;
        }

        var hasCancellationToken = method.Parameters.Any(parameter =>
            SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType)
        );
        if (hasCancellationToken)
        {
            return;
        }

        var location = method.Locations.IsEmpty ? Location.None : method.Locations[0];
        context.ReportDiagnostic(
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
