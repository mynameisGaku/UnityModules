using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drawing
{
    /// <summary>
    /// 溜まった線と文字を実際に画面へ出す常駐オブジェクト。
    /// <para>
    /// <see cref="Draw"/> を最初に呼んだときに勝手に生まれる。利用側がシーンに何かを置く必要はない。
    /// ヒエラルキーには出さず、シーンにも保存しない。
    /// </para>
    /// <para>
    /// 描画は <see cref="Graphics.RenderMesh"/> にメッシュを 1 枚渡すだけにしている。
    /// URP ではカメラの描画後コールバック（<c>Camera.onPostRender</c>）が呼ばれないため
    /// <c>GL</c> で直接描く方法を避け、URP の描画経路へ載せている。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class DrawRenderer : MonoBehaviour
    {
        private const string ShaderPath = "StudioGakuDrawLines";
        private const int ZTestLessEqual = 4;
        private const int ZTestAlways = 8;

        private static DrawRenderer _instance;
        private static bool _quitting;

        private readonly List<Vector3> _positions = new List<Vector3>(1024);
        private readonly List<Vector4> _others = new List<Vector4>(1024);
        private readonly List<Color> _colors = new List<Color>(1024);
        private readonly List<int> _indices = new List<int>(1536);

        /// <summary>文字ごとの一時確保を避けるため、表示内容だけ差し替えて使う。</summary>
        private readonly GUIContent _labelContent = new GUIContent();

        private Mesh _depthTestedMesh;
        private Mesh _overlayMesh;
        private Material _depthTestedMaterial;
        private Material _overlayMaterial;
        private GUIStyle _labelStyle;
        private bool _warnedLineOverflow;
        private bool _warnedLabelOverflow;
        private bool _warnedMissingShader;

        /// <summary>溜まっているもの。</summary>
        public DrawBuffer Buffer { get; } = new DrawBuffer();

        /// <summary>
        /// 常駐オブジェクトを用意して返す。すでにあればそれを返す。
        /// <para>
        /// 終了処理の最中に作ると破棄されないオブジェクトが残るので、そのときは <c>null</c> を返す。
        /// </para>
        /// </summary>
        public static DrawRenderer Ensure()
        {
            if (_instance != null) return _instance;
            if (!Application.isPlaying) return null;
            if (_quitting) return null;

            var host = new GameObject("[Drawing]")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            // 編集中に呼ぶと警告になるうえ、意味も無い（シーンは切り替わらない）。
            if (Application.isPlaying) DontDestroyOnLoad(host);

            _instance = host.AddComponent<DrawRenderer>();
            return _instance;
        }

        /// <summary>Play Mode を始めるたびに、前回の終了状態と破棄済み参照を戻す。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _quitting = false;
        }

        /// <summary>今あるものを全部消す。</summary>
        public static void ClearAll()
        {
            if (_instance != null) _instance.Buffer.Clear();
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

            DestroyIfNeeded(_depthTestedMesh);
            DestroyIfNeeded(_overlayMesh);
            DestroyIfNeeded(_depthTestedMaterial);
            DestroyIfNeeded(_overlayMaterial);
        }

        private static void DestroyIfNeeded(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void LateUpdate()
        {
            // 先に寿命の切れたものを落とす。1 フレームだけの線は、積まれたフレームのうちは残るので
            // 「積んだ直後に消えて一度も描かれない」ことはない。
            Buffer.Purge(Time.unscaledTime, Time.frameCount);

            if (Buffer.LineOverflowed && !_warnedLineOverflow)
            {
                _warnedLineOverflow = true;
                Debug.LogWarning(
                    $"[Drawing] 溜まった線が上限（{Buffer.LineCapacity}）に達したので、以降を捨てた。" +
                    " duration を付けたまま毎フレーム呼んでいないか確認する。");
            }

            if (Buffer.LabelOverflowed && !_warnedLabelOverflow)
            {
                _warnedLabelOverflow = true;
                Debug.LogWarning(
                    $"[Drawing] 溜まった文字が上限（{Buffer.LabelCapacity}）に達したので、以降を捨てた。" +
                    " duration を付けたまま毎フレーム呼んでいないか確認する。");
            }

            if (!EnsureResources()) return;

            Submit(_depthTestedMesh, _depthTestedMaterial, depthTest: true);
            Submit(_overlayMesh, _overlayMaterial, depthTest: false);
        }

        private void Submit(Mesh mesh, Material material, bool depthTest)
        {
            if (!Build(mesh, depthTest)) return;

            var parameters = new RenderParams(material)
            {
                // 線はどこにでも伸びる。狭い境界を渡すと、カメラの外と判断されて丸ごと消える。
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            Graphics.RenderMesh(parameters, mesh, 0, Matrix4x4.identity);
            Buffer.MarkLinesSubmitted(depthTest);
        }

        /// <summary>
        /// 線 1 本につき四角形 1 枚を組む。
        /// <para>
        /// 頂点には端点の座標だけを入れ、太さぶんの広がりはシェーダに任せる。
        /// ここで広げてしまうと、シーンビューとゲームビューのように
        /// カメラが 2 つ以上あるときに片方で向きが狂う。
        /// </para>
        /// </summary>
        private bool Build(Mesh mesh, bool depthTest)
        {
            _positions.Clear();
            _others.Clear();
            _colors.Clear();
            _indices.Clear();

            var lines = Buffer.Lines;

            // Linear 色空間では、画面に出る直前に linear → sRGB の変換が入る。
            // 利用側が渡してくる Color は sRGB のつもりの値なので、そのまま流すと
            // 変換ぶんだけ明るく出る。先に linear へ落としておくと、指定した色で出る。
            var toLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.DepthTest != depthTest) continue;

                var color = toLinear ? line.Color.linear : line.Color;

                // 下限は 0.75（幅 1.5 ピクセル）。ちょうど 1 ピクセル幅にすると、
                // 四角形が画素の中心をまたいだ場所で塗られず、線が虫食いになる。
                // アンチエイリアスの有無に関係なく途切れないよう、少しだけ広げておく。
                var half = Mathf.Max(0.75f, line.Thickness * 0.5f);
                var start = _positions.Count;

                _positions.Add(line.A);
                _positions.Add(line.A);
                _positions.Add(line.B);
                _positions.Add(line.B);

                _others.Add(new Vector4(line.B.x, line.B.y, line.B.z, half));
                _others.Add(new Vector4(line.B.x, line.B.y, line.B.z, -half));
                _others.Add(new Vector4(line.A.x, line.A.y, line.A.z, -half));
                _others.Add(new Vector4(line.A.x, line.A.y, line.A.z, half));

                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);

                _indices.Add(start);
                _indices.Add(start + 1);
                _indices.Add(start + 2);
                _indices.Add(start);
                _indices.Add(start + 2);
                _indices.Add(start + 3);
            }

            mesh.Clear();

            if (_indices.Count == 0) return false;

            mesh.indexFormat = _positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_positions);
            mesh.SetUVs(0, _others);
            mesh.SetColors(_colors);
            mesh.SetIndices(_indices, MeshTopology.Triangles, 0, calculateBounds: false);

            // 自前で広い境界を入れておく。頂点だけから計算した境界だと、
            // 太さぶんの広がりが入らずに端が切れることがある。
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e7f);
            return true;
        }

        private bool EnsureResources()
        {
            if (_depthTestedMaterial != null) return true;

            var shader = Resources.Load<Shader>(ShaderPath);

            if (shader == null)
            {
                if (_warnedMissingShader) return false;

                _warnedMissingShader = true;
                Debug.LogError(
                    $"[Drawing] シェーダ '{ShaderPath}' を読み込めない。" +
                    " Runtime/Resources/ の中身が欠けていないか確認する。");
                return false;
            }

            _depthTestedMesh = NewMesh("Drawing Lines (Depth)");
            _overlayMesh = NewMesh("Drawing Lines (Overlay)");

            _depthTestedMaterial = NewMaterial(shader, ZTestLessEqual);
            _overlayMaterial = NewMaterial(shader, ZTestAlways);
            return true;
        }

        private static Mesh NewMesh(string name)
        {
            return new Mesh
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,

                // 毎フレーム全部作り直す。書き換え前提だと Unity 側の扱いが軽くなる。
                indexFormat = IndexFormat.UInt16,
            };
        }

        private static Material NewMaterial(Shader shader, int zTest)
        {
            var material = new Material(shader)
            {
                name = $"Drawing Lines (ZTest {zTest})",
                hideFlags = HideFlags.HideAndDontSave,
            };

            material.SetFloat("_ZTest", zTest);
            return material;
        }

        /// <summary>
        /// 文字を出す。
        /// <para>
        /// ワールド座標を画面座標へ落として、そこへ書く。専用のフォント資産も
        /// 追加パッケージも要らないので、これが一番手が掛からない。
        /// </para>
        /// </summary>
        private void OnGUI()
        {
            var labels = Buffer.Labels;
            if (labels.Count == 0) return;
            if (Event.current.type != EventType.Repaint) return;

            var camera = Draw.Camera;
            if (!PrepareLabelRepaint(camera)) return;

            _labelStyle = _labelStyle ?? new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            for (var i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                var screen = camera.WorldToScreenPoint(label.Position);

                // 背後にあるものは、画面の反対側へ折り返して出てしまうので描かない。
                if (screen.z <= 0f) continue;

                _labelContent.text = label.Text;
                var size = _labelStyle.CalcSize(_labelContent);
                var rect = new Rect(
                    screen.x - size.x * 0.5f,
                    Screen.height - screen.y - size.y * 0.5f,
                    size.x,
                    size.y);

                // 明るい背景でも読めるよう、1 ピクセルずらした影を先に敷く。
                _labelStyle.normal.textColor = new Color(0f, 0f, 0f, label.Color.a * 0.7f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), _labelContent, _labelStyle);

                _labelStyle.normal.textColor = label.Color;
                GUI.Label(rect, _labelContent, _labelStyle);
            }

            Buffer.MarkLabelsSubmitted();
        }

        /// <summary>
        /// 文字を出せるカメラがあるか確かめる。
        /// カメラが無い再描画では、1 フレーム文字を破棄して後日の一括表示を防ぐ。
        /// </summary>
        internal bool PrepareLabelRepaint(Camera camera)
        {
            if (camera != null) return true;

            Buffer.DiscardSingleFrameLabels();
            return false;
        }
    }
}
