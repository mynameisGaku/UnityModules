using NUnit.Framework;

namespace DiagnosticsContext.Tests
{
    /// <summary>Unicode scalar単位の入力検証とcallback用切詰めを確認する。</summary>
    public sealed class DiagnosticsTextTests
    {
        /// <summary>surrogate pairを1 scalarとして扱い、境界を壊さず切り詰める。</summary>
        [Test]
        public void NormalizeAndTruncate_SurrogatePairAtBoundary_KeepsWholeScalar()
        {
            var value = "A\U0001F642B";

            Assert.That(DiagnosticsText.NormalizeAndTruncate(value, 2), Is.EqualTo("A\U0001F642"));
            Assert.That(DiagnosticsText.IsValidRequiredInput(value, 3), Is.True);
            Assert.That(DiagnosticsText.IsValidRequiredInput(value, 2), Is.False);
        }

        /// <summary>単独surrogateを置換文字へ変換し、JSON encoderへ不正文字列を渡さない。</summary>
        [Test]
        public void NormalizeAndTruncate_UnpairedSurrogates_ReplacesEachMalformedScalar()
        {
            var value = "\uD800A\uDC00";

            Assert.That(DiagnosticsText.NormalizeAndTruncate(value, 3), Is.EqualTo("\uFFFDA\uFFFD"));
            Assert.That(DiagnosticsText.IsValidRequiredInput(value, 3), Is.False);
        }
    }
}
