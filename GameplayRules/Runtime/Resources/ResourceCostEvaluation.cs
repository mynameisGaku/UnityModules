namespace GameplayResources
{
    /// <summary>全costの支払可否と、cost入力順のimmutableなresource別明細を保持します。</summary>
    public sealed class ResourceCostEvaluation
    {
        private readonly ResourceCostLine[] _lines;

        internal ResourceCostEvaluation(bool canPay, ResourceCostLine[] lines)
        {
            CanPay = canPay;
            _lines = lines;
        }

        /// <summary>全costを不足なく支払える場合はtrueです。</summary>
        public bool CanPay { get; }
        /// <summary>評価したcost明細数を取得します。</summary>
        public int LineCount => _lines.Length;

        /// <summary>cost入力順のindexからresource別明細を取得します。</summary>
        /// <param name="index">0以上LineCount未満のindexです。</param>
        /// <param name="line">indexが有効な場合に明細を返します。</param>
        /// <returns>indexが有効な場合はtrueです。</returns>
        public bool TryGetLine(int index, out ResourceCostLine line)
        {
            if (index < 0 || index >= _lines.Length)
            {
                line = default;
                return false;
            }

            line = _lines[index];
            return true;
        }
    }
}
