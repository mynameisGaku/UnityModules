using System;

namespace SaveSystem
{
    /// <summary>保存データと一緒に記録される識別情報。</summary>
    public readonly struct SaveMetadata
    {
        /// <summary>保存スロット、データ版、保存時刻、復旧元を指定して作る。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <param name="dataVersion">ゲーム側が決めるデータ版。</param>
        /// <param name="savedAtUtc">UTC の保存時刻。</param>
        /// <param name="recoveredFromBackup">バックアップから復旧した場合は true。</param>
        public SaveMetadata(string slot, string dataVersion, DateTime savedAtUtc, bool recoveredFromBackup)
        {
            Slot = slot;
            DataVersion = dataVersion;
            SavedAtUtc = savedAtUtc;
            RecoveredFromBackup = recoveredFromBackup;
        }

        /// <summary>保存スロット。</summary>
        public string Slot { get; }

        /// <summary>ゲーム側が決めるデータ版。</summary>
        public string DataVersion { get; }

        /// <summary>UTC の保存時刻。</summary>
        public DateTime SavedAtUtc { get; }

        /// <summary>主ファイルを読めず、バックアップから復旧した場合は true。</summary>
        public bool RecoveredFromBackup { get; }
    }
}
