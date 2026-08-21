using System;

namespace GameplayDamage
{
    /// <summary>元damageへ明示した軽減層を入力順に適用し、stateを変更せず全明細を返します。</summary>
    public static class DamageMitigationEvaluator
    {
        /// <summary>1回に受理する軽減層の最大件数です。</summary>
        public const int MaximumLayerCount = 32;

        /// <summary>元damageと軽減層を検証し、成功時だけ入力順の軽減評価を返します。</summary>
        /// <param name="damage">0以上の有限な元damageです。</param>
        /// <param name="layers">0〜32件の軽減層です。配列と要素は変更しません。</param>
        /// <param name="evaluation">成功時の軽減評価です。</param>
        /// <param name="error">失敗理由です。成功時は<see cref="DamageMitigationError.None"/>です。</param>
        /// <returns>入力が有効で評価を作成できた場合はtrueです。</returns>
        public static bool TryEvaluate(double damage, DamageMitigationLayer[] layers, out DamageMitigationEvaluation evaluation, out DamageMitigationError error)
        {
            evaluation = null;
            if (double.IsNaN(damage) || double.IsInfinity(damage))
            {
                error = DamageMitigationError.NonFiniteDamage;
                return false;
            }

            if (damage < 0d)
            {
                error = DamageMitigationError.NegativeDamage;
                return false;
            }

            if (layers == null)
            {
                error = DamageMitigationError.NullLayers;
                return false;
            }

            if (layers.Length > MaximumLayerCount)
            {
                error = DamageMitigationError.InvalidLayerCount;
                return false;
            }

            for (var index = 0; index < layers.Length; index++)
            {
                var layer = layers[index];
                if (layer.LayerId <= 0)
                {
                    error = DamageMitigationError.InvalidLayerId;
                    return false;
                }

                if (layer.Kind != DamageMitigationKind.FlatReduction && layer.Kind != DamageMitigationKind.RatioReduction)
                {
                    error = DamageMitigationError.InvalidKind;
                    return false;
                }

                if (double.IsNaN(layer.Value) || double.IsInfinity(layer.Value))
                {
                    error = DamageMitigationError.NonFiniteValue;
                    return false;
                }

                if (layer.Value < 0d)
                {
                    error = DamageMitigationError.NegativeValue;
                    return false;
                }

                if (layer.Kind == DamageMitigationKind.RatioReduction && layer.Value > 1d)
                {
                    error = DamageMitigationError.RatioOutOfRange;
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (layers[previous].LayerId != layer.LayerId) continue;
                    error = DamageMitigationError.DuplicateLayerId;
                    return false;
                }
            }

            evaluation = DamageMitigationEngine.Evaluate(damage, layers);
            error = DamageMitigationError.None;
            return true;
        }
    }
}
