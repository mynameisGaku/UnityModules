using UnityEngine;

namespace AdaptiveLayout
{
    internal static class SafeAreaMath
    {
        internal static bool TryCreateSnapshot(int screenWidth, int screenHeight, Rect safeArea, out SafeAreaSnapshot snapshot)
        {
            snapshot = default;
            if (screenWidth <= 0 || screenHeight <= 0 || !IsFinite(safeArea))
            {
                return false;
            }

            if (safeArea.width <= 0f || safeArea.height <= 0f
                || safeArea.xMin < 0f || safeArea.yMin < 0f
                || safeArea.xMax > screenWidth || safeArea.yMax > screenHeight)
            {
                return false;
            }

            snapshot = new SafeAreaSnapshot(new Vector2Int(screenWidth, screenHeight), safeArea);
            return true;
        }

        internal static Rect GetNormalizedRect(SafeAreaSnapshot snapshot, SafeAreaEdges edges)
        {
            var width = snapshot.ScreenSize.x;
            var height = snapshot.ScreenSize.y;
            var minX = Includes(edges, SafeAreaEdges.Left) ? snapshot.SafeArea.xMin / width : 0f;
            var minY = Includes(edges, SafeAreaEdges.Bottom) ? snapshot.SafeArea.yMin / height : 0f;
            var maxX = Includes(edges, SafeAreaEdges.Right) ? snapshot.SafeArea.xMax / width : 1f;
            var maxY = Includes(edges, SafeAreaEdges.Top) ? snapshot.SafeArea.yMax / height : 1f;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        internal static bool Includes(SafeAreaEdges value, SafeAreaEdges edge)
        {
            return (value & edge) == edge;
        }

        internal static SafeAreaEdges NormalizeEdges(SafeAreaEdges edges)
        {
            return edges & SafeAreaEdges.All;
        }

        private static bool IsFinite(Rect value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.width)
                && float.IsFinite(value.height);
        }
    }
}
