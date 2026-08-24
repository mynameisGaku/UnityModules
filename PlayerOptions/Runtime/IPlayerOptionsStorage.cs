// SPDX-License-Identifier: MIT

namespace PlayerOptions
{
    /// <summary>
    /// version付きplayer option文書を一つの同期値として保存する。
    /// 未存在だけをfalseで返し、読込・書込失敗は例外としてserviceへ伝える。
    /// </summary>
    public interface IPlayerOptionsStorage
    {
        /// <summary>保存済み文書を同期して読む。</summary>
        /// <param name="contents">存在する場合の文書。未存在時はnull。</param>
        /// <returns>文書が存在する場合はtrue。未存在の場合だけfalse。</returns>
        bool TryRead(out string contents);

        /// <summary>指定文書を一つのlogical valueとして同期保存する。</summary>
        /// <param name="contents">保存するnull以外の文書。</param>
        void Write(string contents);
    }
}
