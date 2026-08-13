using System;

namespace Inspector
{
    /// <summary>
    /// 再生中だけ Inspector に出す。
    /// <para>
    /// 実行中の内部状態を覗くための表示（<see cref="ShowNativePropertyAttribute"/> など）に添えると、
    /// 編集中のインスペクタが散らからずに済む。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ShowInPlayModeAttribute : InspectorAttribute
    {
    }

    /// <summary>再生中は Inspector から隠す。編集中にだけ触ってよい設定に付ける。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class HideInPlayModeAttribute : InspectorAttribute
    {
    }
}
