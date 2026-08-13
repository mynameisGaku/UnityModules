using System;

namespace Inspector
{
    /// <summary>
    /// アセット参照の下にサムネイルを出す。
    /// <code>
    /// [ShowAssetPreview(96, 96)]
    /// [SerializeField] private Sprite _icon;
    /// </code>
    /// <para>
    /// 名前だけ見ても中身が分からない資産（アイコン、マテリアル、プレハブ）を
    /// 取り違えないようにするためのもの。
    /// プレビューがまだ生成されていない間は何も出ない（Unity が非同期で作るため）。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ShowAssetPreviewAttribute : DecoratorAttribute
    {
        public ShowAssetPreviewAttribute(int width = 64, int height = 64)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        /// <inheritdoc/>
        public override DecoratorPosition Position => DecoratorPosition.After;
    }
}
