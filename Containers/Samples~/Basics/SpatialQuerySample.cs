using Containers;
using Containers.Spatial;
using UnityEngine;

namespace Containers.Samples
{
    /// <summary>
    /// <see cref="SpatialHashGrid{T}"/> で群れの近傍問い合わせを行うサンプル。
    /// <para>
    /// 見どころは 2 つ。毎フレーム全個体の位置を <see cref="SpatialHashGrid{T}.Update"/> に流しても、
    /// セルをまたがない限り安いこと。そして結果の受け皿に <see cref="TempList{T}"/> を使うことで、
    /// 定常状態では 1 バイトも確保しないこと。
    /// </para>
    /// </summary>
    public sealed class SpatialQuerySample : MonoBehaviour
    {
        [SerializeField] private int _agentCount = 300;
        [SerializeField] private float _areaSize = 60f;
        [SerializeField] private float _neighbourRadius = 4f;
        [SerializeField] private float _speed = 3f;

        /// <summary>セルの一辺。問い合わせ半径と同程度が最も効率が良い。</summary>
        [SerializeField] private float _cellSize = 4f;

        private SpatialHashGrid<int> _grid;
        private Vector3[] _positions;
        private Vector3[] _velocities;
        private int[] _neighbourCounts;

        private void Start()
        {
            _grid = new SpatialHashGrid<int>(_cellSize);
            _positions = new Vector3[_agentCount];
            _velocities = new Vector3[_agentCount];
            _neighbourCounts = new int[_agentCount];

            for (var i = 0; i < _agentCount; i++)
            {
                _positions[i] = new Vector3(
                    Random.Range(0f, _areaSize), 0f, Random.Range(0f, _areaSize));
                _velocities[i] = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;

                _grid.Insert(i, _positions[i]);
            }
        }

        private void Update()
        {
            Move();
            CountNeighbours();
        }

        private void Move()
        {
            var delta = Time.deltaTime * _speed;

            for (var i = 0; i < _agentCount; i++)
            {
                _positions[i] += _velocities[i] * delta;

                // 領域の端で跳ね返す。
                if (_positions[i].x < 0f || _positions[i].x > _areaSize) _velocities[i].x = -_velocities[i].x;
                if (_positions[i].z < 0f || _positions[i].z > _areaSize) _velocities[i].z = -_velocities[i].z;

                _grid.Update(i, _positions[i]);
            }
        }

        private void CountNeighbours()
        {
            // スコープを抜けるとリストはプールに戻るので、毎フレーム呼んでも確保が増えない。
            using var neighbours = TempList<int>.Rent();

            for (var i = 0; i < _agentCount; i++)
            {
                neighbours.List.Clear();
                _grid.QueryRadiusExact(_positions[i], _neighbourRadius, id => _positions[id], neighbours.List);

                // 自分自身が必ず 1 件入るので引いておく。
                _neighbourCounts[i] = neighbours.List.Count - 1;
            }
        }

        private void OnDrawGizmos()
        {
            if (_positions == null) return;

            for (var i = 0; i < _positions.Length; i++)
            {
                // 近傍が多いほど赤く。
                Gizmos.color = Color.Lerp(Color.cyan, Color.red, Mathf.Clamp01(_neighbourCounts[i] / 8f));
                Gizmos.DrawSphere(_positions[i], 0.3f);
            }
        }
    }
}
