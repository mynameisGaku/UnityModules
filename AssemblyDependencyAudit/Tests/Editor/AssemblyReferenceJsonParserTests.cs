using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// asmref JSON の strict grammar、reference shape、Unicode 境界を検証します。
    /// </summary>
    internal sealed class AssemblyReferenceJsonParserTests
    {
        /// <summary>
        /// 直下 reference を escape 復元後に取得し、未知の正当な JSON value は保持対象外として許可します。
        /// </summary>
        [TestCase("{\"reference\":\"Target\"}", "Target")]
        [TestCase(" { \"metadata\" : {\"values\":[1,true,null]}, \"reference\":\"GUID:0123456789abcdef0123456789abcdef\" } ",
            "GUID:0123456789abcdef0123456789abcdef")]
        [TestCase("{\"ref\\u0065rence\":\"EscapedTarget\"}", "EscapedTarget")]
        public void Parse_ValidObjectReturnsDecodedReference(string json, string expectedReference)
        {
            var status = AuditEditor.AssemblyReferenceJsonParser.Parse(json, out var reference);

            Assert.That(status, Is.EqualTo(AuditEditor.AssemblyReferenceJsonParseStatus.Valid));
            Assert.That(reference, Is.EqualTo(expectedReference));
        }

        /// <summary>
        /// reference が無い、null、空、空白だけの場合を構文不正と混同しません。
        /// </summary>
        [TestCase("{}", "")]
        [TestCase("{\"reference\":null}", "")]
        [TestCase("{\"reference\":\"\"}", "")]
        [TestCase("{\"reference\":\" \\t \"}", " \t ")]
        public void Parse_AbsentNullOrBlankReferenceReturnsMissing(string json, string expectedReference)
        {
            var status = AuditEditor.AssemblyReferenceJsonParser.Parse(json, out var reference);

            Assert.That(status, Is.EqualTo(AuditEditor.AssemblyReferenceJsonParseStatus.MissingReference));
            Assert.That(reference, Is.EqualTo(expectedReference));
        }

        /// <summary>
        /// root shape、構文、重複 key、string 以外の reference を InvalidJson として拒否します。
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{\"reference\":")]
        [TestCase("{\"reference\":1}")]
        [TestCase("{\"reference\":true}")]
        [TestCase("{\"reference\":{}}")]
        [TestCase("{\"reference\":[]}")]
        [TestCase("{\"reference\":\"A\",\"reference\":\"B\"}")]
        [TestCase("{\"reference\":\"A\",\"ref\\u0065rence\":\"B\"}")]
        [TestCase("{\"other\":1,\"other\":2,\"reference\":\"A\"}")]
        [TestCase("{\"reference\":\"A\"} trailing")]
        public void Parse_InvalidShapeOrDuplicateKeyReturnsInvalidJson(string json)
        {
            var status = AuditEditor.AssemblyReferenceJsonParser.Parse(json, out var reference);

            Assert.That(status, Is.EqualTo(AuditEditor.AssemblyReferenceJsonParseStatus.InvalidJson));
            Assert.That(reference, Is.Empty);
        }

        /// <summary>
        /// raw と Unicode escape のどちらでも正しい surrogate pair だけを受理します。
        /// </summary>
        [Test]
        public void Parse_AcceptsPairedSurrogatesAndRejectsUnpairedCodeUnits()
        {
            const string escapedPairJson = "{\"reference\":\"\\uD83D\\uDE00\"}";
            var rawPairJson = "{\"reference\":\"" + '\uD83D' + '\uDE00' + "\"}";
            var rawHighJson = "{\"reference\":\"" + '\uD800' + "\"}";
            var rawLowJson = "{\"reference\":\"" + '\uDC00' + "\"}";

            AssertParsedReference(escapedPairJson, "\uD83D\uDE00");
            AssertParsedReference(rawPairJson, "\uD83D\uDE00");
            AssertInvalidJson("{\"reference\":\"\\uD800\"}");
            AssertInvalidJson("{\"reference\":\"\\uDC00\"}");
            AssertInvalidJson(rawHighJson);
            AssertInvalidJson(rawLowJson);
        }

        /// <summary>
        /// 深さ上限を超える未知 property でも全体を InvalidJson とし、再帰消費を制限します。
        /// </summary>
        [Test]
        public void Parse_RejectsExcessivelyDeepJson()
        {
            var nested = new string('[', 65) + "0" + new string(']', 65);
            var json = "{\"reference\":\"Target\",\"nested\":" + nested + "}";

            AssertInvalidJson(json);
        }

        /// <summary>Valid と復元済み reference を同時に確認します。</summary>
        private static void AssertParsedReference(string json, string expectedReference)
        {
            var status = AuditEditor.AssemblyReferenceJsonParser.Parse(json, out var reference);

            Assert.That(status, Is.EqualTo(AuditEditor.AssemblyReferenceJsonParseStatus.Valid));
            Assert.That(reference, Is.EqualTo(expectedReference));
        }

        /// <summary>InvalidJson と空の部分結果を同時に確認します。</summary>
        private static void AssertInvalidJson(string json)
        {
            var status = AuditEditor.AssemblyReferenceJsonParser.Parse(json, out var reference);

            Assert.That(status, Is.EqualTo(AuditEditor.AssemblyReferenceJsonParseStatus.InvalidJson));
            Assert.That(reference, Is.Empty);
        }
    }
}
