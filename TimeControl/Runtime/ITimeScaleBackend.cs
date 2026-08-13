namespace TimeControl
{
    /// <summary>時間倍率の保管先をengineへ渡す内部境界。</summary>
    internal interface ITimeScaleBackend
    {
        /// <summary>現在の時間倍率を読み取る。</summary>
        /// <returns>保管先にある現在値。</returns>
        float Read();

        /// <summary>指定した時間倍率を書き込む。</summary>
        /// <param name="value">書き込む有限の時間倍率。</param>
        void Write(float value);
    }
}
