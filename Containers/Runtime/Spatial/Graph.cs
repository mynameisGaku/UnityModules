using System;
using System.Collections.Generic;

namespace Containers.Spatial
{
    /// <summary>
    /// 隣接リストで持つ重み付き有向グラフ。必要になるたび書き直されがちな探索も同梱してある。
    /// <para>
    /// 経路探索が分かりやすい用途だが、同じ構造で空間と無関係な問いにも答えられる ——
    /// スキルツリーの前提条件、クラフトの依存、会話の分岐、ステージの解放順。
    /// <see cref="TopologicalSort"/> はそのためにあり、循環があれば<b>ハングせずに報告する</b>。
    /// </para>
    /// <para>無向グラフは双方向の辺を張ればよい（<see cref="AddEdgeBoth"/>）。</para>
    /// </summary>
    public sealed class Graph<TNode>
    {
        /// <summary>ある頂点から伸びる 1 本の辺。</summary>
        public readonly struct Edge
        {
            /// <summary>辺の行き先。</summary>
            public readonly TNode Target;

            /// <summary>辺のコスト。</summary>
            public readonly float Weight;

            /// <summary>行き先とコストを指定して作る。</summary>
            /// <param name="target">行き先。</param>
            /// <param name="weight">コスト。</param>
            public Edge(TNode target, float weight)
            {
                Target = target;
                Weight = weight;
            }
        }

        private readonly Dictionary<TNode, FastList<Edge>> _adjacency;
        private readonly IEqualityComparer<TNode> _comparer;

        /// <summary>頂点の比較方法を指定して作る。</summary>
        /// <param name="comparer">頂点の同一性の判定方法。</param>
        public Graph(IEqualityComparer<TNode> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TNode>.Default;
            _adjacency = new Dictionary<TNode, FastList<Edge>>(_comparer);
        }

        /// <summary>頂点の数。</summary>
        public int NodeCount => _adjacency.Count;

        /// <summary>登録されている頂点の一覧。</summary>
        public Dictionary<TNode, FastList<Edge>>.KeyCollection Nodes => _adjacency.Keys;

        /// <summary>辺を持たない頂点を追加する。既にあれば false。</summary>
        /// <param name="node">追加する頂点。</param>
        public bool AddNode(TNode node)
        {
            if (_adjacency.ContainsKey(node)) return false;

            _adjacency.Add(node, new FastList<Edge>());
            return true;
        }

        /// <summary>頂点が登録されているか。</summary>
        /// <param name="node">探す頂点。</param>
        public bool ContainsNode(TNode node) => _adjacency.ContainsKey(node);

        /// <summary>片方向の辺を張る。未登録の端点は自動的に追加される。</summary>
        /// <param name="from">始点。</param>
        /// <param name="to">終点。</param>
        /// <param name="weight">コスト。負は不可。</param>
        public void AddEdge(TNode from, TNode to, float weight = 1f)
        {
            if (weight < 0f) throw new ArgumentOutOfRangeException(nameof(weight), "Dijkstra は非負のコストを前提にしている。");

            AddNode(from);
            AddNode(to);
            _adjacency[from].Add(new Edge(to, weight));
        }

        /// <summary>両方向に辺を張り、実質的に無向にする。</summary>
        /// <param name="a">一方の端点。</param>
        /// <param name="b">もう一方の端点。</param>
        /// <param name="weight">コスト。</param>
        public void AddEdgeBoth(TNode a, TNode b, float weight = 1f)
        {
            AddEdge(a, b, weight);
            AddEdge(b, a, weight);
        }

        /// <summary>辺を取り除く。</summary>
        /// <param name="from">始点。</param>
        /// <param name="to">終点。</param>
        public bool RemoveEdge(TNode from, TNode to)
        {
            if (!_adjacency.TryGetValue(from, out var edges)) return false;

            for (var i = 0; i < edges.Count; i++)
            {
                if (!_comparer.Equals(edges[i].Target, to)) continue;

                edges.RemoveAtSwapBack(i);
                return true;
            }

            return false;
        }

        /// <summary>頂点と、そこへ入る辺を全て取り除く。</summary>
        /// <param name="node">取り除く頂点。</param>
        public bool RemoveNode(TNode node)
        {
            if (!_adjacency.Remove(node)) return false;

            foreach (var pair in _adjacency)
            {
                var edges = pair.Value;
                for (var i = edges.Count - 1; i >= 0; i--)
                {
                    if (_comparer.Equals(edges[i].Target, node)) edges.RemoveAtSwapBack(i);
                }
            }

            return true;
        }

        /// <summary>
        /// 頂点から出る辺。未登録の頂点なら空のリスト。
        /// <para>
        /// 未登録時は毎回新しいリストを返す。共有の空リストを配ると、
        /// 呼び出し側がうっかり書き換えたときにプロセス全体が汚染される。
        /// 未登録が頻繁で確保を避けたいなら <see cref="ContainsNode"/> で先に弾くこと。
        /// </para>
        /// </summary>
        /// <param name="node">対象の頂点。</param>
        public FastList<Edge> EdgesFrom(TNode node) =>
            _adjacency.TryGetValue(node, out var edges) ? edges : new FastList<Edge>();

        /// <summary>全て捨てる。</summary>
        public void Clear() => _adjacency.Clear();

        // ── 探索 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 幅優先の訪問順。コストが一様なら、これがそのまま最短の歩数順にもなる。
        /// </summary>
        /// <param name="start">開始頂点。</param>
        /// <param name="results">追記先。</param>
        public void BreadthFirst(TNode start, FastList<TNode> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!_adjacency.ContainsKey(start)) return;

            var visited = new HashSet<TNode>(_comparer) { start };
            var frontier = new Deque<TNode>();
            frontier.PushBack(start);

            while (frontier.Count > 0)
            {
                var node = frontier.PopFront();
                results.Add(node);

                var edges = _adjacency[node];
                for (var i = 0; i < edges.Count; i++)
                {
                    var next = edges[i].Target;
                    if (!visited.Add(next)) continue;

                    frontier.PushBack(next);
                }
            }
        }

        /// <summary>深さ優先の訪問順。再帰ではなくスタックで回すので、深いグラフでも落ちない。</summary>
        /// <param name="start">開始頂点。</param>
        /// <param name="results">追記先。</param>
        public void DepthFirst(TNode start, FastList<TNode> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!_adjacency.ContainsKey(start)) return;

            var visited = new HashSet<TNode>(_comparer);
            var stack = new Stack<TNode>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!visited.Add(node)) continue;

                results.Add(node);

                var edges = _adjacency[node];
                for (var i = edges.Count - 1; i >= 0; i--) stack.Push(edges[i].Target);
            }
        }

        /// <summary>
        /// コスト最小の経路を求める。<see cref="IndexedPriorityQueue{TElement,TPriority}"/> を使うので、
        /// 各頂点はフロンティアに 1 つだけ入る（辺の数だけ重複しない）。
        /// </summary>
        /// <param name="start">開始頂点。</param>
        /// <param name="goal">目標頂点。</param>
        /// <param name="path">経路の書き込み先。</param>
        /// <param name="cost">経路の総コスト。</param>
        public bool TryFindPath(TNode start, TNode goal, FastList<TNode> path, out float cost) =>
            TryFindPath(start, goal, path, null, out cost);

        /// <summary>
        /// <paramref name="heuristic"/> があれば A*、無ければ Dijkstra として探索する。
        /// <para>
        /// ヒューリスティックは<b>残りコストを過大評価してはいけない</b>。過大評価すると
        /// 見つかる経路が最小コストでなくなる。空間的なグラフなら直線距離が安全な選択。
        /// </para>
        /// </summary>
        /// <param name="start">開始頂点。</param>
        /// <param name="goal">目標頂点。</param>
        /// <param name="path">経路の書き込み先。</param>
        /// <param name="heuristic">残りコストの見積り。null なら Dijkstra。</param>
        /// <param name="cost">経路の総コスト。</param>
        public bool TryFindPath(TNode start, TNode goal, FastList<TNode> path, Func<TNode, TNode, float> heuristic, out float cost)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            cost = 0f;
            if (!_adjacency.ContainsKey(start) || !_adjacency.ContainsKey(goal)) return false;

            var costSoFar = new Dictionary<TNode, float>(_comparer) { [start] = 0f };
            var cameFrom = new Dictionary<TNode, TNode>(_comparer);
            var frontier = new IndexedPriorityQueue<TNode, float>(elementComparer: _comparer);

            frontier.Enqueue(start, heuristic?.Invoke(start, goal) ?? 0f);

            while (frontier.TryDequeue(out var current, out _))
            {
                if (_comparer.Equals(current, goal))
                {
                    cost = costSoFar[goal];
                    Reconstruct(cameFrom, start, goal, path);
                    return true;
                }

                var currentCost = costSoFar[current];
                var edges = _adjacency[current];

                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    var nextCost = currentCost + edge.Weight;

                    if (costSoFar.TryGetValue(edge.Target, out var knownCost) && nextCost >= knownCost) continue;

                    costSoFar[edge.Target] = nextCost;
                    cameFrom[edge.Target] = current;

                    var priority = nextCost + (heuristic?.Invoke(edge.Target, goal) ?? 0f);
                    if (!frontier.TryUpdatePriority(edge.Target, priority)) frontier.Enqueue(edge.Target, priority);
                }
            }

            return false;
        }

        /// <summary>開始頂点から到達可能な全頂点への最小コスト。</summary>
        /// <param name="start">開始頂点。</param>
        public Dictionary<TNode, float> ShortestDistances(TNode start)
        {
            var distances = new Dictionary<TNode, float>(_comparer);
            if (!_adjacency.ContainsKey(start)) return distances;

            distances[start] = 0f;
            var frontier = new IndexedPriorityQueue<TNode, float>(elementComparer: _comparer);
            frontier.Enqueue(start, 0f);

            while (frontier.TryDequeue(out var current, out var currentCost))
            {
                var edges = _adjacency[current];
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    var nextCost = currentCost + edge.Weight;

                    if (distances.TryGetValue(edge.Target, out var known) && nextCost >= known) continue;

                    distances[edge.Target] = nextCost;
                    if (!frontier.TryUpdatePriority(edge.Target, nextCost)) frontier.Enqueue(edge.Target, nextCost);
                }
            }

            return distances;
        }

        /// <summary>
        /// 全ての辺が前を向くように頂点を並べる —— 依存が依存元より先に来る順序。
        /// 循環があって並べられない場合は false を返す。データを書いた人に伝えるべきなのは
        /// たいていその事実そのものなので、黙って部分結果を返さない。
        /// </summary>
        /// <param name="results">並べた結果の書き込み先。</param>
        public bool TopologicalSort(FastList<TNode> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var inDegree = new Dictionary<TNode, int>(_comparer);
            foreach (var pair in _adjacency) inDegree[pair.Key] = 0;

            foreach (var pair in _adjacency)
            {
                var edges = pair.Value;
                for (var i = 0; i < edges.Count; i++) inDegree[edges[i].Target]++;
            }

            var ready = new Deque<TNode>();
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0) ready.PushBack(pair.Key);
            }

            while (ready.Count > 0)
            {
                var node = ready.PopFront();
                results.Add(node);

                var edges = _adjacency[node];
                for (var i = 0; i < edges.Count; i++)
                {
                    var target = edges[i].Target;
                    if (--inDegree[target] == 0) ready.PushBack(target);
                }
            }

            // 残った頂点は依存が解決していない ＝ 循環がある。
            return results.Count == _adjacency.Count;
        }

        /// <summary>開始頂点から到達可能な頂点の集合。開始頂点自身も含む。</summary>
        /// <param name="start">開始頂点。</param>
        public HashSet<TNode> ConnectedComponent(TNode start)
        {
            var component = new HashSet<TNode>(_comparer);
            using var order = TempList<TNode>.Rent();

            BreadthFirst(start, order.List);
            for (var i = 0; i < order.List.Count; i++) component.Add(order.List[i]);
            return component;
        }

        private void Reconstruct(Dictionary<TNode, TNode> cameFrom, TNode start, TNode goal, FastList<TNode> path)
        {
            path.Clear();

            var current = goal;
            path.Add(current);

            while (!_comparer.Equals(current, start))
            {
                if (!cameFrom.TryGetValue(current, out current)) break;
                path.Add(current);
            }

            path.Reverse();
        }
    }
}
