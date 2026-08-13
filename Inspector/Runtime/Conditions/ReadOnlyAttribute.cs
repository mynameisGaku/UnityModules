using System;

namespace Inspector
{
    /// <summary>
    /// 値を見せるが編集させない。
    /// <code>
    /// [ReadOnly]
    /// [SerializeField] private int _generatedId;
    /// </code>
    /// <para>
    /// 実行時に決まる値や、ツールが書き込む値を Inspector で確認したいときに使う。
    /// <c>[SerializeField] private</c> のままだと編集できてしまい、
    /// うっかり手で変えた値が保存されてしまうのを防ぐ。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ReadOnlyAttribute : InspectorAttribute
    {
    }
}
