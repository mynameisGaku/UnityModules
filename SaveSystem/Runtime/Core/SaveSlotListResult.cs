using System;
using System.Collections.Generic;

namespace SaveSystem
{
    /// <summary>保存スロット一覧の取得結果。</summary>
    public readonly struct SaveSlotListResult
    {
        private SaveSlotListResult(bool isSuccess, IReadOnlyList<string> slots, SaveError error, string message)
        {
            IsSuccess = isSuccess;
            Slots = slots;
            Error = error;
            Message = message;
        }

        /// <summary>一覧の取得に成功した場合は true。</summary>
        public bool IsSuccess { get; }

        /// <summary>取得時点で固定した保存スロット一覧。失敗時は空。</summary>
        public IReadOnlyList<string> Slots { get; }

        /// <summary>失敗理由。成功時は <see cref="SaveError.None"/>。</summary>
        public SaveError Error { get; }

        /// <summary>ログや画面表示に使える短い説明。</summary>
        public string Message { get; }

        /// <summary>保存先から取得した一覧を複製して成功結果を作る。</summary>
        /// <param name="slots">取得した保存スロット一覧。null は空一覧として扱う。</param>
        /// <returns>複製したスロット一覧を持つ成功結果。</returns>
        public static SaveSlotListResult Success(IReadOnlyList<string> slots)
        {
            if (slots == null || slots.Count == 0) return new SaveSlotListResult(true, Array.Empty<string>(), SaveError.None, string.Empty);

            var snapshot = new string[slots.Count];
            for (var i = 0; i < slots.Count; i++) snapshot[i] = slots[i];
            return new SaveSlotListResult(true, Array.AsReadOnly(snapshot), SaveError.None, string.Empty);
        }

        /// <summary>一覧を持たない失敗結果を作る。</summary>
        /// <param name="error">失敗理由。None は指定できない。</param>
        /// <param name="message">失敗内容。</param>
        /// <returns>空の一覧と指定した失敗理由を持つ失敗結果。</returns>
        public static SaveSlotListResult Failure(SaveError error, string message) =>
            new SaveSlotListResult(false, Array.Empty<string>(), error == SaveError.None ? SaveError.StorageFailed : error, message ?? string.Empty);
    }
}
