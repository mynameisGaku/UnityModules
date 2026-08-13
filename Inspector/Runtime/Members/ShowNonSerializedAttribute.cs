using System;

namespace Inspector
{
    /// <summary>
    /// 保存されないフィールドを Inspector に表示だけする（編集はできない）。
    /// <code>
    /// [ShowNonSerialized] private int _framesSinceHit;
    /// </code>
    /// <para>
    /// <c>[SerializeField]</c> を付けて見えるようにすると、その値まで保存されてしまう。
    /// 実行中にしか意味のない値を保存対象にすると、
    /// シーンやプレハブの差分が毎回汚れる。表示だけしたい場面のための属性。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ShowNonSerializedAttribute : InspectorAttribute
    {
    }

    /// <summary>
    /// プロパティの現在値を Inspector に表示だけする（編集はできない）。
    /// <code>
    /// [ShowNativeProperty] public int RemainingAmmo =&gt; _magazine - _fired;
    /// </code>
    /// <para>
    /// 計算で決まる値の確認に使う。get のあるプロパティが対象で、値の取得中に例外が出ても
    /// Inspector を落とさず、その旨を表示する。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ShowNativePropertyAttribute : InspectorAttribute
    {
    }
}
