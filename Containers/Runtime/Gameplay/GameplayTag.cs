using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Containers.Gameplay
{
    /// <summary>
    /// <c>Ability.Fire.Burn</c> のような階層タグ 1 個。
    /// <para>
    /// 文字列そのものではなく、登録時に割り振った整数で持つ。比較も辞書引きも整数で済み、
    /// 階層の判定も文字列の <c>StartsWith</c> ではなく祖先集合の照合で行う。
    /// </para>
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>, ISerializationCallbackReceiver
    {
        public static readonly GameplayTag None = default;

        [SerializeField] private string _name;

        [NonSerialized] private int _id;
        [NonSerialized] private bool _resolved;

        private GameplayTag(string name, int id)
        {
            _name = name;
            _id = id;
            _resolved = true;
        }

        /// <summary>タグを登録（済みなら取得）する。祖先も同時に登録される。</summary>
        public static GameplayTag Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return None;
            return new GameplayTag(name, GameplayTagRegistry.Register(name));
        }

        /// <summary>完全なタグ名。</summary>
        public string Name => _name;

        internal int Id
        {
            get
            {
                if (!_resolved)
                {
                    _id = string.IsNullOrEmpty(_name) ? 0 : GameplayTagRegistry.Register(_name);
                    _resolved = true;
                }

                return _id;
            }
        }

        public bool IsValid => Id != 0;

        /// <summary>階層の深さ。<c>A.B.C</c> なら 3。</summary>
        public int Depth => GameplayTagRegistry.DepthOf(Id);

        /// <summary>1 つ上の親。<c>A.B.C</c> なら <c>A.B</c>。最上位なら <see cref="None"/>。</summary>
        public GameplayTag Parent
        {
            get
            {
                var parentId = GameplayTagRegistry.ParentOf(Id);
                return parentId == 0 ? None : new GameplayTag(GameplayTagRegistry.NameOf(parentId), parentId);
            }
        }

        /// <summary>
        /// <paramref name="other"/> がこのタグ自身か、その子孫かどうか。
        /// <c>Ability.Fire</c> は <c>Ability.Fire.Burn</c> を含む。
        /// </summary>
        public bool Matches(GameplayTag other) => GameplayTagRegistry.IsSelfOrDescendant(other.Id, Id);

        public bool Equals(GameplayTag other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => string.IsNullOrEmpty(_name) ? "<none>" : _name;

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Id == b.Id;
        public static bool operator !=(GameplayTag a, GameplayTag b) => a.Id != b.Id;

        public static implicit operator GameplayTag(string name) => Get(name);

        public void OnBeforeSerialize() { }

        /// <summary>デシリアライズ直後は id が無効なので、次に使うときに引き直す。</summary>
        public void OnAfterDeserialize()
        {
            _resolved = false;
            _id = 0;
        }
    }

    /// <summary>
    /// タグ名と整数 id の対応、および親子関係を保持する表。
    /// <para>
    /// タグを登録すると祖先も自動的に登録される —— <c>A.B.C</c> を入れると <c>A</c> と <c>A.B</c> も
    /// 表に入る。これにより「<c>A</c> にマッチするか」の判定が、文字列操作なしの整数比較の連鎖になる。
    /// </para>
    /// </summary>
    public static class GameplayTagRegistry
    {
        private sealed class Entry
        {
            public string Name;
            public int ParentId;
            public int Depth;
        }

        private static readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly List<Entry> Entries = new List<Entry> { null };   // index 0 は「無効」
        private static readonly object Gate = new object();

        /// <summary>登録されているタグの総数（自動登録された祖先を含む）。</summary>
        public static int Count
        {
            get
            {
                lock (Gate) return Entries.Count - 1;
            }
        }

        public static int Register(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;

            lock (Gate)
            {
                if (Ids.TryGetValue(name, out var existing)) return existing;

                // 親を先に登録する。再帰は階層の深さぶんだけで、実用上は数段。
                var separator = name.LastIndexOf('.');
                var parentId = separator > 0 ? RegisterLocked(name.Substring(0, separator)) : 0;

                return AddLocked(name, parentId);
            }
        }

        private static int RegisterLocked(string name)
        {
            if (Ids.TryGetValue(name, out var existing)) return existing;

            var separator = name.LastIndexOf('.');
            var parentId = separator > 0 ? RegisterLocked(name.Substring(0, separator)) : 0;
            return AddLocked(name, parentId);
        }

        private static int AddLocked(string name, int parentId)
        {
            var id = Entries.Count;
            Entries.Add(new Entry
            {
                Name = name,
                ParentId = parentId,
                Depth = parentId == 0 ? 1 : Entries[parentId].Depth + 1
            });

            Ids.Add(name, id);
            return id;
        }

        internal static string NameOf(int id)
        {
            lock (Gate) return (uint)id < (uint)Entries.Count && Entries[id] != null ? Entries[id].Name : null;
        }

        internal static int ParentOf(int id)
        {
            lock (Gate) return (uint)id < (uint)Entries.Count && Entries[id] != null ? Entries[id].ParentId : 0;
        }

        internal static int DepthOf(int id)
        {
            lock (Gate) return (uint)id < (uint)Entries.Count && Entries[id] != null ? Entries[id].Depth : 0;
        }

        /// <summary><paramref name="candidate"/> が <paramref name="ancestor"/> 自身かその子孫か。</summary>
        internal static bool IsSelfOrDescendant(int candidate, int ancestor)
        {
            if (candidate == 0 || ancestor == 0) return false;

            lock (Gate)
            {
                var current = candidate;
                while (current != 0)
                {
                    if (current == ancestor) return true;
                    current = Entries[current].ParentId;
                }
            }

            return false;
        }

        /// <summary>登録済みの全タグ名。エディタのピッカー用。</summary>
        public static void AllTagNames(List<string> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            lock (Gate)
            {
                for (var i = 1; i < Entries.Count; i++) results.Add(Entries[i].Name);
            }
        }
    }

    /// <summary>
    /// タグの集合。状態異常、アビリティの分類、免疫、AI の条件記述。
    /// <para>
    /// 単なる <c>HashSet&lt;string&gt;</c> との差は<b>階層で問い合わせられること</b>。
    /// 「<c>Status.Debuff</c> を持っているか」に対し、<c>Status.Debuff.Poison</c> しか
    /// 持っていなくても true を返す。これができると、
    /// 「デバフを全て解除」「炎系に免疫」といったルールがタグ 1 つで書けるようになる。
    /// </para>
    /// <para>
    /// 判定を速くするため、追加時に祖先も一緒に入れておく（<c>A.B.C</c> を足すと <c>A</c> と
    /// <c>A.B</c> も暗黙に含まれる）。おかげで <see cref="HasTag(GameplayTag)"/> は集合の 1 回引きで済む。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class GameplayTagContainer : ISerializationCallbackReceiver, IEnumerable<GameplayTag>
    {
        [SerializeField] private List<string> _tagNames = new List<string>();

        /// <summary>明示的に追加されたタグ。</summary>
        [NonSerialized] private readonly HashSet<int> _explicit = new HashSet<int>();

        /// <summary>祖先を含む全タグ。問い合わせはこちらを見る。</summary>
        [NonSerialized] private readonly HashSet<int> _withAncestors = new HashSet<int>();

        /// <summary>
        /// 実行時 API 経由で中身が変わったか（詳細は
        /// <see cref="SerializableDictionary{TKey,TValue}"/> の同名フィールドを参照）。
        /// </summary>
        [NonSerialized] private bool _runtimeDirty;

        /// <summary>タグが増減したときに発火する。</summary>
        public event Action<GameplayTag, bool> Changed;

        /// <summary>明示的に持っているタグの数。祖先は数えない。</summary>
        public int Count => _explicit.Count;

        public bool Add(GameplayTag tag)
        {
            if (!tag.IsValid || !_explicit.Add(tag.Id)) return false;

            for (var current = tag; current.IsValid; current = current.Parent) _withAncestors.Add(current.Id);

            _runtimeDirty = true;
            Changed?.Invoke(tag, true);
            return true;
        }

        public bool Add(string tagName) => Add(GameplayTag.Get(tagName));

        /// <summary>
        /// タグを外す。祖先の集合は残っている明示タグから作り直す ——
        /// 他のタグが同じ祖先を要求している可能性があるため。
        /// </summary>
        public bool Remove(GameplayTag tag)
        {
            if (!_explicit.Remove(tag.Id)) return false;

            RebuildAncestors();
            _runtimeDirty = true;
            Changed?.Invoke(tag, false);
            return true;
        }

        /// <summary>
        /// <paramref name="tag"/> かその子孫を持っているか。
        /// <c>Status.Debuff</c> で問い合わせると <c>Status.Debuff.Poison</c> にも当たる。
        /// </summary>
        public bool HasTag(GameplayTag tag) => tag.IsValid && _withAncestors.Contains(tag.Id);

        public bool HasTag(string tagName) => HasTag(GameplayTag.Get(tagName));

        /// <summary>階層を無視した完全一致。</summary>
        public bool HasTagExact(GameplayTag tag) => _explicit.Contains(tag.Id);

        /// <summary><paramref name="tags"/> のどれか 1 つでも持っているか。</summary>
        public bool HasAny(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return false;

            for (var i = 0; i < tags.Count; i++)
            {
                if (HasTag(tags[i])) return true;
            }

            return false;
        }

        /// <summary><paramref name="tags"/> を全て持っているか。</summary>
        public bool HasAll(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return true;

            for (var i = 0; i < tags.Count; i++)
            {
                if (!HasTag(tags[i])) return false;
            }

            return true;
        }

        /// <summary><paramref name="parent"/> の子孫にあたるタグを全て外す。「デバフ全解除」用。</summary>
        public int RemoveMatching(GameplayTag parent)
        {
            if (!parent.IsValid) return 0;

            using var doomed = TempList<int>.Rent();
            foreach (var id in _explicit)
            {
                if (GameplayTagRegistry.IsSelfOrDescendant(id, parent.Id)) doomed.List.Add(id);
            }

            for (var i = 0; i < doomed.List.Count; i++) _explicit.Remove(doomed.List[i]);

            if (doomed.List.Count > 0)
            {
                RebuildAncestors();
                _runtimeDirty = true;

                // Add / Remove と同じく通知する。ここで黙っていると、
                // Changed を見て表示を更新している側が消えたタグを出し続ける。
                if (Changed != null)
                {
                    for (var i = 0; i < doomed.List.Count; i++)
                    {
                        Changed.Invoke(GameplayTag.Get(GameplayTagRegistry.NameOf(doomed.List[i])), false);
                    }
                }
            }

            return doomed.List.Count;
        }

        /// <summary>全て捨てる。持っていたタグそれぞれについて <see cref="Changed"/> が発火する。</summary>
        public void Clear()
        {
            if (_explicit.Count == 0) return;

            if (Changed != null)
            {
                using var removed = TempList<int>.Rent();
                foreach (var id in _explicit) removed.List.Add(id);

                _explicit.Clear();
                _withAncestors.Clear();
                _runtimeDirty = true;

                for (var i = 0; i < removed.List.Count; i++)
                {
                    Changed.Invoke(GameplayTag.Get(GameplayTagRegistry.NameOf(removed.List[i])), false);
                }

                return;
            }

            _explicit.Clear();
            _withAncestors.Clear();
            _runtimeDirty = true;
        }

        private void RebuildAncestors()
        {
            _withAncestors.Clear();

            foreach (var id in _explicit)
            {
                var current = id;
                while (current != 0)
                {
                    _withAncestors.Add(current);
                    current = GameplayTagRegistry.ParentOf(current);
                }
            }
        }

        /// <summary>保持しているタグ名を、Unity が保存するリストに書き戻す。</summary>
        public void OnBeforeSerialize()
        {
            if (!_runtimeDirty) return;
            _runtimeDirty = false;

            _tagNames.Clear();
            foreach (var id in _explicit) _tagNames.Add(GameplayTagRegistry.NameOf(id));
        }

        /// <summary>リストからタグ集合を組み直し、祖先も展開する。</summary>
        public void OnAfterDeserialize()
        {
            _explicit.Clear();
            _withAncestors.Clear();
            _runtimeDirty = false;

            for (var i = 0; i < _tagNames.Count; i++)
            {
                var tag = GameplayTag.Get(_tagNames[i]);
                if (!tag.IsValid) continue;

                _explicit.Add(tag.Id);
                for (var current = tag; current.IsValid; current = current.Parent) _withAncestors.Add(current.Id);
            }
        }

        public IEnumerator<GameplayTag> GetEnumerator()
        {
            foreach (var id in _explicit) yield return GameplayTag.Get(GameplayTagRegistry.NameOf(id));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
