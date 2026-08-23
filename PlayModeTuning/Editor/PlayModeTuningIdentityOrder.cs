using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor
{
    /// <summary>Orders property work by direct identity fields instead of hash values or caller selection order.</summary>
    internal static class PlayModeTuningIdentityOrder
    {
        internal static IOrderedEnumerable<T> OrderProperties<T>(IEnumerable<T> source, Func<T, PlayModeTuningPropertyRecord> recordSelector)
        {
            if (recordSelector == null)
                throw new ArgumentNullException(nameof(recordSelector));
            return (source ?? Enumerable.Empty<T>())
                .OrderBy(item => Value(recordSelector(item)?.globalObjectId), StringComparer.Ordinal)
                .ThenBy(item => Value(recordSelector(item)?.propertyPath), StringComparer.Ordinal)
                .ThenBy(item => Value(recordSelector(item)?.propertyType), StringComparer.Ordinal)
                .ThenBy(item => Value(recordSelector(item)?.numericType), StringComparer.Ordinal)
                .ThenBy(item => Value(recordSelector(item)?.componentKey), StringComparer.Ordinal);
        }

        internal static IOrderedEnumerable<T> OrderComponents<T>(IEnumerable<T> source, Func<T, string> componentKeySelector, Func<T, string> scenePathSelector, IEnumerable<PlayModeTuningPropertyRecord> properties)
        {
            if (componentKeySelector == null)
                throw new ArgumentNullException(nameof(componentKeySelector));
            if (scenePathSelector == null)
                throw new ArgumentNullException(nameof(scenePathSelector));
            var firstPropertyByComponent = OrderProperties(properties, item => item)
                .GroupBy(item => item.componentKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            return (source ?? Enumerable.Empty<T>())
                .OrderBy(item => DirectGlobalObjectId(firstPropertyByComponent, componentKeySelector(item)), StringComparer.Ordinal)
                .ThenBy(item => DirectFirstPropertyPath(firstPropertyByComponent, componentKeySelector(item)), StringComparer.Ordinal)
                .ThenBy(item => Value(scenePathSelector(item)), StringComparer.Ordinal)
                .ThenBy(item => Value(componentKeySelector(item)), StringComparer.Ordinal);
        }

        private static string DirectGlobalObjectId(IReadOnlyDictionary<string, PlayModeTuningPropertyRecord> records, string componentKey)
        {
            return records.TryGetValue(Value(componentKey), out var record) ? Value(record.globalObjectId) : string.Empty;
        }

        private static string DirectFirstPropertyPath(IReadOnlyDictionary<string, PlayModeTuningPropertyRecord> records, string componentKey)
        {
            return records.TryGetValue(Value(componentKey), out var record) ? Value(record.propertyPath) : string.Empty;
        }

        private static string Value(string value)
        {
            return value ?? string.Empty;
        }
    }
}
