namespace NetEvolve.Analyzer.Tests.Integration.Helpers;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using NetEvolve.Analyzer.Helpers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Direct tests for the shared <see cref="NullCheckOperand"/> helper, driven off real
/// <see cref="IOperation"/> instances so every branch (null-literal detection through conversions, operand
/// eligibility across reference / nullable / value / type-parameter / pointer types, and expression-tree
/// detection) is exercised on its own rather than only through the NE0004–NE0006 rules.
/// </summary>
public sealed class NullCheckOperandTests
{
    // ---- IsNullLiteral ----------------------------------------------------------------------------------

    [Test]
    public async Task IsNullLiteral_NullOperand_True()
    {
        var binary = Binary("public bool M(object value) => value == null;");

        await Assert.That(NullCheckOperand.IsNullLiteral(binary.RightOperand)).IsTrue();
        await Assert.That(NullCheckOperand.IsNullLiteral(binary.LeftOperand)).IsFalse();
    }

    [Test]
    public async Task IsNullLiteral_CastWrappedNull_True()
    {
        // The null literal is wrapped in an explicit conversion; IsNullLiteral must see through it.
        var binary = Binary("public bool M(object value) => value == (object)null;");

        await Assert.That(NullCheckOperand.IsNullLiteral(binary.RightOperand)).IsTrue();
    }

    // ---- IsPatternable ----------------------------------------------------------------------------------

    [Test]
    public async Task IsPatternable_ReferenceType_True() =>
        await Assert.That(NullCheckOperand.IsPatternable(IsObjectValue("string"))).IsTrue();

    [Test]
    public async Task IsPatternable_NullableValueType_True() =>
        await Assert.That(NullCheckOperand.IsPatternable(IsObjectValue("int?"))).IsTrue();

    [Test]
    public async Task IsPatternable_NonNullableValueType_False() =>
        await Assert.That(NullCheckOperand.IsPatternable(IsObjectValue("int"))).IsFalse();

    [Test]
    public async Task IsPatternable_UnconstrainedTypeParameter_True()
    {
        var isType = IsType("public bool M<T>(T value) => value is object;");

        await Assert.That(NullCheckOperand.IsPatternable(isType.ValueOperand)).IsTrue();
    }

    [Test]
    public async Task IsPatternable_StructConstrainedTypeParameter_False()
    {
        var isType = IsType("public bool M<T>(T value) where T : struct => value is object;");

        await Assert.That(NullCheckOperand.IsPatternable(isType.ValueOperand)).IsFalse();
    }

    // ---- IsWithinExpressionTree -------------------------------------------------------------------------

    [Test]
    public async Task IsWithinExpressionTree_InsideExpressionTree_True()
    {
        var binary = Binary(
            """
            public System.Linq.Expressions.Expression<System.Func<object, bool>> M() => value => value == null;
            """
        );

        await Assert.That(NullCheckOperand.IsWithinExpressionTree(binary)).IsTrue();
    }

    [Test]
    public async Task IsWithinExpressionTree_PlainLambda_False()
    {
        var binary = Binary("public System.Func<object, bool> M() => value => value == null;");

        await Assert.That(NullCheckOperand.IsWithinExpressionTree(binary)).IsFalse();
    }

    // ---- Helpers ----------------------------------------------------------------------------------------

    private static IBinaryOperation Binary(string member) =>
        Operation<BinaryExpressionSyntax, IBinaryOperation>(member);

    private static IIsTypeOperation IsType(string member) =>
        Operation<BinaryExpressionSyntax, IIsTypeOperation>(member);

    private static IOperation IsObjectValue(string parameterType) =>
        IsType($"public bool M({parameterType} value) => value is object;").ValueOperand;

    private static TOperation Operation<TSyntax, TOperation>(string member)
        where TSyntax : SyntaxNode
        where TOperation : class, IOperation
    {
        var source = $$"""
            public class C
            {
                {{member}}
            }
            """;

        var compilation = AnalyzerCompiler.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var node = tree.GetRoot().DescendantNodes().OfType<TSyntax>().First(n => model.GetOperation(n) is TOperation);

        return (TOperation)model.GetOperation(node)!;
    }
}
