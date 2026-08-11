using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 値と「何回変わったか」を持つコンテナ。イベントではなくバージョン番号で変更を検出する。
    /// <para>
    /// イベント方式の代わりにこれを選ぶ理由は 3 つとも Unity 特有の面倒に対応している：
    /// デリゲートの確保が無いので <b>GC が出ない</b>、購読していないので <b>解除漏れが原理的に起きない</b>、
    /// そして通知が発火順に依存しないので<b>1 フレームに何度変わってもコストが増えない</b>。
    /// </para>
    /// <para>
    /// 代償は、見に行かないと気づけないこと。毎フレーム回る UI 更新には向き、
    /// 稀にしか起きない事象の通知には <see cref="ReactiveProperty{T}"/> の方が向く。
    /// </para>
    /// <code>
    /// // 監視側は前回のバージョンを覚えておくだけ
    /// if (_health.HasChangedSince(ref _seenVersion)) _bar.fillAmount = _health.Value / _max;
    /// </code>
    /// </summary>
    public struct VersionedValue<T>
    {
        private T _value;
        private int _version;

        public VersionedValue(T value)
        {
            _value = value;
            _version = 1;
        }

        /// <summary>変更のたびに増える。初期状態は 0。</summary>
        public int Version => _version;

        /// <summary>
        /// 値。等値なら書いてもバージョンは進まないので、
        /// 「毎フレーム同じ値を代入する」コードが無駄な再描画を起こさない。
        /// </summary>
        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;

                _value = value;
                _version++;
            }
        }

        /// <summary>等値判定を飛ばして必ずバージョンを進める。参照型の中身を書き換えたときに使う。</summary>
        public void SetDirty(T value)
        {
            _value = value;
            _version++;
        }

        /// <summary>値はそのままでバージョンだけ進める。</summary>
        public void Invalidate() => _version++;

        /// <summary>
        /// <paramref name="seenVersion"/> より新しくなっていれば true を返し、同時に更新する。
        /// 監視側は int を 1 つ持つだけで済む。
        /// </summary>
        public bool HasChangedSince(ref int seenVersion)
        {
            if (seenVersion == _version) return false;

            seenVersion = _version;
            return true;
        }

        /// <summary>変化していれば値も受け取る版。</summary>
        public bool TryGetChanged(ref int seenVersion, out T value)
        {
            value = _value;
            return HasChangedSince(ref seenVersion);
        }

        public override string ToString() => $"{_value} (v{_version})";

        public static implicit operator T(VersionedValue<T> versioned) => versioned._value;
    }

    /// <summary>
    /// 複数の <see cref="VersionedValue{T}"/> をまとめて監視するための版数の集計器。
    /// 「どれか 1 つでも変わったら作り直す」処理に使う。
    /// </summary>
    public struct VersionWatcher
    {
        private int _accumulated;
        private int _seen;

        /// <summary>監視対象のバージョンを足し込む。1 フレームに必要な数だけ呼ぶ。</summary>
        public void Observe(int version) => _accumulated = unchecked(_accumulated * 31 + version);

        /// <summary>
        /// <see cref="Observe"/> を呼び終えたあとに 1 回呼ぶ。
        /// 前回と合計が変わっていれば true を返し、次回に備えて記録し直す。
        /// </summary>
        public bool Consume()
        {
            var changed = _accumulated != _seen;
            _seen = _accumulated;
            _accumulated = 0;
            return changed;
        }
    }
}
