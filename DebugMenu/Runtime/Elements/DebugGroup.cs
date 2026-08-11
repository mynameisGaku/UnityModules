namespace DebugMenu
{
    /// <summary>
    /// 子行を束ねる見出しの行。値は持たない。
    /// <para>
    /// 基底の <see cref="DebugElement"/> をそのまま使っても同じ働きをするが、
    /// 組み立てるコードで意図が読めるように名前を付けてある。
    /// </para>
    /// </summary>
    public sealed class DebugGroup : DebugElement
    {
        /// <summary>見出し名を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="expanded">最初から開いた状態にするか。</param>
        public DebugGroup(string label, bool expanded = true) : base(label)
        {
            IsExpanded = expanded;
            MarkerVisibility = DebugMarkerVisibility.Always;
        }

        /// <summary>保存対象にしない。値を持たないため。</summary>
        public override bool IsSaveable => false;
    }

    /// <summary>
    /// 見た目を区切るだけの行。選択もできず値も持たない。
    /// <para>項目が増えたページで、関係のない塊の間に空白を作るために使う。</para>
    /// </summary>
    public sealed class DebugSeparator : DebugElement
    {
        /// <summary>見出し文字を指定して作る。空にすると線だけになる。</summary>
        /// <param name="label">区切りに添える文字。</param>
        public DebugSeparator(string label = null) : base(label)
        {
            IsExpandable = false;
            MarkerVisibility = DebugMarkerVisibility.Never;
        }

        /// <summary>保存対象にしない。</summary>
        public override bool IsSaveable => false;

        /// <summary>決定しても何も起きない。</summary>
        public override void OnDecide() { }
    }
}
