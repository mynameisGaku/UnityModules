namespace GameplayDamage
{
    /// <summary>検証済みの軽減層を入力順に適用する内部計算です。</summary>
    internal static class DamageMitigationEngine
    {
        internal static DamageMitigationEvaluation Evaluate(double damage, DamageMitigationLayer[] layers)
        {
            var current = damage;
            var steps = new DamageMitigationStep[layers.Length];
            for (var index = 0; index < layers.Length; index++)
            {
                var layer = layers[index];
                var input = current;
                var requested = layer.Kind == DamageMitigationKind.FlatReduction ? layer.Value : input * layer.Value;
                var applied = requested > input ? input : requested;
                current = input - applied;
                if (current < 0d) current = 0d;
                steps[index] = new DamageMitigationStep(layer.LayerId, layer.Kind, layer.Value, input, requested, applied, current);
            }

            return new DamageMitigationEvaluation(damage, current, steps);
        }
    }
}
