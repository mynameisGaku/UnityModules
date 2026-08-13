using System;

namespace Inspector
{
    /// <summary>
    /// メソッドを Inspector のボタンとして出す。
    /// <code>
    /// [Button("経路を焼き直す")]
    /// private void RebakePath() { ... }
    ///
    /// [Button(EnableMode = ButtonEnableMode.PlayMode)]
    /// private void Respawn() { ... }
    /// </code>
    /// <para>
    /// 呼ぶメソッドは引数なしであること。static でも private でもよい。
    /// 引数付きのメソッドはボタンにできず、その旨を Inspector 上に出す
    /// （引数を入力させる欄まで作ると、値の保存場所が無く扱いが難しくなるため）。
    /// </para>
    /// <para>
    /// 複数のオブジェクトを同時に選んでいる場合は、選択中の全部に対して呼ぶ。
    /// 呼び出し前に <c>Undo</c> に記録するので、編集中に押しても取り消せる。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ButtonAttribute : InspectorAttribute
    {
        /// <param name="label">ボタンの文言。省略するとメソッド名から作る。</param>
        public ButtonAttribute(string label = null) => Label = label;

        public string Label { get; }

        /// <summary>いつ押せるようにするか。</summary>
        public ButtonEnableMode EnableMode { get; set; } = ButtonEnableMode.Always;

        /// <summary>ボタンの高さ。</summary>
        public float Height { get; set; } = 22f;
    }

    /// <summary><see cref="ButtonAttribute"/> を押せるようにする条件。</summary>
    public enum ButtonEnableMode
    {
        /// <summary>いつでも押せる。</summary>
        Always,

        /// <summary>編集中だけ押せる。シーンを組み立てるための操作向け。</summary>
        EditMode,

        /// <summary>再生中だけ押せる。実行中の状態を前提にする操作向け。</summary>
        PlayMode,
    }
}
