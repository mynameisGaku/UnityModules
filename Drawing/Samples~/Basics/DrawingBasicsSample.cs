using UnityEngine;

namespace Drawing.Samples
{
    /// <summary>
    /// 描けるものを一通り出すサンプル。空の GameObject に付けて Play するだけで動く。
    /// </summary>
    [AddComponentMenu("StudioGaku/Drawing Basics Sample")]
    public sealed class DrawingBasicsSample : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _radius = 0.5f;
        [SerializeField] private bool _seeThroughWalls;

        private readonly Vector3[] _path = new Vector3[16];
        private float _pulse;
        private float _nextTimedMarkerAt;

        private void Update()
        {
            var origin = transform.position;
            var to = _target != null ? _target.position : origin + Vector3.forward * 3f;

            _pulse = Mathf.PingPong(Time.time, 1f);

            // 既定値をまとめて指定する。抜ければ元に戻る。
            using (Draw.Scope(depthTest: !_seeThroughWalls))
            {
                Draw.Axis(origin, transform.rotation);
                Draw.Arrow(origin, to, headSize: 0.3f, color: Color.yellow, thickness: 2f);

                Draw.Sphere(to, _radius, Color.cyan);
                Draw.Capsule(origin + Vector3.up * 0.5f, origin + Vector3.up * 1.5f, 0.4f, Color.green);
                Draw.Box(origin + Vector3.right * 2f, Vector3.one, Quaternion.Euler(0f, Time.time * 45f, 0f), Color.magenta);
                Draw.Circle(origin, 1f + _pulse, Vector3.up, new Color(1f, 1f, 1f, 0.4f));

                Draw.Text(to + Vector3.up * (_radius + 0.2f), $"距離 {Vector3.Distance(origin, to):0.00}");
            }

            DrawSpiral(origin);
            DrawTimedMarker(to);
        }

        /// <summary>折れ線。経路や履歴をそのまま渡せる。</summary>
        private void DrawSpiral(Vector3 origin)
        {
            for (var i = 0; i < _path.Length; i++)
            {
                var t = (float)i / _path.Length;
                var angle = t * Mathf.PI * 4f + Time.time;

                _path[i] = origin + new Vector3(Mathf.Cos(angle), t * 2f, Mathf.Sin(angle)) * 1.5f;
            }

            Draw.Path(_path, closed: false, color: new Color(1f, 0.6f, 0.2f), thickness: 3f);
        }

        /// <summary>2 秒ごとに 1 回だけ、持続時間つきの目印を積む。</summary>
        private void DrawTimedMarker(Vector3 position)
        {
            if (Time.unscaledTime < _nextTimedMarkerAt) return;

            _nextTimedMarkerAt = Time.unscaledTime + 2f;
            Draw.Point(position, 0.3f, Color.red, duration: 1f, thickness: 2f);
            Draw.Text(position, "1 秒残る目印", Color.red, duration: 1f);
        }
    }
}
