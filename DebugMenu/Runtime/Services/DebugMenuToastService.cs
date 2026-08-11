using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>トーストの意味に応じた表示種別。</summary>
    public enum DebugMenuToastKind
    {
        Info,
        Success,
        Warning,
        Error,
    }

    /// <summary>画面へ一定時間表示する短い通知。</summary>
    public readonly struct DebugMenuToast
    {
        public DebugMenuToast(string message, DebugMenuToastKind kind, float durationSeconds)
        {
            Message = message ?? string.Empty;
            Kind = kind;
            DurationSeconds = Math.Max(0.1f, durationSeconds);
        }

        public readonly string Message;
        public readonly DebugMenuToastKind Kind;
        public readonly float DurationSeconds;
    }

    /// <summary>通知を順番に表示し、経過時間で自動的に次へ進める。</summary>
    public sealed class DebugMenuToastService
    {
        private readonly Queue<DebugMenuToast> _pending = new Queue<DebugMenuToast>();

        private DebugMenuToast? _current;
        private float _remainingSeconds;

        /// <summary>現在表示中の通知。無ければnull。</summary>
        public DebugMenuToast? Current => _current;

        /// <summary>現在の通知が消えるまでの秒数。</summary>
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>待機中の通知数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>表示内容が変わったときに呼ばれる。</summary>
        public event Action Changed;

        /// <summary>通知を表示キューへ加える。</summary>
        public void Show(string message, DebugMenuToastKind kind = DebugMenuToastKind.Info, float durationSeconds = 2.5f)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var toast = new DebugMenuToast(message.Trim(), kind, durationSeconds);
            if (_current.HasValue)
            {
                _pending.Enqueue(toast);
                return;
            }

            SetCurrent(toast);
        }

        /// <summary>非スケール時間で表示時間を進める。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!_current.HasValue || deltaSeconds <= 0f) return;

            _remainingSeconds -= deltaSeconds;
            if (_remainingSeconds > 0f) return;
            Advance();
        }

        /// <summary>現在の通知を閉じ、待機中があれば次を表示する。</summary>
        public void Dismiss()
        {
            if (!_current.HasValue) return;
            Advance();
        }

        /// <summary>表示中と待機中の通知を全て消す。</summary>
        public void Clear()
        {
            if (!_current.HasValue && _pending.Count == 0) return;

            _current = null;
            _remainingSeconds = 0f;
            _pending.Clear();
            Changed?.Invoke();
        }

        private void Advance()
        {
            if (_pending.Count > 0) SetCurrent(_pending.Dequeue());
            else
            {
                _current = null;
                _remainingSeconds = 0f;
                Changed?.Invoke();
            }
        }

        private void SetCurrent(in DebugMenuToast toast)
        {
            _current = toast;
            _remainingSeconds = toast.DurationSeconds;
            Changed?.Invoke();
        }
    }
}
