using System;
using System.Collections.Generic;

namespace InputChording
{
    /// <summary>明示tickとpressed command snapshotから同時押しchordを判定するEngine非依存state machine。</summary>
    public sealed class InputChordMatcher
    {
        /// <summary>chordへ設定できるrequired command数の上限。</summary>
        public const int MaximumRequiredCommandCount = 16;

        /// <summary>1 snapshotへ渡せるpressed command数の上限。</summary>
        public const int MaximumPressedCommandCount = 64;

        private readonly int[] _requiredCommandIds;
        private readonly bool[] _pressed;
        private readonly ulong[] _pressTicks;
        private readonly ulong _maximumSpanTicks;
        private ulong _currentTick;
        private int _pressedRequiredCommandCount;
        private bool _isComplete;

        /// <summary>chordに必要なcommand数。</summary>
        public int RequiredCommandCount => _requiredCommandIds.Length;

        /// <summary>最古と最新のrequired押下edgeに許す最大tick差。</summary>
        public ulong MaximumSpanTicks => _maximumSpanTicks;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        /// <summary>処理後にrequired commandがすべて押下中か。</summary>
        public bool IsComplete => _isComplete;

        private InputChordMatcher(int[] requiredCommandIds, ulong maximumSpanTicks, ulong initialTick)
        {
            _requiredCommandIds = requiredCommandIds;
            _pressed = new bool[requiredCommandIds.Length];
            _pressTicks = new ulong[requiredCommandIds.Length];
            _maximumSpanTicks = maximumSpanTicks;
            _currentTick = initialTick;
        }

        /// <summary>required command列を複製・検証してmatcherを作る。</summary>
        public static bool TryCreate(IReadOnlyList<int> requiredCommandIds, ulong maximumSpanTicks, ulong initialTick, out InputChordMatcher matcher, out InputChordError error)
        {
            if (requiredCommandIds == null || requiredCommandIds.Count < 2 || requiredCommandIds.Count > MaximumRequiredCommandCount)
            {
                matcher = null;
                error = InputChordError.InvalidRequiredCommandCount;
                return false;
            }

            var copy = new int[requiredCommandIds.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                var commandId = requiredCommandIds[index];
                if (commandId <= 0)
                {
                    matcher = null;
                    error = InputChordError.InvalidRequiredCommandId;
                    return false;
                }

                copy[index] = commandId;
            }

            Array.Sort(copy);
            for (var index = 1; index < copy.Length; index++)
            {
                if (copy[index] != copy[index - 1]) continue;
                matcher = null;
                error = InputChordError.DuplicateRequiredCommandId;
                return false;
            }

            matcher = new InputChordMatcher(copy, maximumSpanTicks, initialTick);
            error = InputChordError.None;
            return true;
        }

        /// <summary>正のcommand idを厳密昇順に並べた現在pressed snapshotを処理する。</summary>
        public bool TrySample(ulong tick, IReadOnlyList<int> pressedCommandIds, out InputChordStatus status, out InputChordError error)
        {
            if (!IsValidPressedSnapshot(pressedCommandIds))
            {
                status = Snapshot();
                error = InputChordError.InvalidPressedSnapshot;
                return false;
            }

            if (tick < _currentTick)
            {
                status = Snapshot();
                error = InputChordError.TickMovedBackward;
                return false;
            }

            _currentTick = tick;
            var wasComplete = _isComplete;
            var pressedIndex = 0;
            var pressedRequiredCount = 0;
            for (var requiredIndex = 0; requiredIndex < _requiredCommandIds.Length; requiredIndex++)
            {
                var requiredId = _requiredCommandIds[requiredIndex];
                while (pressedIndex < pressedCommandIds.Count && pressedCommandIds[pressedIndex] < requiredId) pressedIndex++;
                var isPressed = pressedIndex < pressedCommandIds.Count && pressedCommandIds[pressedIndex] == requiredId;
                if (isPressed)
                {
                    pressedRequiredCount++;
                    if (!_pressed[requiredIndex]) _pressTicks[requiredIndex] = tick;
                }

                _pressed[requiredIndex] = isPressed;
            }

            _pressedRequiredCommandCount = pressedRequiredCount;
            _isComplete = pressedRequiredCount == _requiredCommandIds.Length;
            var pressSpanTicks = CalculatePressSpanTicks();
            var enteredComplete = _isComplete && !wasComplete;
            var triggered = enteredComplete && pressSpanTicks <= _maximumSpanTicks;
            var spanExceeded = enteredComplete && !triggered;
            var rearmed = wasComplete && !_isComplete;
            status = CreateStatus(triggered, spanExceeded, rearmed, pressSpanTicks);
            error = InputChordError.None;
            return true;
        }

        /// <summary>状態を進めず今回だけの判定flagを持たない現在statusを返す。</summary>
        public InputChordStatus Snapshot() => CreateStatus(false, false, false, CalculatePressSpanTicks());

        /// <summary>pressed状態とedge tickを破棄し、新しいsimulation tickへ初期化する。</summary>
        public void Reset(ulong tick)
        {
            _currentTick = tick;
            _pressedRequiredCommandCount = 0;
            _isComplete = false;
            Array.Clear(_pressed, 0, _pressed.Length);
            Array.Clear(_pressTicks, 0, _pressTicks.Length);
        }

        private static bool IsValidPressedSnapshot(IReadOnlyList<int> pressedCommandIds)
        {
            if (pressedCommandIds == null || pressedCommandIds.Count > MaximumPressedCommandCount) return false;
            var previous = 0;
            for (var index = 0; index < pressedCommandIds.Count; index++)
            {
                var commandId = pressedCommandIds[index];
                if (commandId <= previous) return false;
                previous = commandId;
            }

            return true;
        }

        private ulong CalculatePressSpanTicks()
        {
            if (!_isComplete) return 0;
            var minimum = _pressTicks[0];
            var maximum = _pressTicks[0];
            for (var index = 1; index < _pressTicks.Length; index++)
            {
                if (_pressTicks[index] < minimum) minimum = _pressTicks[index];
                if (_pressTicks[index] > maximum) maximum = _pressTicks[index];
            }

            return maximum - minimum;
        }

        private InputChordStatus CreateStatus(bool triggered, bool spanExceeded, bool rearmed, ulong pressSpanTicks) => new InputChordStatus(_currentTick, _requiredCommandIds.Length, _pressedRequiredCommandCount, _isComplete, triggered, spanExceeded, rearmed, pressSpanTicks);
    }
}
