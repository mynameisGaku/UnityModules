using UnityEngine;

namespace StartupFlow
{
    /// <summary>起動時に1件ずつ実行される、識別子と順序を持つ非同期処理。</summary>
    public interface IStartupStep
    {
        /// <summary>結果と状態通知で使う一意な識別子。空白以外で128文字以内にする。</summary>
        string Id { get; }

        /// <summary>小さい値から実行する順序。同値の場合はIdのordinal順で実行する。</summary>
        int Order { get; }

        /// <summary>このstepを1回実行する。失敗は例外として返し、キャンセルはcontextのtokenへ従う。</summary>
        /// <param name="context">キャンセル状態と進捗通知を提供する、この実行だけのcontext。</param>
        /// <returns>stepの完了を表す、一度だけawait可能な処理。</returns>
        Awaitable ExecuteAsync(StartupStepContext context);
    }
}
