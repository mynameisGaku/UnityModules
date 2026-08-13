using System;

namespace Inspector
{
    /// <summary>
    /// プロジェクト内のアセットしか受け付けないことを示し、シーン上のオブジェクトが入っていたら知らせる。
    /// <code>
    /// [AssetOnly]
    /// [SerializeField] private GameObject _enemyPrefab;
    /// </code>
    /// <para>
    /// プレハブを入れるつもりの欄にシーン上のインスタンスが入っていると、
    /// そのプレハブを生成しているつもりで「シーンにいる個体」を複製してしまう。
    /// しかもプレハブ資産に保存するとその参照は切れる。実行するまで気付きにくい類の事故なので、
    /// Inspector の時点で見えるようにする。
    /// </para>
    /// <para>
    /// 値を勝手に消したりはしない。誤りを知らせるだけで、直すのは人に任せる。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class AssetOnlyAttribute : ValidatorAttribute
    {
    }

    /// <summary>
    /// シーン上のオブジェクトしか受け付けないことを示し、アセットが入っていたら知らせる。
    /// <see cref="AssetOnlyAttribute"/> の裏返し。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SceneObjectOnlyAttribute : ValidatorAttribute
    {
    }
}
