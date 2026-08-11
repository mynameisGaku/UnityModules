using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Containers
{
    /// <summary>
    /// シーンごとに区切って登録し、そのシーンがアンロードされたら自動的に中身を捨てる登録簿。
    /// <para>
    /// シーンより長生きする static なキャッシュは、静かなバグの源になる ——
    /// 前のレベルのスポーン地点、前の面を指したままの対応表、破棄済みオブジェクトを掴んだエントリ。
    /// 登録をオブジェクトが属するシーンに紐づけてしまえば、後始末は自動になり、
    /// 各システムが覚えておく必要が無くなる。
    /// </para>
    /// </summary>
    public sealed class SceneScopedRegistry<T> where T : class
    {
        // キーは int ではなく SceneHandle。Unity 6.5 で SceneHandle → int の暗黙変換が廃止された。
        private readonly Dictionary<SceneHandle, FastList<T>> _byScene = new Dictionary<SceneHandle, FastList<T>>();

        private bool _subscribed;

        /// <summary>シーンのアンロードで捨てられたエントリを通知する。</summary>
        public event Action<T> Evicted;

        /// <summary>登録のあるシーンの数。</summary>
        public int SceneCount => _byScene.Count;

        /// <summary>コンポーネントが属するシーンに紐づけて登録する。</summary>
        /// <param name="owner">所属シーンを決めるコンポーネント。</param>
        /// <param name="item">登録するもの。</param>
        public void Register(Component owner, T item)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Register(owner.gameObject.scene, item);
        }

        /// <summary>シーンを明示して登録する。</summary>
        /// <param name="scene">紐づけるシーン。</param>
        /// <param name="item">登録するもの。</param>
        public void Register(Scene scene, T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            EnsureSubscribed();

            if (!_byScene.TryGetValue(scene.handle, out var list))
            {
                list = new FastList<T>();
                _byScene[scene.handle] = list;
            }

            if (!list.Contains(item)) list.Add(item);
        }

        /// <summary>コンポーネントの所属シーンから登録を外す。</summary>
        /// <param name="owner">所属シーンを決めるコンポーネント。</param>
        /// <param name="item">外すもの。</param>
        public bool Unregister(Component owner, T item) =>
            owner != null && Unregister(owner.gameObject.scene, item);

        /// <summary>シーンを明示して登録を外す。</summary>
        /// <param name="scene">対象のシーン。</param>
        /// <param name="item">外すもの。</param>
        public bool Unregister(Scene scene, T item)
        {
            if (!_byScene.TryGetValue(scene.handle, out var list)) return false;
            if (!list.RemoveSwapBack(item)) return false;

            if (list.Count == 0) _byScene.Remove(scene.handle);
            return true;
        }

        /// <summary>
        /// 1 つのシーンに登録されているもの。無ければ空のリスト。
        /// <para>
        /// 未登録時は毎回新しいリストを返す。共有インスタンスを配ると、
        /// 呼び出し側の書き換えが他の全シーンの結果に漏れる。
        /// </para>
        /// </summary>
        /// <param name="scene">対象のシーン。</param>
        public FastList<T> ForScene(Scene scene) =>
            _byScene.TryGetValue(scene.handle, out var list) ? list : new FastList<T>();

        /// <summary>読み込み中の全シーンの登録を <paramref name="results"/> に集める。</summary>
        /// <param name="results">追記先。</param>
        public void All(FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            foreach (var pair in _byScene) results.AddRange(pair.Value);
        }

        /// <summary>全シーン合計の登録数。</summary>
        public int TotalCount()
        {
            var total = 0;
            foreach (var pair in _byScene) total += pair.Value.Count;
            return total;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            if (Evicted != null)
            {
                foreach (var pair in _byScene)
                {
                    for (var i = 0; i < pair.Value.Count; i++) Evicted.Invoke(pair.Value[i]);
                }
            }

            _byScene.Clear();
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _subscribed = true;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!_byScene.TryGetValue(scene.handle, out var list)) return;

            if (Evicted != null)
            {
                for (var i = 0; i < list.Count; i++) Evicted.Invoke(list[i]);
            }

            _byScene.Remove(scene.handle);
        }

        /// <summary>シーンアンロードの購読を解除する。この登録簿自体を捨てるときに呼ぶ。</summary>
        public void Detach()
        {
            if (!_subscribed) return;

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _subscribed = false;
        }
    }
}
