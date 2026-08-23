namespace PlayModeTuning.Editor
{
    /// <summary>Describes one selected value difference without exposing mutable engine state.</summary>
    public sealed class PlayModeTuningChange
    {
        internal PlayModeTuningChange(string targetName, string componentType, string propertyPath, PlayModeTuningValueKind valueKind, string beforeValue, string afterValue)
        {
            TargetName = targetName ?? string.Empty;
            ComponentType = componentType ?? string.Empty;
            PropertyPath = propertyPath ?? string.Empty;
            ValueKind = valueKind;
            BeforeValue = beforeValue ?? string.Empty;
            AfterValue = afterValue ?? string.Empty;
        }

        public string TargetName { get; }
        public string ComponentType { get; }
        public string PropertyPath { get; }
        public PlayModeTuningValueKind ValueKind { get; }
        public string BeforeValue { get; }
        public string AfterValue { get; }
    }
}
