namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Maintainability;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>Code-fix tests for NE0015: removing the redundant <c>Clear()</c> statement.</summary>
public sealed class AvoidRedundantCollectionClearCodeFixTests
{
    [Test]
    public Task RedundantClear_Removed() =>
        CSharpCodeFixVerifier<
            AvoidRedundantCollectionClearAnalyzer,
            AvoidRedundantCollectionClearCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    {|NE0015:list.Clear()|};
                    list.Add(1L);
                }
            }
            """,
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();

                    list.Add(1L);
                }
            }
            """
        );

    // ---- A trailing comment on the removed line stays with the surrounding code, not lost ------------------

    [Test]
    public Task RedundantClear_WithTrailingComment_CommentPreserved() =>
        CSharpCodeFixVerifier<
            AvoidRedundantCollectionClearAnalyzer,
            AvoidRedundantCollectionClearCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    {|NE0015:list.Clear()|}; // leftover from the old reuse-loop
                    list.Add(1L);
                }
            }
            """,
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
             // leftover from the old reuse-loop
                    list.Add(1L);
                }
            }
            """
        );
}
