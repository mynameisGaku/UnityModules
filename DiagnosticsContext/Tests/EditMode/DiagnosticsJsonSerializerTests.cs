using System;
using NUnit.Framework;
using UnityEngine;

namespace DiagnosticsContext.Tests
{
    /// <summary>schema version 1のfield順、escape、culture非依存値をgolden JSONで確認する。</summary>
    public sealed class DiagnosticsJsonSerializerTests
    {
        /// <summary>全fieldと各itemを契約どおりの固定順で出力する。</summary>
        [Test]
        public void Serialize_AllFields_ReturnsExactGoldenJson()
        {
            var snapshot = new DiagnosticsReportSnapshot(
                new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc).AddTicks(4567),
                "manual\n\"reason\"",
                4,
                5,
                new[] { new DiagnosticsContextItem("a", "one"), new DiagnosticsContextItem("z", "two") },
                new[] { new DiagnosticsBreadcrumbItem(7, "clicked") },
                new[] { new DiagnosticsLogItem(8, LogType.Warning, "warn", "stack\\path") });

            var actual = DiagnosticsJsonSerializer.Serialize(snapshot);

            Assert.That(actual, Is.EqualTo("{\"schemaVersion\":1,\"createdUtc\":\"2026-08-14T01:02:03.0004567Z\",\"reason\":\"manual\\n\\\"reason\\\"\",\"droppedBreadcrumbCount\":4,\"droppedLogCount\":5,\"context\":[{\"key\":\"a\",\"value\":\"one\"},{\"key\":\"z\",\"value\":\"two\"}],\"breadcrumbs\":[{\"sequence\":7,\"message\":\"clicked\"}],\"logs\":[{\"sequence\":8,\"type\":\"Warning\",\"message\":\"warn\",\"stackTrace\":\"stack\\\\path\"}]}"));
        }
    }
}
