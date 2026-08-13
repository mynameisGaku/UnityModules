using System;
using System.Collections.Generic;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// 後始末が必要なものを集めておき、まとめて片付けるための袋。
    /// <para>
    /// 購読解除の書き忘れは Unity で最も多い漏れ方をする。発行側がハンドラを掴み、
    /// ハンドラがクロージャを掴み、クロージャが破棄済みの MonoBehaviour を掴み、
    /// オブジェクトグラフ全体がシーンより長生きする。<b>購読と解除を同じ 1 文に書ける</b>ようにすれば、
    /// 片方だけ忘れることが構造的にできなくなる。
    /// </para>
    /// <code>
    /// private readonly DisposableBag _bag = new DisposableBag();
    ///
    /// void OnEnable()  => _bag.AddAction(() => _health.Changed -= OnHealthChanged,
    ///                                    () => _health.Changed += OnHealthChanged);
    /// void OnDisable() => _bag.Dispose();
    /// </code>
    /// </summary>
    public sealed class DisposableBag : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();

        /// <summary>既に片付け済みか。</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>入っている後始末の数。</summary>
        public int Count => _items.Count;

        /// <summary>
        /// 後始末を登録する。片付け済みの袋に入れた場合はその場で実行する ——
        /// 遅れて登録されたものが静かに生き残るのを防ぐため。
        /// </summary>
        /// <param name="disposable">登録するもの。</param>
        public T Add<T>(T disposable) where T : IDisposable
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));

            if (IsDisposed)
            {
                disposable.Dispose();
                return disposable;
            }

            _items.Add(disposable);
            return disposable;
        }

        /// <summary>後始末をコールバックとして登録する。</summary>
        /// <param name="onDispose">片付け時に実行する処理。</param>
        public void AddAction(Action onDispose)
        {
            if (onDispose == null) throw new ArgumentNullException(nameof(onDispose));
            Add(new ActionDisposable(onDispose));
        }

        /// <summary>
        /// <paramref name="setUp"/> をその場で実行し、<paramref name="tearDown"/> を片付け時に実行する。
        /// 購読と解除が 1 文に収まる。
        /// </summary>
        /// <param name="tearDown">片付け時の処理。</param>
        /// <param name="setUp">今すぐ実行する処理。</param>
        public void AddAction(Action tearDown, Action setUp)
        {
            if (setUp == null) throw new ArgumentNullException(nameof(setUp));

            setUp();
            AddAction(tearDown);
        }

        /// <summary>片付け時に Unity オブジェクトを破棄するよう登録する。既に破棄済みでも安全。</summary>
        /// <param name="target">破棄する対象。</param>
        public T AddDestroy<T>(T target) where T : UnityEngine.Object
        {
            if (target == null) return target;

            AddAction(() =>
            {
                if (target != null) UnityEngine.Object.Destroy(target);
            });

            return target;
        }

        /// <summary>
        /// 中身を全て片付けて空にするが、袋そのものは再利用できる状態のままにする。
        /// <c>OnEnable</c> / <c>OnDisable</c> の対に紐づける使い方向け。
        /// </summary>
        public void Clear()
        {
            DisposeItems();
            IsDisposed = false;
        }

        /// <summary>中身を全て片付け、以後の追加を即時実行に切り替える。</summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            DisposeItems();
        }

        private void DisposeItems()
        {
            // 登録と逆順に片付ける。依存のある後始末でも順序が合う。
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                try
                {
                    _items[i].Dispose();
                }
                catch (Exception exception)
                {
                    // 1 つの失敗で残りの後始末が止まらないようにする。
                    Debug.LogException(exception);
                }
            }

            _items.Clear();
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _action;

            public ActionDisposable(Action action) => _action = action;

            public void Dispose()
            {
                var action = _action;
                _action = null;      // 2 回 Dispose されても 2 回実行しない
                action?.Invoke();
            }
        }
    }

    /// <summary>コールバックを <see cref="IDisposable"/> として包む小道具。</summary>
    public static class Disposable
    {
        /// <summary>片付け時に <paramref name="onDispose"/> を 1 回だけ実行するものを作る。</summary>
        /// <param name="onDispose">片付け時の処理。</param>
        public static IDisposable Create(Action onDispose)
        {
            if (onDispose == null) throw new ArgumentNullException(nameof(onDispose));
            return new Anonymous(onDispose);
        }

        /// <summary>何もしない <see cref="IDisposable"/>。</summary>
        public static IDisposable Empty { get; } = new Anonymous(null);

        private sealed class Anonymous : IDisposable
        {
            private Action _action;

            public Anonymous(Action action) => _action = action;

            public void Dispose()
            {
                var action = _action;
                _action = null;
                action?.Invoke();
            }
        }
    }
}
