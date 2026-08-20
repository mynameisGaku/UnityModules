using System;

namespace AudioControl
{
    /// <summary>1つの再生voiceを所有し、Disposeでそのvoiceだけを停止するhandleです。</summary>
    public sealed class AudioControlHandle : IDisposable
    {
        private readonly AudioControlToken _token;

        internal AudioControlHandle(AudioControlToken token)
        {
            _token = token;
        }

        /// <summary>voiceが現在も再生ownerに保持されているかを取得します。</summary>
        public bool IsActive => _token.IsActive;

        /// <summary>0を最高、255を最低とするvoice priorityを取得します。</summary>
        public int Priority => _token.Priority;

        internal AudioControlToken Token => _token;

        /// <summary>このhandleが所有するvoiceを停止します。任意スレッドから重複して呼べます。</summary>
        public void Dispose()
        {
            _token.Dispose();
        }
    }
}
