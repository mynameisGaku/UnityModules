using System;
using Containers;

namespace DebugMenu
{
    /// <summary>
    /// ピン留めされた行だけを集めたページを作る。
    /// <para>
    /// 項目が数百になると、よく使う 5 個に辿り着くまでの手数が実用性を殺す。
    /// 留めた行を 1 ページに集めることで、そこだけは常に 1 手で届くようにする。
    /// </para>
    /// <para>
    /// 集めた行は<b>実体を借りている</b>ので、こちらで値を変えれば元のページでも変わる。
    /// 写しを作ると二重管理になるため、あえて同じ行を指している。
    /// </para>
    /// </summary>
    public sealed class DebugMenuFavorites
    {
        private readonly DebugPage _page;
        private uint _syncedVersion;

        /// <summary>ページ名を指定して作る。</summary>
        /// <param name="pageName">お気に入りページの名前。</param>
        public DebugMenuFavorites(string pageName = "Favorites")
        {
            _page = new DebugPage(pageName)
            {
                Description = "ピン留めした行がここに集まる。留め外しは対象の行で切り替える。",
            };
        }

        /// <summary>お気に入りを集めたページ。</summary>
        public DebugPage Page => _page;

        /// <summary>
        /// 留め外しがあれば組み直す。毎フレーム呼んでよい。
        /// <para>
        /// 版数が変わっていなければ何もしないので、走査の costs は留め外しの瞬間だけ。
        /// </para>
        /// </summary>
        /// <param name="menu">対象のメニュー。</param>
        /// <returns>組み直したなら true。</returns>
        public bool SyncIfDirty(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (_syncedVersion == DebugElement.FavoriteVersion) return false;

            Rebuild(menu);
            return true;
        }

        /// <summary>留め外しの有無に関わらず組み直す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public void Rebuild(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            // 見出しは所有物、その中の行は借り物。外す順を逆にすると、
            // 借りている行の Parent を消してしまう。
            DetachBorrowed();
            _page.Root.ClearChildren();

            // ページごとに見出しを立てる。どこから来た行かが分からないと、
            // 同じ名前の行が並んだときに区別できない。
            DebugPage lastPage = null;
            DebugElement currentGroup = null;

            menu.VisitAll((page, element) =>
            {
                if (!element.IsFavorite) return;
                if (ReferenceEquals(page, _page)) return;   // 自分自身は集めない

                if (!ReferenceEquals(page, lastPage))
                {
                    currentGroup = _page.Root.Add(new DebugGroup(page.Name));
                    lastPage = page;
                }

                // 実体を借りるだけ。所有を移すと元ページ側の経路が壊れる。
                currentGroup.AddBorrowed(element);
            });

            _page.Invalidate();
            _syncedVersion = DebugElement.FavoriteVersion;
        }

        /// <summary>
        /// 集めた行を親から外す。お気に入りページを捨てる前に呼ぶ。
        /// <para>
        /// 行の実体は元のページのものなので、外さないと親の付け替えが残る。
        /// </para>
        /// </summary>
        public void Detach()
        {
            DetachBorrowed();
            _page.Root.ClearChildren();
            _page.Invalidate();
        }

        /// <summary>見出しの中の借り物だけを外す。行の <c>Parent</c> は元のままになる。</summary>
        private void DetachBorrowed()
        {
            var groups = _page.Root.Children;
            for (var i = 0; i < groups.Count; i++) groups[i].ClearBorrowedChildren();
        }

        /// <summary>留められている行の数。</summary>
        public int Count
        {
            get
            {
                var total = 0;
                var groups = _page.Root.Children;
                for (var i = 0; i < groups.Count; i++) total += groups[i].Children.Count;
                return total;
            }
        }
    }
}
