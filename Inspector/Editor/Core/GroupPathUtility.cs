using System;

namespace Inspector.Editor
{
    /// <summary>
    /// グループパス（<c>"表示/戦闘"</c>）の分解と整形。
    /// <para>
    /// パスは属性の引数として人が手で書くため、前後の空白や
    /// <c>"/戦闘"</c>・<c>"表示//戦闘"</c> のような書き方が混ざる。
    /// 表記ゆれで別グループに割れると原因が分かりにくいので、入口で 1 つの形に揃える。
    /// </para>
    /// </summary>
    internal static class GroupPathUtility
    {
        public const char Separator = '/';

        private static readonly string[] NoSegments = new string[0];

        /// <summary>
        /// 空の区切りと前後の空白を落として揃える。
        /// 中身が無くなったら <c>null</c>（グループ無しと同じ扱い）。
        /// </summary>
        public static string Normalize(string path)
        {
            var segments = Split(path);
            return segments.Length == 0 ? null : string.Join("/", segments);
        }

        /// <summary>整形済みの区切りごとの名前。</summary>
        public static string[] Split(string path)
        {
            if (string.IsNullOrEmpty(path)) return NoSegments;

            var raw = path.Split(Separator);
            var kept = 0;

            for (var i = 0; i < raw.Length; i++)
            {
                var trimmed = raw[i].Trim();
                if (trimmed.Length == 0) continue;

                raw[kept++] = trimmed;
            }

            if (kept == raw.Length) return raw;
            if (kept == 0) return NoSegments;

            var result = new string[kept];
            Array.Copy(raw, result, kept);
            return result;
        }

        /// <summary>階層の深さ。整形して空になるパスは 0。</summary>
        public static int Depth(string path) => Split(path).Length;

        /// <summary>1 つ上の階層のパス。最上位なら <c>null</c>。</summary>
        public static string Parent(string path)
        {
            var segments = Split(path);
            if (segments.Length <= 1) return null;

            return string.Join("/", segments, 0, segments.Length - 1);
        }

        /// <summary>末尾の名前。表示上の見出しになる。</summary>
        public static string Leaf(string path)
        {
            var segments = Split(path);
            return segments.Length == 0 ? null : segments[segments.Length - 1];
        }
    }
}
