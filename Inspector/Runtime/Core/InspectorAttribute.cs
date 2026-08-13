using System;

namespace Inspector
{
    /// <summary>
    /// このモジュールが解釈する属性すべての基底。
    /// <para>
    /// <b><see cref="UnityEngine.PropertyAttribute"/> をあえて継承していない。</b>
    /// 継承すると Unity の描画解決に割り込んでしまい、1 フィールドに付けられる
    /// <c>PropertyDrawer</c> が 1 つだけという制限に巻き込まれる。
    /// たとえば <c>[ShowIf]</c> を付けただけで <c>Optional&lt;T&gt;</c> 専用の描画が消える、
    /// といった事故が起きる。ここでは素の <see cref="Attribute"/> のままにしておき、
    /// 解釈は <c>Inspector.Editor</c> 側の Editor が一括で行う。
    /// </para>
    /// </summary>
    public abstract class InspectorAttribute : Attribute
    {
    }

    /// <summary>
    /// 表示するか、触れるようにするかを条件で切り替える属性の基底。
    /// <para>
    /// 条件の書き方は 3 通り。
    /// </para>
    /// <code>
    /// [ShowIf(nameof(_useOverride))]                       // bool メンバーが true
    /// [ShowIf(nameof(_mode), Mode.Advanced, Mode.Expert)]  // 値がいずれかと一致
    /// [ShowIf(ConditionOperator.And, nameof(_a), "!" + nameof(_b))]  // 複数条件（"!" で反転）
    /// </code>
    /// <para>
    /// 参照先はフィールド・プロパティ・引数なしメソッドのいずれでもよく、
    /// private でも基底クラスのものでも引ける。
    /// </para>
    /// </summary>
    public abstract class ConditionAttribute : InspectorAttribute
    {
        private static readonly string[] EmptyMembers = new string[0];
        private static readonly object[] EmptyValues = new object[0];

        /// <summary>単一メンバーを見る形。<paramref name="values"/> を省くと bool として扱う。</summary>
        /// <param name="member">条件として参照するメンバーの名前。</param>
        /// <param name="values">成立とみなす値。空ならメンバーを bool として判定する。</param>
        protected ConditionAttribute(string member, params object[] values)
        {
            Members = member == null ? EmptyMembers : new[] { member };
            Values = values ?? EmptyValues;
            Operator = ConditionOperator.And;
        }

        /// <summary>複数メンバーを論理演算でまとめる形。各メンバーは bool として扱う。</summary>
        /// <param name="conditionOperator">各メンバーの判定結果をまとめる論理演算。</param>
        /// <param name="members">条件として参照するメンバー名の並び。</param>
        protected ConditionAttribute(ConditionOperator conditionOperator, params string[] members)
        {
            Members = members ?? EmptyMembers;
            Values = EmptyValues;
            Operator = conditionOperator;
        }

        /// <summary>条件が成立したときに何が起きるか。</summary>
        public abstract ConditionEffect Effect { get; }

        /// <summary>見に行くメンバー名。先頭の <c>!</c> はそのメンバーの結果を反転させる。</summary>
        public string[] Members { get; }

        /// <summary>比較する値。空なら「メンバーが bool として true か」を見る。</summary>
        public object[] Values { get; }

        /// <summary><see cref="Members"/> が複数あるときのまとめ方。</summary>
        public ConditionOperator Operator { get; }
    }

    /// <summary>フィールドの前後に付け足す表示（見出し・注意書き・区切り線など）の基底。</summary>
    public abstract class DecoratorAttribute : InspectorAttribute
    {
        /// <summary>フィールド本体より前に描くか、後に描くか。</summary>
        public abstract DecoratorPosition Position { get; }
    }

    /// <summary>
    /// フィールドの値そのものの描き方を差し替える属性の基底。
    /// <para>
    /// 1 つのメンバーに 2 つ以上付けると描き方が決まらないため、
    /// Editor 側は最初の 1 つを使い、残りはエラーとして画面に出す。
    /// </para>
    /// </summary>
    public abstract class FieldDrawerAttribute : InspectorAttribute
    {
    }

    /// <summary>ラベルや色など、描き方の細部だけを変える属性の基底。</summary>
    public abstract class StyleAttribute : InspectorAttribute
    {
    }

    /// <summary>値が妥当かを描画後に検査し、駄目なら警告を出す属性の基底。</summary>
    public abstract class ValidatorAttribute : InspectorAttribute
    {
    }

    /// <summary>
    /// メンバーをまとまりに入れる属性の基底。
    /// <para>
    /// <see cref="Path"/> は <c>"表示/戦闘"</c> のように <c>/</c> 区切りで入れ子にできる。
    /// 1 つのメンバーに種類の違うグループ属性を複数付けた場合、
    /// <b>一番深いパスが所属先</b>になり、浅いものは途中の階層の見た目を決めるだけの宣言として働く。
    /// </para>
    /// <code>
    /// [Foldout("上級者向け")]                 // 「上級者向け」は折りたたみ
    /// [BoxGroup("上級者向け/物理")]           // その中の「物理」は枠囲み。所属先はこちら
    /// [SerializeField] private float _drag;
    /// </code>
    /// </summary>
    public abstract class GroupAttribute : InspectorAttribute
    {
        /// <summary>指定したパスを所属先とするグループ属性を作る。</summary>
        /// <param name="path"><c>/</c> 区切りで入れ子にできるグループパス。</param>
        protected GroupAttribute(string path) => Path = path;

        /// <summary><c>/</c> 区切りのグループパス。</summary>
        public string Path { get; }

        /// <summary>このパスの末尾の階層をどう描くか。</summary>
        public abstract GroupKind Kind { get; }
    }

    /// <summary><see cref="ConditionAttribute"/> の条件が成立したときの効果。</summary>
    public enum ConditionEffect
    {
        /// <summary>表示する（不成立なら消える）。</summary>
        Show,

        /// <summary>隠す（不成立なら出る）。</summary>
        Hide,

        /// <summary>編集できるようにする（不成立なら灰色）。</summary>
        Enable,

        /// <summary>灰色にする（不成立なら編集できる）。</summary>
        Disable,
    }

    /// <summary>複数条件のまとめ方。</summary>
    public enum ConditionOperator
    {
        /// <summary>全部成立したときだけ成立。</summary>
        And,

        /// <summary>どれか 1 つ成立すれば成立。</summary>
        Or,
    }

    /// <summary>装飾をフィールドの前に描くか後に描くか。</summary>
    public enum DecoratorPosition
    {
        /// <summary>対象メンバーより前に描く。</summary>
        Before,

        /// <summary>対象メンバーより後に描く。</summary>
        After,
    }

    /// <summary>グループの描き方。</summary>
    public enum GroupKind
    {
        /// <summary>折りたたみ。</summary>
        Foldout,

        /// <summary>ヘルプボックス風の枠と見出し。</summary>
        Box,

        /// <summary>子を横並びにする。</summary>
        Horizontal,

        /// <summary>子をタブとして切り替える入れ物。</summary>
        Tabs,

        /// <summary><see cref="Tabs"/> の下に置かれる 1 枚のタブ。</summary>
        TabPage,
    }
}
