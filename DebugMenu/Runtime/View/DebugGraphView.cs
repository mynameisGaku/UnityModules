using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu
{
    /// <summary>DebugGraph の標本を Painter2D の折れ線として描く。</summary>
    public sealed class DebugGraphView : VisualElement
    {
        private readonly DebugMenuTheme _theme;
        private DebugGraph _graph;
        private Color _lineColor;

        /// <summary>テーマを指定して折れ線の描画領域を作る。</summary>
        /// <param name="theme">色と寸法。</param>
        public DebugGraphView(DebugMenuTheme theme)
        {
            _theme = theme;
            _lineColor = theme.GraphLine;

            style.width = theme.EffectiveRowHeight * theme.GraphWidthRatio;
            style.marginLeft = theme.EffectiveRowHeight * theme.ValueColumnRatio;
            style.marginRight = 0f;
            style.marginBottom = 0f;
            style.overflow = Overflow.Hidden;
            style.display = DisplayStyle.None;

            generateVisualContent += Draw;
        }

        /// <summary>描くグラフを差し替える。null または折り畳み中なら領域を隠す。</summary>
        /// <param name="graph">描くグラフ。</param>
        public void Bind(DebugGraph graph)
        {
            Bind(graph, _theme.GraphLine);
        }

        /// <summary>描くグラフと、行の状態を反映した線色を差し替える。</summary>
        /// <param name="graph">描くグラフ。</param>
        /// <param name="lineColor">値欄と共通の線色。</param>
        public void Bind(DebugGraph graph, Color lineColor)
        {
            _graph = graph;
            _lineColor = lineColor;

            if (graph == null || !graph.IsExpanded)
            {
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;
            style.height = Mathf.Max(_theme.EffectiveRowHeight, _theme.EffectiveRowHeight * graph.HeightRatio);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_graph == null || !_graph.IsExpanded) return;

            var rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f) return;

            var painter = context.painter2D;
            FillRect(painter, rect, _theme.GraphBackground);
            DrawGrid(painter, rect);
            DrawSamples(painter, rect);
        }

        private void DrawGrid(Painter2D painter, Rect rect)
        {
            painter.strokeColor = _theme.GraphGrid;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(1f));
            painter.BeginPath();

            for (var i = 1; i < 4; i++)
            {
                var y = rect.yMin + rect.height * i / 4f;
                painter.MoveTo(new Vector2(rect.xMin, y));
                painter.LineTo(new Vector2(rect.xMax, y));
            }

            painter.Stroke();
        }

        private void DrawSamples(Painter2D painter, Rect rect)
        {
            var samples = _graph.Samples;
            if (samples.Count < 2) return;

            _graph.GetScale(out var min, out var max);
            if (float.IsNaN(min) || float.IsInfinity(min) || float.IsNaN(max) || float.IsInfinity(max)) return;

            var range = max - min;
            if (range <= 0f) return;

            var inset = _theme.ScalePixels(2f);
            var inner = new Rect(
                rect.xMin + inset,
                rect.yMin + inset,
                Mathf.Max(0f, rect.width - inset * 2f),
                Mathf.Max(0f, rect.height - inset * 2f));
            painter.strokeColor = _lineColor;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(1.5f));

            var pathHasPoint = false;
            for (var i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample))
                {
                    if (pathHasPoint) painter.Stroke();
                    pathHasPoint = false;
                    continue;
                }

                var x = inner.xMin + inner.width * i / (samples.Count - 1f);
                var ratio = Mathf.Clamp01((sample - min) / range);
                var point = new Vector2(x, Mathf.Lerp(inner.yMax, inner.yMin, ratio));

                if (!pathHasPoint)
                {
                    painter.BeginPath();
                    painter.MoveTo(point);
                    pathHasPoint = true;
                }
                else
                {
                    painter.LineTo(point);
                }
            }

            if (pathHasPoint) painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillGradient = default;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
