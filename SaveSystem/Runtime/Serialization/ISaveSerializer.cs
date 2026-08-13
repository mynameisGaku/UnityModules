namespace SaveSystem
{
    /// <summary>ゲームデータと保存用文字列を相互変換する。</summary>
    public interface ISaveSerializer
    {
        /// <summary>値を保存用文字列へ変換する。変換できない場合は例外を投げる。</summary>
        /// <typeparam name="T">保存する値の型。</typeparam>
        /// <param name="value">保存する値。</param>
        /// <returns>保存先へ書き込める形式へ変換した文字列。</returns>
        string Serialize<T>(T value);

        /// <summary>保存用文字列を値へ戻す。変換できない場合は例外を投げる。</summary>
        /// <typeparam name="T">読み込む値の型。</typeparam>
        /// <param name="serialized">保存されていた文字列。</param>
        /// <returns>保存用文字列から復元した値。</returns>
        T Deserialize<T>(string serialized);
    }
}
