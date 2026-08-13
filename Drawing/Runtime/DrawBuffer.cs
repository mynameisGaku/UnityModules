using System.Collections.Generic;
using UnityEngine;

namespace Drawing
{
    /// <summary>描画待ちの線分 1 本。</summary>
    internal struct DrawnLine
    {
        public Vector3 A;
        public Vector3 B;
        public Color Color;

        /// <summary>画面上の太さ（ピクセル）。</summary>
        public float Thickness;

        /// <summary>手前のものに隠れるか。<c>false</c> なら常に最前面に出る。</summary>
        public bool DepthTest;

        /// <summary>この時刻を過ぎたら捨ててよい。</summary>
        public float ExpiresAt;

        /// <summary>積まれたフレーム。1 フレームだけの線を「1 回は必ず描く」ために持つ。</summary>
        public int Frame;

        /// <summary>描画処理へ 1 回以上渡されたか。</summary>
        internal bool Submitted;

        /// <summary>期限を過ぎても、最初の描画処理までは保持する 1 フレーム描画か。</summary>
        internal bool WaitForFirstSubmission;
    }

    /// <summary>描画待ちの文字 1 件。</summary>
    internal struct DrawnLabel
    {
        public Vector3 Position;
        public string Text;
        public Color Color;
        public float ExpiresAt;
        public int Frame;
        internal bool Submitted;
        internal bool WaitForFirstSubmission;
    }

    /// <summary>
    /// 描くものを溜めておく場所。
    /// <para>
    /// 溜めるだけで、描き方も時間の進み方も知らない。現在時刻とフレーム番号は
    /// <see cref="Purge"/> の引数として外から渡す。こうしておくと、
    /// 「2 秒経ったら消える」といった話を Unity を動かさずに確かめられる。
    /// </para>
    /// </summary>
    internal sealed class DrawBuffer
    {
        private readonly List<DrawnLine> _lines = new List<DrawnLine>(256);
        private readonly List<DrawnLabel> _labels = new List<DrawnLabel>(32);
        private int _lineCapacity = 16384;
        private int _labelCapacity = 1024;

        /// <summary>
        /// 溜めておける線分の上限。
        /// <para>
        /// 長い持続時間を指定したまま毎フレーム呼ぶと、際限なく積み上がってエディタごと重くなる。
        /// 描画は補助であって、それが原因で作業が止まるのは本末転倒なので上限で頭を打たせる。
        /// </para>
        /// </summary>
        public int LineCapacity
        {
            get => _lineCapacity;
            set => _lineCapacity = value < 0 ? 0 : value;
        }

        /// <summary>溜めておける文字の上限。</summary>
        public int LabelCapacity
        {
            get => _labelCapacity;
            set => _labelCapacity = value < 0 ? 0 : value;
        }

        /// <summary>上限に達して捨てた分があるか。呼び出し側が 1 回だけ警告を出すのに使う。</summary>
        public bool Overflowed => LineOverflowed || LabelOverflowed;

        /// <summary>線分が上限に達して捨てられたか。</summary>
        public bool LineOverflowed { get; private set; }

        /// <summary>文字が上限に達して捨てられたか。</summary>
        public bool LabelOverflowed { get; private set; }

        public IReadOnlyList<DrawnLine> Lines => _lines;

        public IReadOnlyList<DrawnLabel> Labels => _labels;

        public void AddLine(Vector3 a, Vector3 b, Color color, float thickness, bool depthTest, float expiresAt, int frame, bool waitForFirstSubmission = false)
        {
            if (!IsFinite(a) || !IsFinite(b)) return;

            if (_lines.Count >= LineCapacity)
            {
                LineOverflowed = true;
                return;
            }

            _lines.Add(new DrawnLine
            {
                A = a,
                B = b,
                Color = IsFinite(color) ? color : Color.white,
                Thickness = IsFinite(thickness) && thickness > 0f ? thickness : 1f,
                DepthTest = depthTest,
                ExpiresAt = IsFinite(expiresAt) ? expiresAt : 0f,
                Frame = frame,
                Submitted = false,
                WaitForFirstSubmission = waitForFirstSubmission,
            });
        }

        public void AddLabel(Vector3 position, string text, Color color, float expiresAt, int frame, bool waitForFirstSubmission = false)
        {
            if (string.IsNullOrEmpty(text) || !IsFinite(position)) return;

            if (_labels.Count >= LabelCapacity)
            {
                LabelOverflowed = true;
                return;
            }

            _labels.Add(new DrawnLabel
            {
                Position = position,
                Text = text,
                Color = IsFinite(color) ? color : Color.white,
                ExpiresAt = IsFinite(expiresAt) ? expiresAt : 0f,
                Frame = frame,
                Submitted = false,
                WaitForFirstSubmission = waitForFirstSubmission,
            });
        }

        /// <summary>
        /// 寿命の切れたものを捨てる。
        /// <para>
        /// 持続時間を指定しなかったものは積んだ時点で期限切れになっているが、
        /// <b>積まれたフレームのうちは残す</b>。そうしないと、描かれる前に消えてしまう。
        /// </para>
        /// </summary>
        /// <param name="now">現在時刻。</param>
        /// <param name="frame">現在のフレーム番号。</param>
        public void Purge(float now, int frame)
        {
            // 途中から削ると後ろが毎回ずれるので、走査しながら残すものを前へ詰め、
            // 最後に長さを切り詰める。数千本あっても 1 回の走査で済む。
            var kept = 0;
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                var expired = now >= line.ExpiresAt && frame > line.Frame;
                if (expired && (!line.WaitForFirstSubmission || line.Submitted)) continue;

                _lines[kept++] = line;
            }

            if (kept != _lines.Count) _lines.RemoveRange(kept, _lines.Count - kept);

            kept = 0;
            for (var i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                var expired = now >= label.ExpiresAt && frame > label.Frame;
                // ゲームビューの再描画が一度も来ないbatch/headless環境でも、
                // 1フレーム文字を無期限に溜めない。追加の次フレームまでは再描画機会を待つ。
                var waitingForRepaint = label.WaitForFirstSubmission
                    && !label.Submitted
                    && frame <= label.Frame + 1;
                if (expired && !waitingForRepaint) continue;

                _labels[kept++] = label;
            }

            if (kept != _labels.Count) _labels.RemoveRange(kept, _labels.Count - kept);
        }

        /// <summary>指定した深度設定の線分を、描画処理へ渡した状態にする。</summary>
        internal void MarkLinesSubmitted(bool depthTest)
        {
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.DepthTest != depthTest || line.Submitted) continue;

                line.Submitted = true;
                _lines[i] = line;
            }
        }

        /// <summary>文字を画面描画処理へ渡した状態にする。</summary>
        internal void MarkLabelsSubmitted()
        {
            for (var i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                if (label.Submitted) continue;

                label.Submitted = true;
                _labels[i] = label;
            }
        }

        /// <summary>
        /// カメラが無い再描画では、1 フレーム文字を表示済み扱いにして後からまとめて出るのを防ぐ。
        /// 持続時間つきの文字は期限内にカメラが用意される可能性があるため残す。
        /// </summary>
        internal void DiscardSingleFrameLabels()
        {
            var kept = 0;
            for (var i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                if (label.WaitForFirstSubmission) continue;

                _labels[kept++] = label;
            }

            if (kept != _labels.Count) _labels.RemoveRange(kept, _labels.Count - kept);
        }

        /// <summary>溜めたものを全部捨てる。</summary>
        public void Clear()
        {
            _lines.Clear();
            _labels.Clear();
            LineOverflowed = false;
            LabelOverflowed = false;
        }

        /// <summary>ベクトルの全成分が有限値か確かめる。</summary>
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        /// <summary>色の全成分が有限値か確かめる。</summary>
        private static bool IsFinite(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
        }

        /// <summary>数値が NaN でも無限大でもないか確かめる。</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
