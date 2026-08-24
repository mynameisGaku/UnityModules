using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Switch;
using UnityEngine.InputSystem.XInput;

namespace InputDeviceDisplay
{
    internal static class InputDeviceDisplayClassifier
    {
        /// <summary>layout完全一致の上書きを優先し、端末の型から表記体系を判定する。</summary>
        /// <param name="device">判定するInput System端末。</param>
        /// <param name="exactLayoutOverrides">layout名を完全一致で置き換える設定。</param>
        /// <returns>端末に対応する表記体系。対応外またはnullならUnknown。</returns>
        internal static InputDeviceDisplayStyle Classify(
            InputDevice device,
            InputDeviceDisplayLayoutOverride[] exactLayoutOverrides)
        {
            if (device == null) return InputDeviceDisplayStyle.Unknown;

            if (exactLayoutOverrides != null)
            {
                var layoutName = device.layout;
                for (var i = 0; i < exactLayoutOverrides.Length; i++)
                {
                    var item = exactLayoutOverrides[i];
                    if (item != null && string.Equals(item.LayoutName, layoutName, StringComparison.Ordinal))
                    {
                        return item.Style;
                    }
                }
            }

            if (device is Keyboard || device is Mouse) return InputDeviceDisplayStyle.KeyboardMouse;
            if (device is XInputController) return InputDeviceDisplayStyle.XboxStyleGamepad;
            if (device is DualShockGamepad) return InputDeviceDisplayStyle.PlayStationStyleGamepad;
            if (device is SwitchProController) return InputDeviceDisplayStyle.SwitchStyleGamepad;
            if (device is Gamepad) return InputDeviceDisplayStyle.GenericGamepad;
            if (device is Touchscreen) return InputDeviceDisplayStyle.Touch;
            return InputDeviceDisplayStyle.Unknown;
        }
    }
}
