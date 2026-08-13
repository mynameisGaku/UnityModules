using NUnit.Framework;
using UnityEngine;

namespace Drawing.Tests
{
    /// <summary>
    /// 溜めたものがいつ消えるか。時刻とフレーム番号を外から渡す作りなので、
    /// 実際に時間を進めずに確かめられる。
    /// </summary>
    public sealed class DrawBufferTests
    {
        private DrawBuffer _buffer;

        [SetUp]
        public void SetUp() => _buffer = new DrawBuffer();

        private void AddLine(float expiresAt, int frame, bool waitForFirstSubmission = false, bool depthTest = true)
        {
            _buffer.AddLine(Vector3.zero, Vector3.one, Color.white, 1f, depthTest, expiresAt, frame, waitForFirstSubmission);
        }

        [Test]
        public void SingleFrameLine_SurvivesItsOwnFrameAndIsGoneTheNext()
        {
            // 持続時間を指定しない線は、積んだ時点でもう期限切れ。
            // それでも積まれたフレームのうちは残さないと、一度も描かれないまま消える。
            AddLine(expiresAt: 3f, frame: 10, waitForFirstSubmission: true);

            _buffer.Purge(3f, 10);
            Assert.AreEqual(1, _buffer.Lines.Count, "積んだフレームでは残る");

            MarkLinesSubmitted(depthTest: true);
            _buffer.Purge(3f, 11);
            Assert.AreEqual(0, _buffer.Lines.Count, "次のフレームには消える");
        }

        [Test]
        public void TimedLine_SurvivesUntilItsDeadline()
        {
            AddLine(expiresAt: 5f, frame: 10);
            MarkLinesSubmitted(depthTest: true);

            _buffer.Purge(4.9f, 30);
            Assert.AreEqual(1, _buffer.Lines.Count);

            _buffer.Purge(5f, 31);
            Assert.AreEqual(0, _buffer.Lines.Count, "期限に達したら消える");
        }

        [Test]
        public void TimedLine_ExpiresAtItsDeadlineWithoutBeingSubmitted()
        {
            AddLine(expiresAt: 5f, frame: 10);

            _buffer.Purge(4.9f, 30);
            Assert.AreEqual(1, _buffer.Lines.Count);

            _buffer.Purge(5f, 31);
            Assert.IsEmpty(_buffer.Lines, "描画機会が無くても持続時間を超えて残さない");
        }

        [Test]
        public void Purge_KeepsTheSurvivorsAndDropsTheRestInOnePass()
        {
            AddLine(expiresAt: 1f, frame: 1);
            AddLine(expiresAt: 100f, frame: 1);
            AddLine(expiresAt: 2f, frame: 1);
            AddLine(expiresAt: 100f, frame: 1);
            MarkLinesSubmitted(depthTest: true);

            _buffer.Purge(50f, 2);

            Assert.AreEqual(2, _buffer.Lines.Count);
            foreach (var line in _buffer.Lines)
            {
                Assert.AreEqual(100f, line.ExpiresAt, "残るべきものだけが残っている");
            }
        }

        [Test]
        public void Lines_StopBeingAcceptedAtTheCapacity()
        {
            // 持続時間を付けたまま毎フレーム呼ぶと際限なく積み上がる。
            // 描画は補助なので、それが原因でエディタが重くなるほうが困る。
            _buffer.LineCapacity = 2;

            AddLine(1f, 1);
            AddLine(1f, 1);
            AddLine(1f, 1);

            Assert.AreEqual(2, _buffer.Lines.Count);
            Assert.IsTrue(_buffer.Overflowed);
            Assert.IsTrue(_buffer.LineOverflowed);
            Assert.IsFalse(_buffer.LabelOverflowed);
        }

        [Test]
        public void Capacities_ClampNegativeValuesToZeroAndTrackOverflowSeparately()
        {
            _buffer.LineCapacity = -1;
            _buffer.LabelCapacity = -1;

            AddLine(1f, 1);
            _buffer.AddLabel(Vector3.zero, "文字", Color.white, 1f, 1);

            Assert.AreEqual(0, _buffer.LineCapacity);
            Assert.AreEqual(0, _buffer.LabelCapacity);
            Assert.IsEmpty(_buffer.Lines);
            Assert.IsEmpty(_buffer.Labels);
            Assert.IsTrue(_buffer.LineOverflowed);
            Assert.IsTrue(_buffer.LabelOverflowed);
        }

        [Test]
        public void AddLine_RejectsInvalidPositionsAndSanitizesInvalidStyleValues()
        {
            _buffer.AddLine(
                new Vector3(float.NaN, 0f, 0f),
                Vector3.one,
                Color.white,
                1f,
                true,
                1f,
                1);

            Assert.IsEmpty(_buffer.Lines);

            _buffer.AddLine(
                Vector3.zero,
                Vector3.one,
                new Color(float.PositiveInfinity, 0f, 0f, 1f),
                float.NaN,
                true,
                float.NegativeInfinity,
                1);

            Assert.AreEqual(1, _buffer.Lines.Count);
            Assert.AreEqual(Color.white, _buffer.Lines[0].Color);
            Assert.AreEqual(1f, _buffer.Lines[0].Thickness);
            Assert.AreEqual(0f, _buffer.Lines[0].ExpiresAt);
        }

        [Test]
        public void ExpiredLine_RemainsUntilItHasActuallyBeenSubmitted()
        {
            AddLine(expiresAt: 0f, frame: 10, waitForFirstSubmission: true);

            _buffer.Purge(100f, 11);
            Assert.AreEqual(1, _buffer.Lines.Count, "LateUpdate 後に積まれた線は次回の描画まで残る");

            MarkLinesSubmitted(depthTest: true);
            _buffer.Purge(100f, 12);
            Assert.IsEmpty(_buffer.Lines);
        }

        [Test]
        public void Labels_IgnoreEmptyText()
        {
            _buffer.AddLabel(Vector3.zero, null, Color.white, 1f, 1);
            _buffer.AddLabel(Vector3.zero, string.Empty, Color.white, 1f, 1);
            _buffer.AddLabel(Vector3.zero, "出る", Color.white, 1f, 1);

            Assert.AreEqual(1, _buffer.Labels.Count);
            Assert.AreEqual("出る", _buffer.Labels[0].Text);
        }

        [Test]
        public void TimedLabels_ExpireAtTheirDeadlineWithoutBeingSubmitted()
        {
            _buffer.AddLabel(Vector3.zero, "期限切れ", Color.white, 3f, 10);
            _buffer.AddLabel(Vector3.zero, "しばらく残る", Color.white, 100f, 10);

            _buffer.Purge(3f, 10);
            Assert.AreEqual(2, _buffer.Labels.Count);

            _buffer.Purge(3f, 11);
            Assert.AreEqual(1, _buffer.Labels.Count);
            Assert.AreEqual("しばらく残る", _buffer.Labels[0].Text);
        }

        [Test]
        public void CameraUnavailableRepaint_DiscardsOnlySingleFrameLabels()
        {
            _buffer.AddLabel(Vector3.zero, "1 フレーム", Color.white, 0f, 10, waitForFirstSubmission: true);
            _buffer.AddLabel(Vector3.zero, "期限内", Color.white, 100f, 10);

            _buffer.DiscardSingleFrameLabels();

            Assert.AreEqual(1, _buffer.Labels.Count);
            Assert.AreEqual("期限内", _buffer.Labels[0].Text);
        }

        [Test]
        public void SingleFrameLabelWithoutRepaint_ExpiresAfterOneGraceFrame()
        {
            _buffer.AddLabel(Vector3.zero, "再描画待ち", Color.white, 0f, 10, waitForFirstSubmission: true);

            _buffer.Purge(1f, 11);
            Assert.AreEqual(1, _buffer.Labels.Count, "追加直後の次フレームまでは再描画機会を待つ");

            _buffer.Purge(1f, 12);
            Assert.IsEmpty(_buffer.Labels, "再描画が無い環境でも文字を無期限に溜めない");
        }

        [Test]
        public void DepthSpecificSubmission_DoesNotConsumeTheOtherLineGroup()
        {
            AddLine(expiresAt: 0f, frame: 10, waitForFirstSubmission: true, depthTest: true);
            AddLine(expiresAt: 0f, frame: 10, waitForFirstSubmission: true, depthTest: false);

            _buffer.MarkLinesSubmitted(depthTest: true);
            _buffer.Purge(1f, 11);

            Assert.AreEqual(1, _buffer.Lines.Count);
            Assert.IsFalse(_buffer.Lines[0].DepthTest);

            _buffer.MarkLinesSubmitted(depthTest: false);
            _buffer.Purge(1f, 12);
            Assert.IsEmpty(_buffer.Lines);
        }

        [Test]
        public void Clear_EmptiesEverythingAndResetsTheOverflowFlag()
        {
            _buffer.LineCapacity = 1;
            AddLine(100f, 1);
            AddLine(100f, 1);
            _buffer.AddLabel(Vector3.zero, "文字", Color.white, 100f, 1);

            Assert.IsTrue(_buffer.Overflowed);

            _buffer.Clear();

            Assert.IsEmpty(_buffer.Lines);
            Assert.IsEmpty(_buffer.Labels);
            Assert.IsFalse(_buffer.Overflowed);
        }

        private void MarkLinesSubmitted(bool depthTest)
        {
            _buffer.MarkLinesSubmitted(depthTest);
        }
    }
}
