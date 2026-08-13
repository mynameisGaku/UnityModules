using System;

namespace SaveSystem
{
    /// <summary>ペイロードと検証情報をファイルへまとめる内部形式。</summary>
    [Serializable]
    internal sealed class SaveEnvelope
    {
        /// <summary>このモジュールが解釈する外側の形式版。</summary>
        public int FormatVersion;

        /// <summary>ゲーム側が決めるデータ版。</summary>
        public string DataVersion;

        /// <summary>保存時に指定された型を安定して識別する名前。</summary>
        public string TypeId;

        /// <summary>UTC 保存時刻の ticks。</summary>
        public long SavedAtUtcTicks;

        /// <summary>利用側のシリアライザーが作った保存文字列。</summary>
        public string Payload;

        /// <summary>外側の識別情報とペイロードから作る SHA-256。</summary>
        public string Checksum;
    }
}
