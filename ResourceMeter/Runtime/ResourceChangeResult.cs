using System;

namespace GameplayResources
{
    /// <summary>resourceの前後値、要求・実適用・未適用deltaを表すimmutableな変更結果。</summary>
    public readonly struct ResourceChangeResult : IEquatable<ResourceChangeResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の変更前値。失敗時は0。</summary>
        public double PreviousValue { get; }

        /// <summary>成功時の変更後値。失敗時は0。</summary>
        public double CurrentValue { get; }

        /// <summary>成功時のimmutableなcapacity。失敗時は0。</summary>
        public double Capacity { get; }

        /// <summary>回復は正、消費は負で表した要求delta。失敗時は0。</summary>
        public double RequestedDelta { get; }

        /// <summary>実際にstateへ適用した符号付きdelta。失敗時は0。</summary>
        public double AppliedDelta { get; }

        /// <summary>要求deltaのうちcapacityまたはpolicyで適用しなかった符号付きdelta。失敗時は0。</summary>
        public double UnappliedDelta { get; }

        /// <summary>要求amountを全て適用したか。</summary>
        public bool WasFullyApplied { get; }

        /// <summary>現在値が変化したか。</summary>
        public bool Changed { get; }

        /// <summary>この変更で0へ到達したか。</summary>
        public bool BecameEmpty { get; }

        /// <summary>この変更でcapacityへ到達したか。</summary>
        public bool BecameFull { get; }

        /// <summary>変更後値が0か。</summary>
        public bool IsEmpty { get; }

        /// <summary>変更後値がcapacityと同じか。</summary>
        public bool IsFull { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public ResourceMeterError Error { get; }

        /// <summary>有効な変更結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == ResourceMeterError.None;

        private ResourceChangeResult(double previousValue, double currentValue, double capacity, double requestedDelta, double appliedDelta, double unappliedDelta, bool wasFullyApplied, ResourceMeterError error, bool hasValue)
        {
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            Capacity = capacity;
            RequestedDelta = requestedDelta;
            AppliedDelta = appliedDelta;
            UnappliedDelta = unappliedDelta;
            WasFullyApplied = wasFullyApplied;
            Changed = previousValue != currentValue;
            BecameEmpty = previousValue != 0d && currentValue == 0d;
            BecameFull = previousValue != capacity && currentValue == capacity;
            IsEmpty = hasValue && currentValue == 0d;
            IsFull = hasValue && currentValue == capacity;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static ResourceChangeResult Success(double previousValue, double currentValue, double capacity, double requestedDelta, double appliedDelta, double unappliedDelta, bool wasFullyApplied) => new ResourceChangeResult(previousValue, currentValue, capacity, requestedDelta, appliedDelta, unappliedDelta, wasFullyApplied, ResourceMeterError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static ResourceChangeResult Failure(ResourceMeterError error) => new ResourceChangeResult(0d, 0d, 0d, 0d, 0d, 0d, false, error, false);

        /// <summary>全出力と成功状態が同じかを返す。</summary>
        /// <param name="other">比較する結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public bool Equals(ResourceChangeResult other) => PreviousValue.Equals(other.PreviousValue) && CurrentValue.Equals(other.CurrentValue) && Capacity.Equals(other.Capacity) && RequestedDelta.Equals(other.RequestedDelta) && AppliedDelta.Equals(other.AppliedDelta) && UnappliedDelta.Equals(other.UnappliedDelta) && WasFullyApplied == other.WasFullyApplied && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ結果の場合true。</returns>
        public override bool Equals(object obj) => obj is ResourceChangeResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        /// <returns>全出力と成功状態から求めたhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PreviousValue.GetHashCode();
                hash = (hash * 397) ^ CurrentValue.GetHashCode();
                hash = (hash * 397) ^ Capacity.GetHashCode();
                hash = (hash * 397) ^ RequestedDelta.GetHashCode();
                hash = (hash * 397) ^ AppliedDelta.GetHashCode();
                hash = (hash * 397) ^ UnappliedDelta.GetHashCode();
                hash = (hash * 397) ^ (WasFullyApplied ? 1 : 0);
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public static bool operator ==(ResourceChangeResult left, ResourceChangeResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>異なる結果の場合true。</returns>
        public static bool operator !=(ResourceChangeResult left, ResourceChangeResult right) => !left.Equals(right);
    }
}
