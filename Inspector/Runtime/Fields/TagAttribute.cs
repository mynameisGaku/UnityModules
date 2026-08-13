using System;

namespace Inspector
{
    /// <summary>
    /// <c>string</c> フィールドをタグの選択欄にする。
    /// <code>
    /// [Tag] [SerializeField] private string _targetTag = "Player";
    /// </code>
    /// <para>
    /// タグ名を手で打つと、綴り違いに実行時まで気付けない
    /// （<c>CompareTag</c> は存在しないタグを渡すと例外を投げる）。
    /// 選択式にしてしまえば、その事故は起きない。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TagAttribute : FieldDrawerAttribute
    {
    }

    /// <summary>
    /// レイヤーの選択欄にする。<c>int</c> ならレイヤー番号、<c>string</c> ならレイヤー名が入る。
    /// <code>
    /// [Layer] [SerializeField] private int _groundLayer;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class LayerAttribute : FieldDrawerAttribute
    {
    }

    /// <summary>
    /// スプライトの並び順レイヤーの選択欄にする。<c>string</c> なら名前、<c>int</c> なら ID が入る。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SortingLayerAttribute : FieldDrawerAttribute
    {
    }

    /// <summary>
    /// Build Settings に登録済みのシーンから選ぶ欄にする。
    /// <c>string</c> ならシーン名、<c>int</c> ならビルドインデックスが入る。
    /// <code>
    /// [Scene] [SerializeField] private string _nextScene;
    /// </code>
    /// <para>
    /// 未登録のシーン名は <c>SceneManager.LoadScene</c> で実行時に落ちる。
    /// 登録済みのものだけを候補にすることで、その組み合わせを作れなくする。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SceneAttribute : FieldDrawerAttribute
    {
    }
}
