using System;

namespace GameplayMath
{
    /// <summary>最大32点をX昇順へ保持し、範囲外を端点clampするpiecewise linear curve。</summary>
    public sealed class PiecewiseLinearCurve
    {
        /// <summary>保持できるpoint件数の上限。</summary>
        public const int MaximumPointCount = 32;

        private readonly CurvePoint[] _points = new CurvePoint[MaximumPointCount];
        private int _pointCount;

        /// <summary>空のcurveを作成する。</summary>
        public PiecewiseLinearCurve()
        {
        }

        /// <summary>現在のpoint件数。</summary>
        public int PointCount => _pointCount;

        /// <summary>一意な有限Xと有限Yを追加する。重複Xまたは上限到達では状態を変えない。</summary>
        public CurveChangeResult Add(double x, double y)
        {
            var validation = ValidatePoint(x, y);
            if (validation != CurveError.None) return Failure(validation, x, 0d, y);
            var foundIndex = FindIndex(x);
            if (foundIndex >= 0) return Failure(CurveError.DuplicateX, x, _points[foundIndex].Y, y);
            if (_pointCount >= MaximumPointCount) return Failure(CurveError.CapacityReached, x, 0d, y);

            var insertionIndex = ~foundIndex;
            var previousCount = _pointCount;
            for (var index = _pointCount; index > insertionIndex; index--) _points[index] = _points[index - 1];
            _points[insertionIndex] = new CurvePoint(x, y);
            _pointCount++;
            return new CurveChangeResult(true, true, CurveError.None, x, 0d, y, previousCount, _pointCount);
        }

        /// <summary>既存XのYを更新する。無効値または未登録Xでは状態を変えない。</summary>
        public CurveChangeResult Update(double x, double y)
        {
            var validation = ValidatePoint(x, y);
            if (validation != CurveError.None) return Failure(validation, x, 0d, y);
            var index = FindIndex(x);
            if (index < 0) return Failure(CurveError.PointNotFound, x, 0d, y);
            var previousY = _points[index].Y;
            if (previousY.Equals(y)) return new CurveChangeResult(true, false, CurveError.None, x, previousY, y, _pointCount, _pointCount);
            _points[index] = new CurvePoint(x, y);
            return new CurveChangeResult(true, true, CurveError.None, x, previousY, y, _pointCount, _pointCount);
        }

        /// <summary>既存Xのpointを除去する。無効値または未登録Xでは状態を変えない。</summary>
        public CurveChangeResult Remove(double x)
        {
            if (!IsFinite(x)) return Failure(CurveError.InvalidX, 0d, 0d, 0d);
            var index = FindIndex(x);
            if (index < 0) return Failure(CurveError.PointNotFound, x, 0d, 0d);
            var previousY = _points[index].Y;
            var previousCount = _pointCount;
            for (var current = index; current + 1 < _pointCount; current++) _points[current] = _points[current + 1];
            _pointCount--;
            _points[_pointCount] = default;
            return new CurveChangeResult(true, true, CurveError.None, x, previousY, 0d, previousCount, _pointCount);
        }

        /// <summary>全pointを除去する。</summary>
        public CurveChangeResult Clear()
        {
            var previousCount = _pointCount;
            if (_pointCount == 0) return new CurveChangeResult(true, false, CurveError.None, 0d, 0d, 0d, 0, 0);
            Array.Clear(_points, 0, _pointCount);
            _pointCount = 0;
            return new CurveChangeResult(true, true, CurveError.None, 0d, 0d, 0d, previousCount, 0);
        }

        /// <summary>X昇順のindexからpointを取得する。範囲外ではfalseを返す。</summary>
        public bool TryGetPointAt(int index, out CurvePoint point, out CurveError error)
        {
            if (index < 0 || index >= _pointCount)
            {
                point = default;
                error = CurveError.IndexOutOfRange;
                return false;
            }
            point = _points[index];
            error = CurveError.None;
            return true;
        }

        /// <summary>完全一致する有限Xからpointを取得する。無効値または未登録Xではfalseを返す。</summary>
        public bool TryGetPoint(double x, out CurvePoint point, out CurveError error)
        {
            if (!IsFinite(x))
            {
                point = default;
                error = CurveError.InvalidX;
                return false;
            }
            var index = FindIndex(x);
            if (index < 0)
            {
                point = default;
                error = CurveError.PointNotFound;
                return false;
            }
            point = _points[index];
            error = CurveError.None;
            return true;
        }

        /// <summary>有限queryをpoint間で線形補間し、範囲外では最寄り端点へclampする。</summary>
        public CurveEvaluationResult Evaluate(double query)
        {
            if (!IsFinite(query)) return FailureEvaluation(CurveError.InvalidQuery, 0d);
            if (_pointCount == 0) return FailureEvaluation(CurveError.EmptyCurve, query);

            var foundIndex = FindIndex(query);
            if (foundIndex >= 0) return Exact(query, foundIndex, false);
            var insertionIndex = ~foundIndex;
            if (insertionIndex == 0) return Exact(query, 0, true);
            if (insertionIndex == _pointCount) return Exact(query, _pointCount - 1, true);

            var lowerIndex = insertionIndex - 1;
            var upperIndex = insertionIndex;
            var lower = _points[lowerIndex];
            var upper = _points[upperIndex];
            var span = upper.X - lower.X;
            var interpolation = IsFinite(span)
                ? (query - lower.X) / span
                : ((query * 0.5d) - (lower.X * 0.5d)) / ((upper.X * 0.5d) - (lower.X * 0.5d));
            var value = (lower.Y * (1d - interpolation)) + (upper.Y * interpolation);
            if (!IsFinite(interpolation) || interpolation < 0d || interpolation > 1d || !IsFinite(value))
                return FailureEvaluation(CurveError.NumericOverflow, query);
            return new CurveEvaluationResult(true, CurveError.None, query, value, lower, upper, lowerIndex, upperIndex, interpolation, false);
        }

        private CurveChangeResult Failure(CurveError error, double x, double previousY, double currentY) => new CurveChangeResult(false, false, error, IsFinite(x) ? x : 0d, previousY, currentY, _pointCount, _pointCount);

        private static CurveEvaluationResult FailureEvaluation(CurveError error, double query) => new CurveEvaluationResult(false, error, query, 0d, default, default, -1, -1, 0d, false);

        private CurveEvaluationResult Exact(double query, int index, bool clamped)
        {
            var point = _points[index];
            return new CurveEvaluationResult(true, CurveError.None, query, point.Y, point, point, index, index, 0d, clamped);
        }

        private static CurveError ValidatePoint(double x, double y)
        {
            if (!IsFinite(x)) return CurveError.InvalidX;
            return IsFinite(y) ? CurveError.None : CurveError.InvalidY;
        }

        private int FindIndex(double x)
        {
            var lower = 0;
            var upper = _pointCount - 1;
            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) / 2);
                var comparison = _points[middle].X.CompareTo(x);
                if (comparison == 0) return middle;
                if (comparison < 0) lower = middle + 1;
                else upper = middle - 1;
            }
            return ~lower;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
