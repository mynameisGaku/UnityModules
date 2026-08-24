using System;

namespace InputDeviceDisplay
{
    /// <summary>現在の入力端末と画面へ表示する表記体系をまとめた通知用スナップショット。</summary>
    public readonly struct InputDeviceDisplayState : IEquatable<InputDeviceDisplayState>
    {
        /// <summary>入力端末の表示状態を作る。</summary>
        /// <param name="isReady">入力イベントを監視できる場合はtrue。</param>
        /// <param name="hasDeviceActivity">実際の端末操作から表記体系を選択した場合はtrue。</param>
        /// <param name="style">画面へ表示する表記体系。</param>
        /// <param name="deviceId">選択元のInput System端末ID。fallback状態では0。</param>
        /// <param name="layoutName">選択元のInput System layout名。fallback状態では空文字列。</param>
        /// <param name="error">現在状態へ至った理由。</param>
        public InputDeviceDisplayState(
            bool isReady,
            bool hasDeviceActivity,
            InputDeviceDisplayStyle style,
            int deviceId,
            string layoutName,
            InputDeviceDisplayError error)
        {
            IsReady = isReady;
            HasDeviceActivity = hasDeviceActivity;
            Style = style;
            DeviceId = deviceId;
            LayoutName = layoutName ?? string.Empty;
            Error = error;
        }

        /// <summary>入力イベントを監視できる健康な状態ならtrue。</summary>
        public bool IsReady { get; }

        /// <summary>実際の端末操作から現在の表記体系を選択した場合はtrue。</summary>
        public bool HasDeviceActivity { get; }

        /// <summary>画面へ表示する現在の表記体系。</summary>
        public InputDeviceDisplayStyle Style { get; }

        /// <summary>選択元のInput System端末ID。fallback状態では0。</summary>
        public int DeviceId { get; }

        /// <summary>選択元のInput System layout名。fallback状態では空文字列。</summary>
        public string LayoutName { get; }

        /// <summary>現在状態へ至った理由。監視中はNone。</summary>
        public InputDeviceDisplayError Error { get; }

        /// <summary>全ての表示状態が等しいかを返す。</summary>
        /// <param name="other">比較する表示状態。</param>
        /// <returns>全ての値が等しい場合はtrue。</returns>
        public bool Equals(InputDeviceDisplayState other)
        {
            return IsReady == other.IsReady &&
                   HasDeviceActivity == other.HasDeviceActivity &&
                   Style == other.Style &&
                   DeviceId == other.DeviceId &&
                   string.Equals(LayoutName, other.LayoutName, StringComparison.Ordinal) &&
                   Error == other.Error;
        }

        /// <summary>指定objectが同じ表示状態ならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ表示状態ならtrue。</returns>
        public override bool Equals(object obj) => obj is InputDeviceDisplayState other && Equals(other);

        /// <summary>全ての表示状態からhash値を返す。</summary>
        /// <returns>表示状態のhash値。</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(IsReady, HasDeviceActivity, (int)Style, DeviceId, LayoutName, (int)Error);
        }

        /// <summary>左右の表示状態が等しいかを返す。</summary>
        /// <param name="left">左辺の表示状態。</param>
        /// <param name="right">右辺の表示状態。</param>
        /// <returns>等しい場合はtrue。</returns>
        public static bool operator ==(InputDeviceDisplayState left, InputDeviceDisplayState right) => left.Equals(right);

        /// <summary>左右の表示状態が異なるかを返す。</summary>
        /// <param name="left">左辺の表示状態。</param>
        /// <param name="right">右辺の表示状態。</param>
        /// <returns>異なる場合はtrue。</returns>
        public static bool operator !=(InputDeviceDisplayState left, InputDeviceDisplayState right) => !left.Equals(right);
    }
}
