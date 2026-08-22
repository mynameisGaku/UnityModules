// SPDX-License-Identifier: MIT

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupChange
    {
        internal ProjectSetupChange(ProjectSetupSettingKey key, string label, string currentValue, string desiredValue)
        {
            Key = key;
            Label = label;
            CurrentValue = currentValue;
            DesiredValue = desiredValue;
        }

        internal ProjectSetupSettingKey Key { get; }
        internal string Label { get; }
        internal string CurrentValue { get; }
        internal string DesiredValue { get; }
    }
}
