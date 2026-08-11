using System;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 決定キーで処理を 1 回走らせる行。リセット、リロード、テストデータの投入など。
    /// <para>
    /// 例外を握り潰さずログへ出すのは、デバッグメニューから呼ぶ処理ほど
    /// 未完成で落ちやすく、しかも落ちた拍子にメニューごと閉じると原因が追えないため。
    /// </para>
    /// </summary>
    public sealed class DebugAction : DebugElement
    {
        private readonly Action _action;

        /// <summary>表示名と処理を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="action">決定キーで走らせる処理。</param>
        /// <param name="subTitle">右カラムへ出す文字列。</param>
        public DebugAction(string label, Action action, string subTitle = null)
            : base(label, subTitle)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            IsExpandable = false;
        }

        /// <summary>保存対象にしない。値を持たないため。</summary>
        public override bool IsSaveable => false;

        /// <summary>処理を走らせる。例外はログへ出して飲み込み、メニューを生かしたままにする。</summary>
        public override void OnDecide()
        {
            try
            {
                _action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
