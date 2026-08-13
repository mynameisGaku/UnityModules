using System.Collections.Generic;

namespace SaveSystem
{
    /// <summary>
    /// 保存文字列をスロット単位で同期保管する。
    /// <see cref="SaveService"/> は各操作が戻るまで完了を待つため、実装は非同期処理を残さない。
    /// 同じスロットへの操作は呼び出し側が順番に行い、存在しない場合だけ false または空一覧を返す。
    /// 読み書きや列挙自体の失敗はストレージ固有の例外として送出し、成功や未存在として隠さない。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>主データを同期して読む。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <param name="contents">読めた保存文字列。存在しない場合は null。</param>
        /// <returns>主データが存在して読み取れた場合は true。存在しない場合だけ false。</returns>
        bool TryRead(string slot, out string contents);

        /// <summary>1つ前のバックアップを同期して読む。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <param name="contents">読めた保存文字列。存在しない場合は null。</param>
        /// <returns>バックアップが存在して読み取れた場合は true。存在しない場合だけ false。</returns>
        bool TryReadBackup(string slot, out string contents);

        /// <summary>主データを書き換え、完了した時点で以前の主データを1世代バックアップとして残す。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <param name="contents">保存文字列。</param>
        void Write(string slot, string contents);

        /// <summary>バックアップを主データへ同期して戻す。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <returns>復旧できた場合は true。バックアップが存在しない場合だけ false。</returns>
        bool RestoreBackup(string slot);

        /// <summary>主データ、バックアップ、および同じスロットの処理残骸を同期して削除する。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <returns>いずれかを削除した場合は true。すべて存在しない場合だけ false。</returns>
        bool Delete(string slot);

        /// <summary>
        /// 主データまたはバックアップが存在するスロットの重複しない名前順スナップショットを返す。
        /// スロットが存在しない場合は空一覧を返し、列挙失敗は例外として送出する。null は返さない。
        /// </summary>
        /// <returns>名前順で重複のないスロット名の読み取り専用スナップショット。</returns>
        IReadOnlyList<string> ListSlots();
    }
}
