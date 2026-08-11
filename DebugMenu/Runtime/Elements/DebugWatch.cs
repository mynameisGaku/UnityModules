using System;
using Containers;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 値を眺めるだけの行。編集はできない。
    /// <para>
    /// 毎フレーム関数を呼んで右カラムに出す。FPS、残弾、状態名など
    /// 「触りたいのではなく見ていたい」ものに使う。
    /// <see cref="DebugElement.SetWarnRange"/> と組み合わせると、
    /// 予算を超えた瞬間に色が変わる。
    /// </para>
    /// </summary>
    public sealed class DebugWatch : DebugElement
    {
        private readonly Func<string> _textProvider;
        private readonly Func<float> _valueProvider;

        /// <summary>文字列を出す監視行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="textProvider">右カラムへ出す文字列を返す関数。</param>
        public DebugWatch(string label, Func<string> textProvider) : base(label)
        {
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
            IsExpandable = false;
        }

        /// <summary>数値を出す監視行を作る。注意色の判定にも使われる。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="valueProvider">監視する値を返す関数。</param>
        /// <param name="digits">小数点以下の桁数。</param>
        public DebugWatch(string label, Func<float> valueProvider, int digits = 2) : base(label)
        {
            _valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
            Digits = Mathf.Clamp(digits, 0, 9);
            IsExpandable = false;
        }

        /// <summary>小数点以下の桁数。文字列を出す行では使われない。</summary>
        public int Digits { get; set; } = 2;

        /// <summary>保存対象にしない。読むだけの行のため。</summary>
        public override bool IsSaveable => false;

        /// <inheritdoc/>
        public override string GetValueText() =>
            _textProvider != null
                ? _textProvider() ?? string.Empty
                : _valueProvider().ToString("F" + Digits, System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>数値の監視行なら値を返す。注意色の判定に使われる。</summary>
        /// <param name="value">書き込み先。</param>
        public override bool TryGetFloat(out float value)
        {
            if (_valueProvider == null)
            {
                value = 0f;
                return false;
            }

            value = _valueProvider();
            return true;
        }

        /// <summary>決定しても何も起きない。</summary>
        public override void OnDecide() { }
    }

    /// <summary>
    /// 値の推移を折れ線で見る行。
    /// <para>
    /// 標本は <see cref="RingBuffer{T}"/> に溜める。「直近 N 件だけ保持して古いものは捨てる」が
    /// まさにこのコンテナの形なので、溢れの処理を自分で書かずに済む。
    /// </para>
    /// <para>
    /// 標本を取るのは<b>画面に出ている間だけ</b>。閉じている行まで毎フレーム
    /// 関数を呼ぶと、メニューを開いていないのに負荷がかかる。
    /// </para>
    /// </summary>
    public sealed class DebugGraph : DebugElement
    {
        private readonly Func<float> _provider;
        private readonly RingBuffer<float> _samples;

        private float _accumulated;

        /// <summary>監視する値と保持する標本数を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="provider">標本にする値を返す関数。</param>
        /// <param name="sampleCount">保持する標本の数。</param>
        public DebugGraph(string label, Func<float> provider, int sampleCount = 120) : base(label)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _samples = new RingBuffer<float>(Mathf.Max(2, sampleCount));

            // 子行を持たないが、畳んで場所を空けられるようにする。
            MarkerVisibility = DebugMarkerVisibility.Always;
            IsExpanded = true;
        }

        /// <summary>縦軸の下端。<see cref="AutoScale"/> が true なら自動で決まる。</summary>
        public float Min { get; set; }

        /// <summary>縦軸の上端。<see cref="AutoScale"/> が true なら自動で決まる。</summary>
        public float Max { get; set; } = 1f;

        /// <summary>縦軸を標本の範囲に合わせるか。</summary>
        public bool AutoScale { get; set; } = true;

        /// <summary>標本を取る間隔（秒）。0 なら毎フレーム。</summary>
        public float SampleInterval { get; set; }

        /// <summary>小数点以下の桁数。</summary>
        public int Digits { get; set; } = 2;

        /// <summary>溜まっている標本。古い順に並ぶ。</summary>
        public RingBuffer<float> Samples => _samples;

        /// <summary>行 1 つ分に対する高さの倍率。折れ線を描くぶん背を高くする。</summary>
        public float HeightRatio { get; set; } = 3f;

        /// <summary>保存対象にしない。読むだけの行のため。</summary>
        public override bool IsSaveable => false;

        /// <summary>子行を持たないが畳めるようにする。</summary>
        public override bool IsAdjustable => false;

        /// <inheritdoc/>
        public override string GetValueText() =>
            _samples.Count == 0
                ? "--"
                : _samples[_samples.Count - 1].ToString("F" + Digits, System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>決定で折れ線の表示を畳む。</summary>
        public override void OnDecide()
        {
            if (IsExpandable) IsExpanded = !IsExpanded;
        }

        /// <summary>直近の値を返す。注意色の判定に使われる。</summary>
        /// <param name="value">書き込み先。</param>
        public override bool TryGetFloat(out float value)
        {
            value = _samples.Count == 0 ? 0f : _samples[_samples.Count - 1];
            return _samples.Count > 0;
        }

        /// <summary>標本を溜める。画面に出ている行だけが呼ばれる。</summary>
        /// <param name="deltaSeconds">前フレームからの経過秒。</param>
        public override void Tick(float deltaSeconds)
        {
            if (SampleInterval > 0f)
            {
                _accumulated += deltaSeconds;
                if (_accumulated < SampleInterval) return;

                // 溜まった分を丸ごと捨てず引くので、長いフレームがあっても間隔がずれない。
                _accumulated -= SampleInterval;
            }

            _samples.PushBack(_provider());
        }

        /// <summary>縦軸の範囲を求める。標本が無ければ設定値をそのまま返す。</summary>
        /// <param name="min">下端の書き込み先。</param>
        /// <param name="max">上端の書き込み先。</param>
        public void GetScale(out float min, out float max)
        {
            if (!AutoScale || _samples.Count == 0)
            {
                min = Min;
                max = Max;
                return;
            }

            min = float.MaxValue;
            max = float.MinValue;
            var hasFiniteSample = false;

            for (var i = 0; i < _samples.Count; i++)
            {
                var sample = _samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample)) continue;

                hasFiniteSample = true;
                if (sample < min) min = sample;
                if (sample > max) max = sample;
            }

            // 計測元が一時的に壊れていても、描画へ無限値を渡さない。
            if (!hasFiniteSample)
            {
                min = Min;
                max = Max;
                return;
            }

            // 全て同じ値だと高さ 0 になって線が消えるので、わずかに広げる。
            if (Mathf.Approximately(min, max))
            {
                min -= 0.5f;
                max += 0.5f;
            }
        }

        /// <summary>溜めた標本を捨てる。</summary>
        public void ClearSamples()
        {
            _samples.Clear();
            _accumulated = 0f;
        }
    }
}
