using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu
{
    /// <summary>DebugColor の HSV 面、色相帯、現在位置を Painter2D で描く。</summary>
    public sealed class DebugColorPickerView : VisualElement
    {
        private const float PlaneHeightRatio = 0.72f;
        private const float BarInsetRatio = 0.15f;
        private const int SaturationSteps = 32;
        private const int BrightnessSteps = 16;

        private readonly DebugMenuTheme _theme;
        private readonly FillGradient _hueGradient;

        private DebugColor _color;
        private DebugColor _dragColor;
        private DragTarget _dragTarget;
        private int _dragPointerId = -1;
        private bool _boundShowAlpha;

        /// <summary>色選択面がポインターを捕捉しているか。</summary>
        public bool HasActivePointerInteraction => _dragPointerId >= 0;

        /// <summary>テーマを指定して HSV の描画領域を作る。</summary>
        /// <param name="theme">色と寸法。</param>
        public DebugColorPickerView(DebugMenuTheme theme)
        {
            _theme = theme;

            var hue = new Gradient();
            hue.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 1f / 6f),
                    new GradientColorKey(Color.green, 2f / 6f),
                    new GradientColorKey(Color.cyan, 3f / 6f),
                    new GradientColorKey(Color.blue, 4f / 6f),
                    new GradientColorKey(Color.magenta, 5f / 6f),
                    new GradientColorKey(Color.red, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            _hueGradient = FillGradient.MakeLinearGradient(hue, Vector2.zero, Vector2.right, AddressMode.Clamp);

            var panelPadding = theme.EffectiveRowHeight * theme.ColorPickerPaddingRatio;
            style.width = theme.EffectiveColorPickerHeight * theme.ColorPickerWidthRatio + panelPadding * 2f;
            style.height = theme.EffectiveColorPickerHeight + panelPadding * 2f;
            style.marginLeft = theme.EffectiveRowHeight * theme.ExpandedContentInsetRatio;
            style.marginRight = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = panelPadding;
            style.paddingRight = panelPadding;
            style.paddingTop = panelPadding;
            style.paddingBottom = panelPadding;
            style.backgroundColor = theme.ColorPickerBackground;
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = theme.ColorPickerPanelBorder;
            style.borderBottomColor = theme.ColorPickerPanelBorder;
            style.borderLeftColor = theme.ColorPickerPanelBorder;
            style.borderRightColor = theme.ColorPickerPanelBorder;
            style.display = DisplayStyle.None;
            style.overflow = Overflow.Hidden;

            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>描く色を差し替える。null または折り畳み中なら領域を隠す。</summary>
        /// <param name="color">描く色。</param>
        public void Bind(DebugColor color)
        {
            if (!ReferenceEquals(_color, color) || color == null || !color.IsExpanded || _boundShowAlpha != color.ShowAlpha) CancelDrag();
            _color = color;
            _boundShowAlpha = color != null && color.ShowAlpha;

            if (color == null || !color.IsExpanded)
            {
                style.display = DisplayStyle.None;
                return;
            }

            if (color.TryGetColor(out var current)) color.SyncHsvFrom(current);
            style.display = DisplayStyle.Flex;
            var panelPadding = _theme.EffectiveRowHeight * _theme.ColorPickerPaddingRatio;
            var alphaHeight = color.ShowAlpha ? _theme.EffectiveRowHeight : 0f;
            style.height = _theme.EffectiveColorPickerHeight + panelPadding * 2f + alphaHeight;
            MarkDirtyRepaint();
        }

        /// <summary>仮想化リストから外れる前にドラッグを終える。</summary>
        public void Unbind()
        {
            CancelDrag();
            _color = null;
            _boundShowAlpha = false;
            style.display = DisplayStyle.None;
        }

        /// <summary>メニューを閉じる前に進行中のドラッグを終える。</summary>
        public void CancelPointerInteraction() => CancelDrag();

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _color == null || !_color.IsExpanded) return;

            _dragTarget = ResolveDragTarget(evt.localPosition);
            if (_dragTarget == DragTarget.None) return;

            _dragColor = _color;
            _dragPointerId = evt.pointerId;
            ApplyPointerPosition(evt.localPosition);
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_dragTarget == DragTarget.None || !this.HasPointerCapture(evt.pointerId)) return;

            ApplyPointerPosition(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;

            ApplyPointerPosition(evt.localPosition);
            _dragPointerId = -1;
            _dragColor = null;
            _dragTarget = DragTarget.None;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;

            _dragPointerId = -1;
            _dragColor = null;
            _dragTarget = DragTarget.None;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _dragPointerId = -1;
            _dragColor = null;
            _dragTarget = DragTarget.None;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) => CancelDrag();

        private void CancelDrag()
        {
            var pointerId = _dragPointerId;
            _dragPointerId = -1;
            _dragColor = null;
            _dragTarget = DragTarget.None;

            if (pointerId >= 0 && this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
        }

        private DragTarget ResolveDragTarget(Vector2 position)
        {
            GetPickerRects(out var hsvRect, out var hueRect, out var alphaRect);
            if (hsvRect.Contains(position)) return DragTarget.SaturationBrightness;
            if (hueRect.Contains(position)) return DragTarget.Hue;
            if (_color != null && _color.ShowAlpha && alphaRect.Contains(position)) return DragTarget.Alpha;

            return DragTarget.None;
        }

        private void ApplyPointerPosition(Vector2 position)
        {
            var color = _dragColor;
            if (color == null) return;

            GetPickerRects(out var hsvRect, out var hueRect, out var alphaRect);
            switch (_dragTarget)
            {
                case DragTarget.SaturationBrightness:
                {
                    var saturation = Mathf.InverseLerp(hsvRect.xMin, hsvRect.xMax, position.x);
                    var brightness = 1f - Mathf.InverseLerp(hsvRect.yMin, hsvRect.yMax, position.y);
                    color.SetHsv(color.Hue, saturation, brightness);
                    break;
                }
                case DragTarget.Hue:
                {
                    var hue = Mathf.InverseLerp(hueRect.xMin, hueRect.xMax, position.x);
                    color.SetHsv(hue, color.Saturation, color.Brightness);
                    break;
                }
                case DragTarget.Alpha:
                {
                    var alpha = Mathf.InverseLerp(alphaRect.xMin, alphaRect.xMax, position.x);
                    color.SetAlpha(alpha);
                    break;
                }
            }

            MarkDirtyRepaint();
        }

        private void GetPickerRects(out Rect hsvRect, out Rect hueRect, out Rect alphaRect)
        {
            var content = contentRect;
            var baseHeight = Mathf.Min(_theme.EffectiveColorPickerHeight, content.height);
            var planeHeight = baseHeight * PlaneHeightRatio;
            hsvRect = new Rect(content.xMin, content.yMin, content.width, planeHeight);
            hueRect = new Rect(content.xMin, hsvRect.yMax, content.width, Mathf.Max(0f, baseHeight - planeHeight));
            alphaRect = _color != null && _color.ShowAlpha
                ? new Rect(content.xMin, content.yMin + baseHeight, content.width, Mathf.Max(0f, content.height - baseHeight))
                : Rect.zero;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_color == null || !_color.IsExpanded) return;

            try
            {
                DrawPicker(context);
            }
            catch (System.Exception exception)
            {
                _color.ReportReadError("値取得", exception);
            }
        }

        /// <summary>色選択面を描く。利用側Getterの例外境界は呼び出し側に置く。</summary>
        private void DrawPicker(MeshGenerationContext context)
        {
            var content = contentRect;
            if (content.width <= 2f || content.height <= 2f) return;
            if (!_color.TryGetColor(out var current)) return;

            _color.SyncHsvFrom(current);

            GetPickerRects(out var hsvRect, out var hueRect, out var alphaRect);
            var painter = context.painter2D;

            DrawHsvMesh(context, hsvRect, _color.Hue);
            StrokeRect(painter, hsvRect, _theme.ColorPickerBorder);

            var hueBand = InsetBar(hueRect);
            FillGradientRect(painter, hueBand, _hueGradient, new Vector2(hueBand.xMin, hueBand.center.y), new Vector2(hueBand.xMax, hueBand.center.y));

            if (_color.ShowAlpha)
            {
                var alphaBand = InsetBar(alphaRect);
                FillRect(painter, alphaBand, new Color(0.22f, 0.22f, 0.22f, 1f));
                DrawAlphaMesh(context, alphaBand, current);
                StrokeRect(painter, alphaBand, _theme.ColorPickerBorder);
            }

            DrawSelection(painter, hsvRect, hueBand, _color.ShowAlpha ? InsetBar(alphaRect) : Rect.zero, current);
        }

        /// <summary>
        /// HSV 面を細かい格子へ分け、各頂点の正しい HSV 色を補間して描く。
        /// 4 頂点だけでは三角形内が線形補間され、中央付近の色が灰色へずれるため。
        /// </summary>
        private static void DrawHsvMesh(MeshGenerationContext context, Rect rect, float hue)
        {
            var columnCount = SaturationSteps + 1;
            var mesh = context.Allocate(columnCount * (BrightnessSteps + 1), SaturationSteps * BrightnessSteps * 6, null);

            for (var y = 0; y <= BrightnessSteps; y++)
            {
                var yRatio = y / (float)BrightnessSteps;
                var brightness = 1f - yRatio;

                for (var x = 0; x <= SaturationSteps; x++)
                {
                    var saturation = x / (float)SaturationSteps;
                    var color = Color.HSVToRGB(hue, saturation, brightness);
                    mesh.SetNextVertex(MakeVertex(
                        Mathf.Lerp(rect.xMin, rect.xMax, saturation),
                        Mathf.Lerp(rect.yMin, rect.yMax, yRatio),
                        color));
                }
            }

            for (var y = 0; y < BrightnessSteps; y++)
            {
                for (var x = 0; x < SaturationSteps; x++)
                {
                    var topLeft = (ushort)(y * columnCount + x);
                    var topRight = (ushort)(topLeft + 1);
                    var bottomLeft = (ushort)(topLeft + columnCount);
                    var bottomRight = (ushort)(bottomLeft + 1);

                    mesh.SetNextIndex(topLeft);
                    mesh.SetNextIndex(topRight);
                    mesh.SetNextIndex(bottomRight);
                    mesh.SetNextIndex(bottomRight);
                    mesh.SetNextIndex(bottomLeft);
                    mesh.SetNextIndex(topLeft);
                }
            }
        }

        private static Vertex MakeVertex(float x, float y, Color color) => new Vertex
        {
            position = new Vector3(x, y, Vertex.nearZ),
            tint = color,
            uv = Vector2.zero,
        };

        private static void DrawAlphaMesh(MeshGenerationContext context, Rect rect, Color color)
        {
            var mesh = context.Allocate(4, 6, null);
            var opaque = color;
            var transparent = color;
            opaque.a = 1f;
            transparent.a = 0f;

            mesh.SetNextVertex(MakeVertex(rect.xMin, rect.yMin, transparent));
            mesh.SetNextVertex(MakeVertex(rect.xMax, rect.yMin, opaque));
            mesh.SetNextVertex(MakeVertex(rect.xMax, rect.yMax, opaque));
            mesh.SetNextVertex(MakeVertex(rect.xMin, rect.yMax, transparent));

            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
            mesh.SetNextIndex(0);
        }

        private void DrawSelection(Painter2D painter, Rect hsvRect, Rect hueRect, Rect alphaRect, Color current)
        {
            var point = new Vector2(
                Mathf.Lerp(hsvRect.xMin, hsvRect.xMax, _color.Saturation),
                Mathf.Lerp(hsvRect.yMax, hsvRect.yMin, _color.Brightness));

            painter.strokeColor = Color.black;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(3f));
            DrawCross(painter, point, _theme.ScalePixels(5f));
            painter.strokeColor = Color.white;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(1f));
            DrawCross(painter, point, _theme.ScalePixels(5f));

            var hueX = Mathf.Lerp(hueRect.xMin, hueRect.xMax, _color.Hue);
            painter.strokeColor = Color.black;
            var markerExtension = _theme.ScalePixels(1f);
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(3f));
            DrawVerticalLine(painter, hueX, hueRect.yMin - markerExtension, hueRect.yMax + markerExtension);
            painter.strokeColor = Color.white;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(1f));
            DrawVerticalLine(painter, hueX, hueRect.yMin - markerExtension, hueRect.yMax + markerExtension);

            if (!_color.ShowAlpha) return;

            var alphaX = Mathf.Lerp(alphaRect.xMin, alphaRect.xMax, current.a);
            painter.strokeColor = Color.black;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(3f));
            DrawVerticalLine(painter, alphaX, alphaRect.yMin - markerExtension, alphaRect.yMax + markerExtension);
            painter.strokeColor = Color.white;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(1f));
            DrawVerticalLine(painter, alphaX, alphaRect.yMin - markerExtension, alphaRect.yMax + markerExtension);
        }

        private static Rect InsetBar(Rect rect)
        {
            var inset = rect.height * BarInsetRatio;
            return new Rect(rect.xMin, rect.yMin + inset, rect.width, Mathf.Max(0f, rect.height - inset * 2f));
        }

        private static void DrawCross(Painter2D painter, Vector2 point, float radius)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(point.x - radius, point.y));
            painter.LineTo(new Vector2(point.x + radius, point.y));
            painter.MoveTo(new Vector2(point.x, point.y - radius));
            painter.LineTo(new Vector2(point.x, point.y + radius));
            painter.Stroke();
        }

        private static void DrawHorizontalLine(Painter2D painter, float xMin, float xMax, float y)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(xMin, y));
            painter.LineTo(new Vector2(xMax, y));
            painter.Stroke();
        }

        private static void DrawVerticalLine(Painter2D painter, float x, float yMin, float yMax)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, yMin));
            painter.LineTo(new Vector2(x, yMax));
            painter.Stroke();
        }

        private static void FillGradientRect(Painter2D painter, Rect rect, FillGradient gradient, Vector2 start, Vector2 end)
        {
            gradient.start = start;
            gradient.end = end;
            painter.fillGradient = gradient;
            BuildRectPath(painter, rect);
            painter.Fill();
            painter.fillGradient = default;
        }

        private void StrokeRect(Painter2D painter, Rect rect, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = Mathf.Max(1f, _theme.ScalePixels(1f));
            BuildRectPath(painter, rect);
            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillGradient = default;
            painter.fillColor = color;
            BuildRectPath(painter, rect);
            painter.Fill();
        }

        private static void BuildRectPath(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
        }

        private enum DragTarget
        {
            None,
            SaturationBrightness,
            Hue,
            Alpha,
        }
    }
}
