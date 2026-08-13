using System.Collections.Generic;
using UnityEngine;

namespace Drawing
{
    /// <summary>
    /// デバッグ用の線と文字を、どこからでも 1 行で描く。
    /// <code>
    /// Draw.Line(transform.position, target.position, Color.red, duration: 2f);
    /// Draw.Sphere(hit.point, 0.2f);
    /// Draw.Text(head.position, $"HP {hp}");
    /// </code>
    /// <para>
    /// 置くものも初期化も要らない。最初に呼んだ時点で必要なものが用意される。
    /// <c>OnDrawGizmos</c> の中でなくても、コルーチンの途中でも、物理コールバックの中でも呼べる。
    /// すべてのメソッドとプロパティーは Unity のメインスレッドから使う。
    /// 線分は 16384 本、文字は 1024 件を上限とし、超えた分は描画せず種類ごとに一度だけ警告する。
    /// </para>
    /// <para>
    /// <b>リリースビルドでは呼び出しごと消える。</b>
    /// エディタと開発ビルドでのみ中身が残るよう <see cref="System.Diagnostics.ConditionalAttribute"/> を付けてあるので、
    /// 製品版のために <c>#if</c> で囲って回る必要がない。引数の計算ごと消えるため、
    /// <c>Draw.Text(p, $"{Heavy()}")</c> のような書き方をしても製品版の負荷にならない。
    /// </para>
    /// </summary>
    public static class Draw
    {
        private const string InEditor = "UNITY_EDITOR";
        private const string InDevelopmentBuild = "DEVELOPMENT_BUILD";

        private static readonly List<Segment> Scratch = new List<Segment>(128);
        private static UnityEngine.Color _color = UnityEngine.Color.white;
        private static float _duration;
        private static float _thickness = 1f;
        private static bool _depthTest = true;
        private static Camera _camera;

        /// <summary>色を省いたときに使う色。</summary>
        public static Color Color
        {
            get => _color;
            set => _color = IsFinite(value) ? value : UnityEngine.Color.white;
        }

        /// <summary>
        /// 持続時間を省いたときに使う秒数。0 なら最初の描画処理まで保持する。
        /// 文字は再描画時にカメラが無ければ破棄する。負数や非有限値は 0 として扱う。
        /// </summary>
        public static float Duration
        {
            get => _duration;
            set => _duration = SanitizeDuration(value);
        }

        /// <summary>太さを省いたときに使うピクセル数。0 以下や非有限値は 1 として扱う。</summary>
        public static float Thickness
        {
            get => _thickness;
            set => _thickness = SanitizeThickness(value);
        }

        /// <summary>手前のものに隠れるか。<c>false</c> にすると壁の向こうでも見える。</summary>
        public static bool DepthTest
        {
            get => _depthTest;
            set => _depthTest = value;
        }

        /// <summary>
        /// 文字を画面のどこに出すか決めるためのカメラ。
        /// 指定しなければ <c>Camera.main</c> を使う。
        /// </summary>
        public static Camera Camera
        {
            get => _camera != null ? _camera : UnityEngine.Camera.main;
            set => _camera = value;
        }

        /// <summary>Play Mode を始めるたびに、前回の静的設定と作業中の線分を既定状態へ戻す。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Scratch.Clear();
            _color = UnityEngine.Color.white;
            _duration = 0f;
            _thickness = 1f;
            _depthTest = true;
            _camera = null;
        }

        /// <summary>
        /// 色や持続時間をまとめて指定する。抜けるときに元へ戻る。
        /// <code>
        /// using (Draw.Scope(Color.yellow, duration: 3f))
        /// {
        ///     Draw.Box(bounds.center, bounds.size);
        ///     Draw.Text(bounds.center, "接地判定");
        /// }
        /// </code>
        /// <para>
        /// 同じ設定を何度も引数で書かずに済む。構造体を返すので確保は起きない。
        /// なお、このメソッド自体はリリースビルドでも残る（値を返すため消せない）が、
        /// 中で描いている呼び出しのほうは消えるので、実質的な負荷は無い。
        /// </para>
        /// </summary>
        /// <param name="color">スコープ内の既定色。<c>null</c> なら現在の <see cref="Draw.Color"/> を維持する。</param>
        /// <param name="duration">スコープ内の既定の持続秒数。<c>null</c> なら現在の <see cref="Draw.Duration"/> を維持する。</param>
        /// <param name="thickness">スコープ内の既定の太さ（ピクセル）。<c>null</c> なら現在の <see cref="Draw.Thickness"/> を維持する。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら現在の <see cref="Draw.DepthTest"/> を維持する。</param>
        /// <returns><c>Dispose</c> 時に、指定前の色・持続時間・太さ・深度設定を復元するスコープ。</returns>
        public static DrawScope Scope(
            Color? color = null,
            float? duration = null,
            float? thickness = null,
            bool? depthTest = null)
        {
            var scope = new DrawScope(Color, Duration, Thickness, DepthTest);

            if (color.HasValue) Color = color.Value;
            if (duration.HasValue) Duration = duration.Value;
            if (thickness.HasValue) Thickness = thickness.Value;
            if (depthTest.HasValue) DepthTest = depthTest.Value;

            return scope;
        }

        /// <summary>2 点を結ぶ線。</summary>
        /// <param name="a">線の始点。</param>
        /// <param name="b">線の終点。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Line(Vector3 a, Vector3 b, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            Scratch.Add(new Segment(a, b));
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>始点と向きで表す線。<c>Physics.Raycast</c> に渡した値をそのまま渡せる。</summary>
        /// <param name="origin">線の始点。</param>
        /// <param name="direction">始点から終点までの向きと長さ。終点は <paramref name="origin"/> + <paramref name="direction"/>。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Ray(Vector3 origin, Vector3 direction, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            Scratch.Add(new Segment(origin, origin + direction));
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>向きの分かる矢印。</summary>
        /// <param name="from">矢印の始点。</param>
        /// <param name="to">矢印の先端。</param>
        /// <param name="headSize">矢じりの長さ（ワールド単位）。0 以下なら軸の線だけを描く。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Arrow(Vector3 from, Vector3 to, float headSize = 0.25f, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Arrow(Scratch, from, to, headSize);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>折れ線。経路や履歴をそのまま渡す。</summary>
        /// <param name="points">順番に結ぶ点。<c>null</c> または 2 点未満なら描かない。</param>
        /// <param name="closed"><c>true</c> なら末尾と先頭も結ぶ。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Path(IReadOnlyList<Vector3> points, bool closed = false, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Path(Scratch, points, closed);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>直方体の枠。</summary>
        /// <param name="center">直方体の中心。</param>
        /// <param name="size">各軸方向の大きさ（ワールド単位）。</param>
        /// <param name="rotation">中心を基準にした回転。<c>null</c> なら回転しない。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Box(Vector3 center, Vector3 size, Quaternion? rotation = null, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Box(Scratch, center, size, rotation ?? Quaternion.identity);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary><see cref="UnityEngine.Bounds"/> の枠。<c>Renderer.bounds</c> や <c>Collider.bounds</c> をそのまま渡せる。</summary>
        /// <param name="bounds">描く中心と大きさを持つ、軸に平行な境界。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Bounds(Bounds bounds, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Box(Scratch, bounds.center, bounds.size, Quaternion.identity);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>球の輪郭。直交する 3 つの円で表す。</summary>
        /// <param name="center">球の中心。</param>
        /// <param name="radius">球の半径（ワールド単位）。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Sphere(Vector3 center, float radius, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Sphere(Scratch, center, radius);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>円。<paramref name="normal"/> に垂直な面に描く。</summary>
        /// <param name="center">円の中心。</param>
        /// <param name="radius">円の半径（ワールド単位）。</param>
        /// <param name="normal">円を描く面の法線。長さ 0 なら上向きとして扱う。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Circle(Vector3 center, float radius, Vector3 normal, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Circle(Scratch, center, radius, normal);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>
        /// カプセル。<paramref name="start"/> と <paramref name="end"/> は半球の中心で、
        /// <c>Physics.CapsuleCast</c> に渡す点と同じ意味。
        /// </summary>
        /// <param name="start">一方の半球の中心。</param>
        /// <param name="end">もう一方の半球の中心。<paramref name="start"/> と同じなら球として描く。</param>
        /// <param name="radius">カプセルの半径（ワールド単位）。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Capsule(Vector3 start, Vector3 end, float radius, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Capsule(Scratch, start, end, radius);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>点の位置を示す小さな十字。</summary>
        /// <param name="position">十字の中心。</param>
        /// <param name="size">各軸に沿った十字の全長（ワールド単位）。</param>
        /// <param name="color">描画色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Point(Vector3 position, float size = 0.1f, Color? color = null, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            DrawShapes.Cross(Scratch, position, size);
            Emit(color, duration, thickness, depthTest);
        }

        /// <summary>
        /// 姿勢を示す 3 軸。赤が右、緑が上、青が前。
        /// <c>color</c> は使わない（軸ごとに色が決まっているため）。
        /// </summary>
        /// <param name="position">3 軸の原点。</param>
        /// <param name="rotation">3 軸の向き。</param>
        /// <param name="size">各軸の長さ（ワールド単位）。</param>
        /// <param name="duration">描画を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        /// <param name="thickness">画面上の太さ（ピクセル）。<c>null</c> なら <see cref="Draw.Thickness"/> を使い、0 以下や非有限値は 1 とする。</param>
        /// <param name="depthTest">手前の物体に隠すか。<c>null</c> なら <see cref="Draw.DepthTest"/> を使う。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Axis(Vector3 position, Quaternion rotation, float size = 0.5f, float? duration = null, float? thickness = null, bool? depthTest = null)
        {
            Line(position, position + rotation * Vector3.right * size, UnityEngine.Color.red, duration, thickness, depthTest);
            Line(position, position + rotation * Vector3.up * size, UnityEngine.Color.green, duration, thickness, depthTest);
            Line(position, position + rotation * Vector3.forward * size, UnityEngine.Color.blue, duration, thickness, depthTest);
        }

        /// <summary>
        /// ワールド座標の位置に文字を出す。
        /// <para>
        /// 出る場所は <see cref="Camera"/> から見た画面上の位置。ゲームビューにのみ出る。
        /// 持続時間が 0 の文字は、再描画時にカメラが無ければ後から表示せず破棄する。
        /// </para>
        /// </summary>
        /// <param name="position">文字を置くワールド座標。</param>
        /// <param name="text">表示する文字列。<c>null</c> または空文字なら描かない。</param>
        /// <param name="color">文字色。<c>null</c> なら <see cref="Draw.Color"/> を使う。</param>
        /// <param name="duration">文字を保持する秒数。<c>null</c> なら <see cref="Draw.Duration"/> を使い、負数や非有限値は 0 とする。</param>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Text(Vector3 position, string text, Color? color = null, float? duration = null)
        {
            var renderer = DrawRenderer.Ensure();
            if (renderer == null) return;

            var resolvedDuration = SanitizeDuration(duration ?? Duration);

            renderer.Buffer.AddLabel(position, text, SanitizeColor(color ?? Color), Time.unscaledTime + resolvedDuration, Time.frameCount, waitForFirstSubmission: resolvedDuration <= 0f);
        }

        /// <summary>今出ているものを全部消す。持続時間を長く指定しすぎたときに使う。</summary>
        [System.Diagnostics.Conditional(InEditor), System.Diagnostics.Conditional(InDevelopmentBuild)]
        public static void Clear() => DrawRenderer.ClearAll();

        /// <summary>
        /// <see cref="Scratch"/> に溜めた線分を送り出す。
        /// <para>
        /// 形ごとに一時的なリストを作ると、毎フレーム確保が走る。
        /// 使い回しの 1 本だけを持ち、送り終えたら空にする。
        /// </para>
        /// </summary>
        private static void Emit(Color? color, float? duration, float? thickness, bool? depthTest)
        {
            var renderer = DrawRenderer.Ensure();

            if (renderer == null)
            {
                Scratch.Clear();
                return;
            }

            var resolvedColor = SanitizeColor(color ?? Color);
            var resolvedThickness = SanitizeThickness(thickness ?? Thickness);
            var resolvedDepthTest = depthTest ?? DepthTest;
            var resolvedDuration = SanitizeDuration(duration ?? Duration);
            var expiresAt = Time.unscaledTime + resolvedDuration;
            var frame = Time.frameCount;

            for (var i = 0; i < Scratch.Count; i++)
            {
                var segment = Scratch[i];
                renderer.Buffer.AddLine(segment.A, segment.B, resolvedColor, resolvedThickness, resolvedDepthTest, expiresAt, frame, waitForFirstSubmission: resolvedDuration <= 0f);
            }

            Scratch.Clear();
        }

        /// <summary>持続時間を、期限計算へ安全に使える 0 以上の有限値へそろえる。</summary>
        private static float SanitizeDuration(float value) => IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        /// <summary>太さを、メッシュ計算へ安全に使える正の有限値へそろえる。</summary>
        private static float SanitizeThickness(float value) => IsFinite(value) && value > 0f ? value : 1f;

        /// <summary>色に非有限値が混じる場合は、シェーダへ渡さず白へ戻す。</summary>
        private static Color SanitizeColor(Color value) => IsFinite(value) ? value : UnityEngine.Color.white;

        /// <summary>色の全成分が有限値か確かめる。</summary>
        private static bool IsFinite(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
        }

        /// <summary>数値が NaN でも無限大でもないか確かめる。</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
