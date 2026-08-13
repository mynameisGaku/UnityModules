using System;

namespace SaveSystem
{
    /// <summary>保存スロット名の共通検証。</summary>
    public static class SaveSlot
    {
        /// <summary>許可する最大文字数。</summary>
        public const int MaxLength = 64;

        /// <summary>
        /// ファイル名として全対応環境で安全なスロット名かを調べる。
        /// 英数字、日本語などの文字と数字、ハイフン、アンダースコアだけを許可する。
        /// </summary>
        /// <param name="slot">調べるスロット名。</param>
        /// <returns>使用できるスロット名なら true、それ以外は false。</returns>
        public static bool IsValid(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) || slot.Length > MaxLength) return false;
            if (slot.EndsWith(".", StringComparison.Ordinal) || slot.EndsWith(" ", StringComparison.Ordinal)) return false;
            if (IsReservedDeviceName(slot)) return false;

            for (var i = 0; i < slot.Length; i++)
            {
                var character = slot[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_') return false;
            }

            return true;
        }

        private static bool IsReservedDeviceName(string slot)
        {
            switch (slot.ToUpperInvariant())
            {
                case "CON":
                case "PRN":
                case "AUX":
                case "NUL":
                case "COM1":
                case "COM2":
                case "COM3":
                case "COM4":
                case "COM5":
                case "COM6":
                case "COM7":
                case "COM8":
                case "COM9":
                case "LPT1":
                case "LPT2":
                case "LPT3":
                case "LPT4":
                case "LPT5":
                case "LPT6":
                case "LPT7":
                case "LPT8":
                case "LPT9":
                    return true;
                default:
                    return false;
            }
        }
    }
}
