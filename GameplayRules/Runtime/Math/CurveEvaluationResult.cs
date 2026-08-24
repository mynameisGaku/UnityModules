using System;

namespace GameplayMath
{
    /// <summary>有限queryの補間値と使用segmentを再構築する結果。</summary>
    public readonly struct CurveEvaluationResult : IEquatable<CurveEvaluationResult>
    {
        internal CurveEvaluationResult(bool succeeded, CurveError error, double query, double value, CurvePoint lowerPoint, CurvePoint upperPoint, int lowerIndex, int upperIndex, double interpolation, bool clamped)
        {
            Succeeded = succeeded;
            Error = error;
            Query = query;
            Value = value;
            LowerPoint = lowerPoint;
            UpperPoint = upperPoint;
            LowerIndex = lowerIndex;
            UpperIndex = upperIndex;
            Interpolation = interpolation;
            Clamped = clamped;
        }

        /// <summary>有限値を評価できたか。</summary>
        public bool Succeeded { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public CurveError Error { get; }

        /// <summary>呼出側が渡した有限query。無効入力時は0。</summary>
        public double Query { get; }

        /// <summary>補間または端点clampで得た有限値。</summary>
        public double Value { get; }

        /// <summary>補間segment下端のpoint。端点または完全一致ではUpperPointと同じ。</summary>
        public CurvePoint LowerPoint { get; }

        /// <summary>補間segment上端のpoint。端点または完全一致ではLowerPointと同じ。</summary>
        public CurvePoint UpperPoint { get; }

        /// <summary>X昇順に並べたLowerPointのindex。失敗時は-1。</summary>
        public int LowerIndex { get; }

        /// <summary>X昇順に並べたUpperPointのindex。失敗時は-1。</summary>
        public int UpperIndex { get; }

        /// <summary>LowerPointからUpperPointまでの0以上1以下の補間率。</summary>
        public double Interpolation { get; }

        /// <summary>queryがpoint範囲外で端点値へclampされたか。</summary>
        public bool Clamped { get; }

        /// <inheritdoc />
        public bool Equals(CurveEvaluationResult other) => Succeeded == other.Succeeded && Error == other.Error && Query.Equals(other.Query) && Value.Equals(other.Value) && LowerPoint.Equals(other.LowerPoint) && UpperPoint.Equals(other.UpperPoint) && LowerIndex == other.LowerIndex && UpperIndex == other.UpperIndex && Interpolation.Equals(other.Interpolation) && Clamped == other.Clamped;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CurveEvaluationResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Succeeded ? 1 : 0;
                hash = (hash * 397) ^ (int)Error;
                hash = (hash * 397) ^ Query.GetHashCode();
                hash = (hash * 397) ^ Value.GetHashCode();
                hash = (hash * 397) ^ LowerPoint.GetHashCode();
                hash = (hash * 397) ^ UpperPoint.GetHashCode();
                hash = (hash * 397) ^ LowerIndex;
                hash = (hash * 397) ^ UpperIndex;
                hash = (hash * 397) ^ Interpolation.GetHashCode();
                return (hash * 397) ^ (Clamped ? 1 : 0);
            }
        }

        /// <summary>2つの評価結果が全fieldで等しいか判定する。</summary>
        public static bool operator ==(CurveEvaluationResult left, CurveEvaluationResult right) => left.Equals(right);

        /// <summary>2つの評価結果に異なるfieldがあるか判定する。</summary>
        public static bool operator !=(CurveEvaluationResult left, CurveEvaluationResult right) => !left.Equals(right);
    }
}
