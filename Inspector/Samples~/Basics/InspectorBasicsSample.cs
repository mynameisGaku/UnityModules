using UnityEngine;

// 見せたいのは属性の付け方であって挙動ではないので、読まれないフィールドがある。
#pragma warning disable 0414

namespace Inspector.Samples
{
    /// <summary>
    /// 収録している属性を 1 つのコンポーネントで見比べるためのサンプル。
    /// <para>
    /// 同梱シーンの設定済みオブジェクトを選び、Inspector でチェックや列挙を切り替えて確認する。
    /// </para>
    /// </summary>
    [AddComponentMenu("StudioGaku/Inspector Basics Sample")]
    public sealed class InspectorBasicsSample : MonoBehaviour
    {
        public enum Mode
        {
            Simple,
            Advanced,
            Expert,
        }

        [Title("基本", "この 2 つが、以下の表示を切り替える")]
        [SerializeField] private Mode _mode = Mode.Simple;
        [SerializeField] private bool _useOverride;

        // ------------------------------------------------------------------ 条件

        [Title("条件")]
        [InfoBox("上のチェックを外すと、次の 2 つの出方が変わる")]
        [ShowIf(nameof(_useOverride))]
        [Suffix("m/s")]
        [SerializeField] private float _shownWhenOverriding = 5f;

        [EnableIf(nameof(_useOverride))]
        [Indent]
        [SerializeField] private float _greyedOutWhenNotOverriding = 1f;

        [ShowIf(nameof(_mode), Mode.Advanced, Mode.Expert)]
        [SerializeField] private AnimationCurve _falloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [ReadOnly]
        [SerializeField] private string _generatedId = "(未生成)";

        [InlineButton(nameof(GenerateId), "生成")]
        [SerializeField] private string _idWithButton = string.Empty;

        // ------------------------------------------------------------------ グループ

        [BoxGroup("体力")]
        [MinValue(1)]
        [SerializeField] private int _maxHp = 100;

        [BoxGroup("体力")]
        [ProgressBar("現在値", MaxMember = nameof(_maxHp), Color = InspectorColor.Green)]
        [SerializeField] private float _hp = 60f;

        [TabGroup("設定", "見た目")]
        [SerializeField] private Color _tint = Color.white;

        [TabGroup("設定", "見た目")]
        [LabelText("不透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float _opacity = 1f;

        [TabGroup("設定", "挙動")]
        [SerializeField] private float _friction = 0.2f;

        [TabGroup("設定", "挙動")]
        [Tag]
        [SerializeField] private string _targetTag = "Player";

        [Foldout("上級者向け")]
        [HorizontalGroup("上級者向け/範囲")]
        [LabelWidth(32f)]
        [SerializeField] private float _min;

        [HorizontalGroup("上級者向け/範囲")]
        [LabelWidth(32f)]
        [SerializeField] private float _max = 1f;

        // ------------------------------------------------------------------ 検証

        [Title("検証", "空にしたり、2 のべき乗でない値を入れたりしてみる")]
        [Required("弾のプレハブを入れないと発射できない")]
        [AssetOnly]
        [SerializeField] private GameObject _projectile;

        [ValidateInput(nameof(IsPowerOfTwo), "2 のべき乗にすること")]
        [SerializeField] private int _textureSize = 256;

        [Dropdown(nameof(Qualities))]
        [SerializeField] private int _quality = 2;

        [HorizontalLine]
        [ResizableTextArea]
        [SerializeField] private string _notes = "行を足すと欄が伸びる。";

        // ------------------------------------------------------------------ 表示のみ

        [ShowNonSerialized] private int _framesSinceStart;

        [ShowNativeProperty] public float HpRatio => _maxHp == 0 ? 0f : _hp / _maxHp;

        [GUIColor(InspectorColor.Red)]
        [Button("体力を全快させる", EnableMode = ButtonEnableMode.PlayMode)]
        private void Heal() => _hp = _maxHp;

        [Button]
        private void ResetToDefaults()
        {
            _mode = Mode.Simple;
            _useOverride = false;
            _hp = _maxHp;
            _notes = string.Empty;
        }

        private static readonly int[] Qualities = { 0, 1, 2, 3 };

        private void Update() => _framesSinceStart++;

        private void GenerateId() => _idWithButton = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        private bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
    }
}
