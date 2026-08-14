using System.Text;

namespace DiagnosticsContext
{
    /// <summary>Unicode scalar境界を壊さずに入力検証と有界copyを行う。</summary>
    internal static class DiagnosticsText
    {
        /// <summary>空でなく、正しいUnicodeで、scalar上限内ならtrueを返す。</summary>
        /// <param name="value">検証する利用側入力。</param>
        /// <param name="maximumScalarCount">許容するUnicode scalar数。</param>
        /// <returns>入力をそのまま保持できる場合はtrue。</returns>
        internal static bool IsValidRequiredInput(string value, int maximumScalarCount)
        {
            if (string.IsNullOrWhiteSpace(value) || maximumScalarCount < 1) return false;

            var scalarCount = 0;
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])) return false;
                    index++;
                }
                else if (char.IsLowSurrogate(current))
                {
                    return false;
                }

                scalarCount++;
                if (scalarCount > maximumScalarCount) return false;
            }

            return true;
        }

        /// <summary>不正surrogateを置換し、Unicode scalar上限で安全に切り詰める。</summary>
        /// <param name="value">callbackまたはreasonから受け取った文字列。</param>
        /// <param name="maximumScalarCount">保持する最大Unicode scalar数。</param>
        /// <returns>正しいUnicodeだけを含む有界文字列。</returns>
        internal static string NormalizeAndTruncate(string value, int maximumScalarCount)
        {
            if (string.IsNullOrEmpty(value) || maximumScalarCount < 1) return string.Empty;

            var builder = new StringBuilder(System.Math.Min(value.Length, maximumScalarCount * 2));
            var scalarCount = 0;
            for (var index = 0; index < value.Length && scalarCount < maximumScalarCount; index++)
            {
                var current = value[index];
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(current);
                        builder.Append(value[++index]);
                    }
                    else
                    {
                        builder.Append('\uFFFD');
                    }
                }
                else if (char.IsLowSurrogate(current))
                {
                    builder.Append('\uFFFD');
                }
                else
                {
                    builder.Append(current);
                }

                scalarCount++;
            }

            return builder.ToString();
        }
    }
}
