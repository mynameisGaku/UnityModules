// SPDX-License-Identifier: MIT

namespace PlayerOptions
{
    /// <summary>player option操作の成否、操作後状態、補正情報を返す。</summary>
    public readonly struct PlayerOptionsResult
    {
        private PlayerOptionsResult(
            bool isSuccess,
            PlayerOptionsError error,
            string message,
            PlayerOptionsState state,
            PlayerOptionsWarning warnings,
            bool usedDefaults,
            bool wasAdjusted,
            bool requiresSave,
            PlayerOptionsField affectedFields,
            PlayerOptionsField rollbackFailedFields,
            PlayerOptionsField outcomeUnknownFields)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
            State = state;
            Warnings = warnings;
            UsedDefaults = usedDefaults;
            WasAdjusted = wasAdjusted;
            RequiresSave = requiresSave;
            AffectedFields = affectedFields;
            RollbackFailedFields = rollbackFailedFields;
            OutcomeUnknownFields = outcomeUnknownFields;
        }

        /// <summary>要求された一つの操作が契約どおり完了した場合はtrue。</summary>
        public bool IsSuccess { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public PlayerOptionsError Error { get; }

        /// <summary>画面またはlogへ出せる短い説明。</summary>
        public string Message { get; }

        /// <summary>操作完了時、または失敗で維持されたservice状態。</summary>
        public PlayerOptionsState State { get; }

        /// <summary>操作時に行った補正またはUnity側の反映条件。</summary>
        public PlayerOptionsWarning Warnings { get; }

        /// <summary>保存値の一部または全部ではなくtyped defaultを採用した場合はtrue。</summary>
        public bool UsedDefaults { get; }

        /// <summary>migration、環境fallback、または正規化により保存文書と異なる状態を採用した場合はtrue。</summary>
        public bool WasAdjusted { get; }

        /// <summary>読込後の正規化状態を次回用に明示保存した方がよい場合はtrue。</summary>
        public bool RequiresSave { get; }

        /// <summary>Unity runtimeへの書込を試みたfield。非Apply操作ではNone。</summary>
        public PlayerOptionsField AffectedFields { get; }

        /// <summary>適用失敗後に変更前の値へrollbackできなかったfield。</summary>
        public PlayerOptionsField RollbackFailedFields { get; }

        /// <summary>書込呼出しの例外後に反映結果を確認できないfield。</summary>
        public PlayerOptionsField OutcomeUnknownFields { get; }

        /// <summary>指定状態を持つ成功結果を作る。</summary>
        internal static PlayerOptionsResult Success(
            PlayerOptionsState state,
            string message = null,
            PlayerOptionsWarning warnings = PlayerOptionsWarning.None,
            bool usedDefaults = false,
            bool wasAdjusted = false,
            bool requiresSave = false,
            PlayerOptionsField affectedFields = PlayerOptionsField.None,
            PlayerOptionsField rollbackFailedFields = PlayerOptionsField.None,
            PlayerOptionsField outcomeUnknownFields = PlayerOptionsField.None)
        {
            return new PlayerOptionsResult(
                true,
                PlayerOptionsError.None,
                message,
                state,
                warnings,
                usedDefaults,
                wasAdjusted,
                requiresSave,
                affectedFields,
                rollbackFailedFields,
                outcomeUnknownFields);
        }

        /// <summary>service状態を変更しない失敗結果を作る。</summary>
        internal static PlayerOptionsResult Failure(
            PlayerOptionsState state,
            PlayerOptionsError error,
            string message,
            PlayerOptionsWarning warnings = PlayerOptionsWarning.None,
            PlayerOptionsField affectedFields = PlayerOptionsField.None,
            PlayerOptionsField rollbackFailedFields = PlayerOptionsField.None,
            PlayerOptionsField outcomeUnknownFields = PlayerOptionsField.None)
        {
            var safeError = error == PlayerOptionsError.None ? PlayerOptionsError.RuntimeUnavailable : error;
            return new PlayerOptionsResult(
                false,
                safeError,
                message,
                state,
                warnings,
                false,
                false,
                false,
                affectedFields,
                rollbackFailedFields,
                outcomeUnknownFields);
        }
    }
}
