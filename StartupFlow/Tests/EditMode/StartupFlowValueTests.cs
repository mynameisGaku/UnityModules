using System;
using System.Linq;
using NUnit.Framework;

namespace StartupFlow.Tests
{
    /// <summary>公開型と不変値の最小契約を検証する。</summary>
    public sealed class StartupFlowValueTests
    {
        /// <summary>Runtime assemblyの公開型を意図した7型へ固定する。</summary>
        [Test]
        public void PublicSurface_ContainsExactlySevenTypes()
        {
            var types = typeof(StartupFlowService).Assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            Assert.That(types, Is.EqualTo(new[]
            {
                typeof(IStartupStep),
                typeof(StartupFlowError),
                typeof(StartupFlowPhase),
                typeof(StartupFlowResult),
                typeof(StartupFlowService),
                typeof(StartupFlowStatus),
                typeof(StartupStepContext)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray()));
        }

        /// <summary>状態値の等値比較が全fieldを反映する。</summary>
        [Test]
        public void StatusEquality_UsesEveryField()
        {
            var value = new StartupFlowStatus(StartupFlowPhase.Running, "cache", 1, 3, 0.5f, 0.5f);
            Assert.That(value, Is.EqualTo(new StartupFlowStatus(StartupFlowPhase.Running, "cache", 1, 3, 0.5f, 0.5f)));
            Assert.That(value, Is.Not.EqualTo(new StartupFlowStatus(StartupFlowPhase.Running, "cache", 1, 3, 0.6f, 0.5f)));
            Assert.That(value.GetHashCode(), Is.EqualTo(new StartupFlowStatus(StartupFlowPhase.Running, "cache", 1, 3, 0.5f, 0.5f).GetHashCode()));
        }

        /// <summary>成功結果は件数を保持し、失敗位置を持たない。</summary>
        [Test]
        public void SuccessResult_PreservesCounts()
        {
            var result = StartupFlowResult.Success(4);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.None));
            Assert.That(result.FailedStepId, Is.Empty);
            Assert.That(result.CompletedStepCount, Is.EqualTo(4));
            Assert.That(result.TotalStepCount, Is.EqualTo(4));
        }
    }
}
