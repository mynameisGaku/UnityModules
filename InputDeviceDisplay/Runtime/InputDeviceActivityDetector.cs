using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace InputDeviceDisplay
{
    internal static class InputDeviceActivityDetector
    {
        private const float ButtonPressThreshold = 0.5f;

        /// <summary>押下、意味のある移動、または閾値を超えた軸入力がstate eventに含まれるかを返す。</summary>
        /// <param name="eventPtr">確認するStateEventまたはDeltaStateEvent。</param>
        /// <param name="device">eventを送信したInput System端末。</param>
        /// <param name="gamepadThreshold">ゲームパッドと汎用端末の最小操作量。</param>
        /// <param name="mouseThreshold">マウス移動とscrollの最小操作量。</param>
        /// <returns>離す、中央へ戻す、閾値未満の揺れ以外の操作を含む場合はtrue。</returns>
        internal static bool HasActivity(
            InputEventPtr eventPtr,
            InputDevice device,
            float gamepadThreshold,
            float mouseThreshold)
        {
            if (device == null || !eventPtr.valid) return false;
            if (eventPtr.type != StateEvent.Type && eventPtr.type != DeltaStateEvent.Type) return false;
            if (!IsFinitePositive(gamepadThreshold) || gamepadThreshold > 1f || !IsFinitePositive(mouseThreshold))
            {
                return false;
            }

            if (device is Keyboard) return HasActuatedButton(eventPtr, device, false);
            if (device is Mouse mouse) return HasMouseActivity(eventPtr, mouse, mouseThreshold);
            if (device is Touchscreen touchscreen) return HasTouchActivity(eventPtr, touchscreen, mouseThreshold);
            return HasActuatedControl(eventPtr, device, gamepadThreshold);
        }

        private static bool HasMouseActivity(InputEventPtr eventPtr, Mouse mouse, float threshold)
        {
            if (HasActuatedButton(eventPtr, mouse, false)) return true;

            var thresholdSquared = threshold * threshold;
            if (mouse.delta.ReadValueFromEvent(eventPtr, out var delta) && delta.sqrMagnitude >= thresholdSquared)
            {
                return true;
            }

            if (mouse.scroll.ReadValueFromEvent(eventPtr, out var scroll) && scroll.sqrMagnitude >= thresholdSquared)
            {
                return true;
            }

            return false;
        }

        private static bool HasTouchActivity(InputEventPtr eventPtr, Touchscreen touchscreen, float movementThreshold)
        {
            if (eventPtr.type == StateEvent.Type)
            {
                try
                {
                    var touchState = StateEvent.GetState<TouchState>(eventPtr);
                    return HasSingleTouchStateActivity(touchState, touchscreen, movementThreshold);
                }
                catch (System.InvalidOperationException)
                {
                    // TouchState以外の通常のTouchscreen stateはcontrol列挙で確認する。
                }
                catch (System.Exception)
                {
                    // 壊れたeventを入力処理へ伝播させず、操作なしとして扱う。
                    return false;
                }
            }

            return HasTouchControlActivity(eventPtr, touchscreen, movementThreshold);
        }

        private static bool HasSingleTouchStateActivity(
            TouchState touchState,
            Touchscreen touchscreen,
            float movementThreshold)
        {
            if (touchState.phase == UnityEngine.InputSystem.TouchPhase.Began) return true;
            if (touchState.phase != UnityEngine.InputSystem.TouchPhase.Moved) return false;

            var thresholdSquared = movementThreshold * movementThreshold;
            if (touchState.delta.sqrMagnitude >= thresholdSquared) return true;

            var touches = touchscreen.touches;
            for (var i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.touchId.ReadValue() != touchState.touchId) continue;
                return (touchState.position - touch.position.ReadValue()).sqrMagnitude >= thresholdSquared;
            }

            return false;
        }

        private static bool HasTouchControlActivity(
            InputEventPtr eventPtr,
            Touchscreen touchscreen,
            float movementThreshold)
        {
            var controls = eventPtr.EnumerateChangedControls(touchscreen);
            foreach (var control in controls)
            {
                var touch = FindTouchControl(control);
                if (touch == null) continue;

                if (control is TouchPressControl && IsTouchPressed(eventPtr, touch)) return true;
                if (!IsTouchPressed(eventPtr, touch)) continue;

                var thresholdSquared = movementThreshold * movementThreshold;
                if (IsSelfOrDescendant(control, touch.delta) &&
                    touch.delta.ReadValueFromEvent(eventPtr, out var delta) &&
                    delta.sqrMagnitude >= thresholdSquared)
                {
                    return true;
                }

                if (IsSelfOrDescendant(control, touch.position) &&
                    touch.position.ReadValueFromEvent(eventPtr, out var position) &&
                    (position - touch.position.ReadValue()).sqrMagnitude >= thresholdSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTouchPressed(InputEventPtr eventPtr, TouchControl touch)
        {
            if (touch.press.ReadValueFromEvent(eventPtr, out var eventValue))
            {
                return eventValue >= ButtonPressThreshold;
            }

            return touch.press.ReadValue() >= ButtonPressThreshold;
        }

        private static TouchControl FindTouchControl(InputControl control)
        {
            for (var current = control; current != null; current = current.parent)
            {
                if (current is TouchControl touch) return touch;
            }

            return null;
        }

        private static bool IsSelfOrDescendant(InputControl control, InputControl expectedAncestor)
        {
            for (var current = control; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, expectedAncestor)) return true;
            }

            return false;
        }

        private static bool HasActuatedButton(InputEventPtr eventPtr, InputDevice device, bool touchPressOnly)
        {
            var controls = eventPtr.EnumerateChangedControls(device);
            foreach (var control in controls)
            {
                if (!(control is ButtonControl button)) continue;
                if (touchPressOnly && !(button is TouchPressControl)) continue;
                if (button.ReadValueFromEvent(eventPtr, out var value) && value >= ButtonPressThreshold) return true;
            }

            return false;
        }

        private static bool HasActuatedControl(InputEventPtr eventPtr, InputDevice device, float threshold)
        {
            var controls = eventPtr.EnumerateChangedControls(device, threshold);
            foreach (var unused in controls) return true;
            return false;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
