using System;
using UnityEngine.InputSystem;

namespace InputGate
{
    /// <summary>所有対象Action Mapの停止前状態を保持し、全停止と完全復元を検証する。</summary>
    internal sealed class ControlledActionMapSet
    {
        private readonly InputActionMap[] _maps;
        private bool[][] _baseline;
        private bool _isBlocking;

        /// <summary>重複検査済みの実行中Action Map一覧を所有する。</summary>
        /// <param name="maps">PlayerInputから解決したAction Map一覧。</param>
        internal ControlledActionMapSet(InputActionMap[] maps)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        }

        /// <summary>所有しているAction Map数。</summary>
        internal int Count => _maps.Length;

        /// <summary>現在、停止前状態を保持して全Actionを停止中ならtrue。</summary>
        internal bool IsBlocking => _isBlocking;

        /// <summary>指定indexのAction Map instance。</summary>
        /// <param name="index">0以上Count未満のindex。</param>
        /// <returns>所有している実行中Action Map。</returns>
        internal InputActionMap GetMap(int index) => _maps[index];

        /// <summary>各Actionの有効状態を保存し、対象Mapを一括停止して結果を確認する。</summary>
        /// <param name="ownershipStillValid">各Map書換え後も同じPlayerInput Action Assetを所有中ならtrueを返す確認処理。</param>
        /// <param name="lifecycleStillActive">取得要求を開始したController lifecycleが継続中ならtrueを返す確認処理。</param>
        /// <returns>全Actionを停止できればNone。</returns>
        internal InputGateError BeginBlocking(Func<bool> ownershipStillValid, Func<bool> lifecycleStillActive)
        {
            if (_isBlocking) return CheckBlocked();
            _baseline = CaptureStates();
            try
            {
                for (var i = 0; i < _maps.Length; i++)
                {
                    _maps[i].Disable();
                    if (!ownershipStillValid())
                    {
                        ForgetBaseline();
                        return InputGateError.ExternalActionStateChanged;
                    }

                    if (!lifecycleStillActive())
                    {
                        var rollbackError = TryRollback(ownershipStillValid);
                        return rollbackError == InputGateError.ExternalActionStateChanged
                            ? rollbackError
                            : InputGateError.ControllerUnavailable;
                    }
                }
            }
            catch (Exception)
            {
                if (!ownershipStillValid())
                {
                    ForgetBaseline();
                    return InputGateError.ExternalActionStateChanged;
                }

                var rollbackError = TryRollback(ownershipStillValid);
                if (!lifecycleStillActive() && rollbackError != InputGateError.ExternalActionStateChanged)
                {
                    return InputGateError.ControllerUnavailable;
                }

                return rollbackError == InputGateError.ExternalActionStateChanged
                    ? rollbackError
                    : InputGateError.ActionStateChangeFailed;
            }

            if (!AreAllActionsDisabled())
            {
                var rollbackError = TryRollback(ownershipStillValid);
                return rollbackError == InputGateError.ExternalActionStateChanged
                    ? rollbackError
                    : InputGateError.ActionStateChangeFailed;
            }

            _isBlocking = true;
            return InputGateError.None;
        }

        /// <summary>停止中に全Actionが無効のままか確認する。</summary>
        /// <returns>期待どおりならNone、外部有効化ならExternalActionStateChanged。</returns>
        internal InputGateError CheckBlocked()
        {
            if (!_isBlocking) return InputGateError.None;
            return AreAllActionsDisabled() ? InputGateError.None : InputGateError.ExternalActionStateChanged;
        }

        /// <summary>外部変更がない場合だけ、停止前に有効だったActionを個別に復元する。</summary>
        /// <param name="ownershipStillValid">各Action書換え前後も同じPlayerInput Action Assetを所有中ならtrueを返す確認処理。</param>
        /// <returns>完全復元ならNone、外部変更または書戻し失敗なら対応する理由。</returns>
        internal InputGateError Restore(Func<bool> ownershipStillValid)
        {
            if (!_isBlocking) return InputGateError.None;
            if (!ownershipStillValid())
            {
                ForgetBaseline();
                return InputGateError.ExternalActionStateChanged;
            }

            if (!AreAllActionsDisabled())
            {
                ForgetBaseline();
                return InputGateError.ExternalActionStateChanged;
            }

            try
            {
                var applyError = ApplyBaseline(ownershipStillValid);
                if (applyError != InputGateError.None)
                {
                    ForgetBaseline();
                    return applyError;
                }
            }
            catch (Exception)
            {
                ForgetBaseline();
                return InputGateError.ActionStateChangeFailed;
            }

            if (!ownershipStillValid())
            {
                ForgetBaseline();
                return InputGateError.ExternalActionStateChanged;
            }

            var restored = MatchesBaseline();
            ForgetBaseline();
            return restored ? InputGateError.None : InputGateError.ActionStateChangeFailed;
        }

        /// <summary>外部状態を変更せず、保持していた停止前状態だけを破棄する。</summary>
        internal void Abandon()
        {
            ForgetBaseline();
        }

        private bool[][] CaptureStates()
        {
            var result = new bool[_maps.Length][];
            for (var mapIndex = 0; mapIndex < _maps.Length; mapIndex++)
            {
                var actions = _maps[mapIndex].actions;
                result[mapIndex] = new bool[actions.Count];
                for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    result[mapIndex][actionIndex] = actions[actionIndex].enabled;
                }
            }

            return result;
        }

        private bool AreAllActionsDisabled()
        {
            for (var mapIndex = 0; mapIndex < _maps.Length; mapIndex++)
            {
                var actions = _maps[mapIndex].actions;
                for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    if (actions[actionIndex].enabled) return false;
                }
            }

            return true;
        }

        private InputGateError ApplyBaseline(Func<bool> ownershipStillValid)
        {
            for (var mapIndex = 0; mapIndex < _maps.Length; mapIndex++)
            {
                var actions = _maps[mapIndex].actions;
                var states = _baseline[mapIndex];
                if (actions.Count != states.Length) return InputGateError.ActionStateChangeFailed;
                for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    if (!states[actionIndex] || actions[actionIndex].enabled) continue;
                    if (!ownershipStillValid()) return InputGateError.ExternalActionStateChanged;
                    actions[actionIndex].Enable();
                    if (!ownershipStillValid()) return InputGateError.ExternalActionStateChanged;
                }
            }

            return InputGateError.None;
        }

        private bool MatchesBaseline()
        {
            for (var mapIndex = 0; mapIndex < _maps.Length; mapIndex++)
            {
                var actions = _maps[mapIndex].actions;
                var states = _baseline[mapIndex];
                if (actions.Count != states.Length) return false;
                for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    if (actions[actionIndex].enabled != states[actionIndex]) return false;
                }
            }

            return true;
        }

        private InputGateError TryRollback(Func<bool> ownershipStillValid)
        {
            try
            {
                return ApplyBaseline(ownershipStillValid);
            }
            catch (Exception)
            {
                // 部分停止に失敗しても元の例外を外へ送らず、Controllerを失敗状態へ移す。
                return InputGateError.ActionStateChangeFailed;
            }
            finally
            {
                ForgetBaseline();
            }
        }

        private void ForgetBaseline()
        {
            _baseline = null;
            _isBlocking = false;
        }
    }
}
