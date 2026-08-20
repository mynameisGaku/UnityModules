namespace InputGate
{
    /// <summary>Action Mapの所有準備、停止状態、取得権数をまとめた通知用スナップショット。</summary>
    public readonly struct InputGateStatus
    {
        /// <summary>Controllerの現在状態から通知用スナップショットを作る。</summary>
        /// <param name="isReady">新しい取得要求を受け付けられる場合はtrue。</param>
        /// <param name="isBlocking">対象Action Mapを停止中ならtrue。</param>
        /// <param name="error">現在状態へ至った理由。</param>
        /// <param name="controlledMapCount">所有しているAction Map数。</param>
        /// <param name="activeLeaseCount">現在有効な取得権数。</param>
        internal InputGateStatus(bool isReady, bool isBlocking, InputGateError error, int controlledMapCount, int activeLeaseCount)
        {
            IsReady = isReady;
            IsBlocking = isBlocking;
            Error = error;
            ControlledMapCount = controlledMapCount;
            ActiveLeaseCount = activeLeaseCount;
        }

        /// <summary>新しい取得要求を受け付けられる健康な所有状態ならtrue。</summary>
        public bool IsReady { get; }

        /// <summary>1件以上の取得権により対象Action Mapを停止中ならtrue。</summary>
        public bool IsBlocking { get; }

        /// <summary>現在状態へ至った理由。健康な所有状態ではNone。</summary>
        public InputGateError Error { get; }

        /// <summary>このControllerが所有している実行中Action Mapの数。</summary>
        public int ControlledMapCount { get; }

        /// <summary>現在の所有世代で有効な取得権の数。</summary>
        public int ActiveLeaseCount { get; }
    }
}
