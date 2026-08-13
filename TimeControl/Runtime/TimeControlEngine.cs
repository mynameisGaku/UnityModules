using System;

namespace TimeControl
{
    /// <summary>UnityのComponent寿命から独立して、1回の所有期間の時間倍率を検査して適用する。</summary>
    internal sealed class TimeControlEngine
    {
        private readonly ITimeScaleBackend _backend;
        private float _baselineTimeScale;
        private float _expectedTimeScale;
        private bool _hasReservation;
        private bool _isControlling;

        /// <summary>指定した時間倍率保管先を使用するengineを作る。</summary>
        /// <param name="backend">時間倍率を読み書きする保管先。</param>
        internal TimeControlEngine(ITimeScaleBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>外部変更の検出を続ける所有予約を保持している場合はtrue。</summary>
        internal bool HasReservation => _hasReservation;

        /// <summary>正常に時間倍率を管理中ならtrue。</summary>
        internal bool IsControlling => _isControlling;

        /// <summary>所有開始時に読み取った基準値。</summary>
        internal float BaselineTimeScale => _baselineTimeScale;

        /// <summary>正常時に保管先へあるべき値。</summary>
        internal float ExpectedTimeScale => _expectedTimeScale;

        /// <summary>保管先の現在値を基準値として所有を開始する。</summary>
        /// <param name="actualTimeScale">読み取りに成功した現在値。</param>
        /// <returns>開始できればNone、そうでなければ読取または範囲エラー。</returns>
        internal TimeControlError TryStart(out float actualTimeScale)
        {
            actualTimeScale = _expectedTimeScale;
            if (_hasReservation) return _isControlling ? TimeControlError.None : TimeControlError.ControllerUnavailable;

            if (!TryRead(out actualTimeScale)) return TimeControlError.TimeScaleWriteFailed;
            var validation = TimeScaleResolver.ValidateBaseline(actualTimeScale);
            if (validation != TimeControlError.None) return validation;

            _baselineTimeScale = actualTimeScale;
            _expectedTimeScale = actualTimeScale;
            _hasReservation = true;
            _isControlling = true;
            return TimeControlError.None;
        }

        /// <summary>現在値が最後に適用した値と同じか確認する。</summary>
        /// <param name="actualTimeScale">確認時に読み取れた値。読取失敗時は直前の期待値。</param>
        /// <returns>一致すればNone、外部変更または読取失敗なら対応する理由。</returns>
        internal TimeControlError CheckExpected(out float actualTimeScale)
        {
            actualTimeScale = _expectedTimeScale;
            if (!_hasReservation || !_isControlling) return TimeControlError.ControllerUnavailable;
            if (!TryRead(out actualTimeScale)) return TimeControlError.TimeScaleWriteFailed;
            return actualTimeScale.Equals(_expectedTimeScale) ? TimeControlError.None : TimeControlError.ExternalTimeScaleChanged;
        }

        /// <summary>基準値へ相対倍率を適用し、書き戻し後の一致まで確認する。</summary>
        /// <param name="multiplier">適用する検査済み相対倍率。</param>
        /// <param name="actualTimeScale">適用後に読み取れた値。</param>
        /// <returns>適用できればNone、そうでなければ範囲または書込理由。</returns>
        internal TimeControlError Apply(float multiplier, out float actualTimeScale)
        {
            actualTimeScale = _expectedTimeScale;
            if (!_hasReservation || !_isControlling) return TimeControlError.ControllerUnavailable;

            var validation = TimeScaleResolver.ValidateMultiplier(_baselineTimeScale, multiplier, out var effectiveTimeScale);
            if (validation != TimeControlError.None) return validation;
            if (!TryWriteAndConfirm(effectiveTimeScale, out actualTimeScale)) return TimeControlError.TimeScaleWriteFailed;

            _expectedTimeScale = effectiveTimeScale;
            return TimeControlError.None;
        }

        /// <summary>異常検出後に管理を止めるが、外部値を保護するため所有予約は維持する。</summary>
        internal void Fault()
        {
            _isControlling = false;
        }

        /// <summary>健康な所有期間だけ基準値を復元し、成功にかかわらず予約を終える。</summary>
        /// <param name="actualTimeScale">終了時に読み取れた値。</param>
        /// <returns>正常終了ならNone、外部変更または復元失敗なら対応する理由。</returns>
        internal TimeControlError Stop(out float actualTimeScale)
        {
            actualTimeScale = _expectedTimeScale;
            if (!_hasReservation) return TimeControlError.ControllerUnavailable;

            var result = TimeControlError.None;
            if (_isControlling)
            {
                result = CheckExpected(out actualTimeScale);
                if (result == TimeControlError.None && !TryWriteAndConfirm(_baselineTimeScale, out actualTimeScale))
                {
                    result = TimeControlError.TimeScaleWriteFailed;
                }
            }
            else if (!TryRead(out actualTimeScale))
            {
                result = TimeControlError.TimeScaleWriteFailed;
            }

            _hasReservation = false;
            _isControlling = false;
            return result;
        }

        private bool TryRead(out float value)
        {
            value = _expectedTimeScale;
            try
            {
                value = _backend.Read();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryWriteAndConfirm(float value, out float actualTimeScale)
        {
            actualTimeScale = _expectedTimeScale;
            try
            {
                _backend.Write(value);
                actualTimeScale = _backend.Read();
                return TimeScaleResolver.IsFinite(actualTimeScale) && actualTimeScale.Equals(value);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
