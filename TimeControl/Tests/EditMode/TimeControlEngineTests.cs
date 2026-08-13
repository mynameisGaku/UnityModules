using System;
using NUnit.Framework;

namespace TimeControl.Tests
{
    /// <summary>所有開始、書込確認、外部変更、終了復元を偽の時間倍率保管先で検証する。</summary>
    public sealed class TimeControlEngineTests
    {
        /// <summary>1以外の基準値を正確に保持し、相対倍率を掛けた値を適用する。</summary>
        [Test]
        public void StartAndApply_NonUnitBaseline_WritesRelativeScale()
        {
            var backend = new FakeTimeScaleBackend(0.8f);
            var engine = new TimeControlEngine(backend);

            Assert.That(engine.TryStart(out var baseline), Is.EqualTo(TimeControlError.None));
            Assert.That(baseline, Is.EqualTo(0.8f));
            Assert.That(engine.Apply(0.25f, out var actual), Is.EqualTo(TimeControlError.None));
            Assert.That(actual, Is.EqualTo(0.2f).Within(0.000001f));
            Assert.That(backend.Value, Is.EqualTo(0.2f).Within(0.000001f));
        }

        /// <summary>外部変更を検出した後は外部値を上書きせず、終了時にも基準値を復元しない。</summary>
        [Test]
        public void ExternalChange_FaultThenStop_PreservesExternalValue()
        {
            var backend = new FakeTimeScaleBackend(1f);
            var engine = new TimeControlEngine(backend);
            Assert.That(engine.TryStart(out _), Is.EqualTo(TimeControlError.None));
            Assert.That(engine.Apply(0.5f, out _), Is.EqualTo(TimeControlError.None));

            backend.Value = 0.75f;
            var check = engine.CheckExpected(out var externalValue);
            engine.Fault();
            var stop = engine.Stop(out var stoppedValue);

            Assert.That(check, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(externalValue, Is.EqualTo(0.75f));
            Assert.That(stop, Is.EqualTo(TimeControlError.None));
            Assert.That(stoppedValue, Is.EqualTo(0.75f));
            Assert.That(backend.Value, Is.EqualTo(0.75f));
        }

        /// <summary>健康な終了は実値一致を確認してから所有開始時の値を正確に復元する。</summary>
        [Test]
        public void Stop_HealthyReservation_RestoresExactBaseline()
        {
            var backend = new FakeTimeScaleBackend(0.625f);
            var engine = new TimeControlEngine(backend);
            Assert.That(engine.TryStart(out _), Is.EqualTo(TimeControlError.None));
            Assert.That(engine.Apply(0f, out _), Is.EqualTo(TimeControlError.None));

            var error = engine.Stop(out var actual);

            Assert.That(error, Is.EqualTo(TimeControlError.None));
            Assert.That(actual, Is.EqualTo(0.625f));
            Assert.That(backend.Value, Is.EqualTo(0.625f));
            Assert.That(engine.HasReservation, Is.False);
        }

        /// <summary>終了直前の外部変更は復元で上書きせず、外部変更として返す。</summary>
        [Test]
        public void Stop_ExternallyChangedReservation_DoesNotRestore()
        {
            var backend = new FakeTimeScaleBackend(1f);
            var engine = new TimeControlEngine(backend);
            Assert.That(engine.TryStart(out _), Is.EqualTo(TimeControlError.None));
            backend.Value = 0.4f;

            var error = engine.Stop(out var actual);

            Assert.That(error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(actual, Is.EqualTo(0.4f));
            Assert.That(backend.Value, Is.EqualTo(0.4f));
        }

        /// <summary>書込後の読取値が異なる場合は書込失敗として扱う。</summary>
        [Test]
        public void Apply_ReadbackMismatch_ReturnsWriteFailure()
        {
            var backend = new FakeTimeScaleBackend(1f);
            var engine = new TimeControlEngine(backend);
            Assert.That(engine.TryStart(out _), Is.EqualTo(TimeControlError.None));
            backend.OverrideReadAfterWrite = 0.75f;

            var error = engine.Apply(0.5f, out var actual);

            Assert.That(error, Is.EqualTo(TimeControlError.TimeScaleWriteFailed));
            Assert.That(actual, Is.EqualTo(0.75f));
        }

        /// <summary>読取または書込の例外を呼出側へ投げず、明示的な書込失敗へ変換する。</summary>
        [Test]
        public void BackendExceptions_ReturnWriteFailure()
        {
            var readFailure = new FakeTimeScaleBackend(1f) { ThrowOnRead = true };
            var startEngine = new TimeControlEngine(readFailure);
            Assert.That(startEngine.TryStart(out _), Is.EqualTo(TimeControlError.TimeScaleWriteFailed));

            var writeFailure = new FakeTimeScaleBackend(1f);
            var applyEngine = new TimeControlEngine(writeFailure);
            Assert.That(applyEngine.TryStart(out _), Is.EqualTo(TimeControlError.None));
            writeFailure.ThrowOnWrite = true;
            Assert.That(applyEngine.Apply(0.5f, out _), Is.EqualTo(TimeControlError.TimeScaleWriteFailed));
        }

        /// <summary>有限範囲外または非有限の開始値を所有せず、明示的な範囲エラーを返す。</summary>
        [TestCase(-0.01f)]
        [TestCase(100.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryStart_InvalidBaseline_DoesNotReserve(float baseline)
        {
            var engine = new TimeControlEngine(new FakeTimeScaleBackend(baseline));

            var error = engine.TryStart(out var actual);

            Assert.That(error, Is.EqualTo(TimeControlError.EffectiveTimeScaleOutOfRange));
            Assert.That(engine.HasReservation, Is.False);
            if (float.IsNaN(baseline)) Assert.That(actual, Is.NaN);
            else Assert.That(actual, Is.EqualTo(baseline));
        }

        private sealed class FakeTimeScaleBackend : ITimeScaleBackend
        {
            private bool _written;

            /// <summary>開始値を持つ偽の保管先を作る。</summary>
            /// <param name="value">最初に読み取らせる値。</param>
            internal FakeTimeScaleBackend(float value)
            {
                Value = value;
            }

            /// <summary>保管している実値。</summary>
            internal float Value { get; set; }

            /// <summary>読取時に例外を送出する場合はtrue。</summary>
            internal bool ThrowOnRead { get; set; }

            /// <summary>書込時に例外を送出する場合はtrue。</summary>
            internal bool ThrowOnWrite { get; set; }

            /// <summary>書込後だけ強制的に返す値。nullなら実値を返す。</summary>
            internal float? OverrideReadAfterWrite { get; set; }

            /// <summary>設定に従って値を読み取る。</summary>
            /// <returns>実値または強制値。</returns>
            public float Read()
            {
                if (ThrowOnRead) throw new InvalidOperationException("read failure");
                return _written && OverrideReadAfterWrite.HasValue ? OverrideReadAfterWrite.Value : Value;
            }

            /// <summary>設定に従って値を書き込む。</summary>
            /// <param name="value">書き込む値。</param>
            public void Write(float value)
            {
                if (ThrowOnWrite) throw new InvalidOperationException("write failure");
                Value = value;
                _written = true;
            }
        }
    }
}
