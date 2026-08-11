using System;
using System.Reflection;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>デバイスAPIから切り離した1フレーム分のゲームパッド標本。</summary>
    public struct DebugMenuGamepadSample
    {
        /// <summary>方向パッドの値。</summary>
        public Vector2 Dpad;

        /// <summary>左スティックの値。</summary>
        public Vector2 LeftStick;

        /// <summary>Southボタンがこのフレームで押されたか。</summary>
        public bool SouthPressed;

        /// <summary>Eastボタンがこのフレームで押されたか。</summary>
        public bool EastPressed;

        /// <summary>左ショルダーがこのフレームで押されたか。</summary>
        public bool LeftShoulderPressed;

        /// <summary>右ショルダーがこのフレームで押されたか。</summary>
        public bool RightShoulderPressed;

        /// <summary>Startボタンがこのフレームで押されたか。</summary>
        public bool StartPressed;
    }

    /// <summary>ゲームパッド標本をデバイス非依存のメニュー入力へ変換する。</summary>
    public static class DebugMenuGamepadInput
    {
        /// <summary>左スティックを方向入力として扱う既定の境界。</summary>
        public const float DefaultStickDeadZone = 0.5f;

        /// <summary>純粋な標本から1フレーム分のメニュー入力を作る。</summary>
        /// <param name="sample">ゲームパッドから採取済みの値。</param>
        /// <param name="stickDeadZone">左スティックを方向として扱う絶対値の境界。</param>
        public static DebugMenuInputState Map(
            in DebugMenuGamepadSample sample,
            float stickDeadZone = DefaultStickDeadZone)
        {
            var deadZone = Mathf.Clamp01(stickDeadZone);
            return new DebugMenuInputState
            {
                Up = sample.Dpad.y > 0f || sample.LeftStick.y > deadZone,
                Down = sample.Dpad.y < 0f || sample.LeftStick.y < -deadZone,
                Left = sample.Dpad.x < 0f || sample.LeftStick.x < -deadZone,
                Right = sample.Dpad.x > 0f || sample.LeftStick.x > deadZone,
                Decide = sample.SouthPressed,
                Cancel = sample.EastPressed,
                PreviousPage = sample.LeftShoulderPressed,
                NextPage = sample.RightShoulderPressed,
                ToggleMenu = sample.StartPressed,
            };
        }
    }

    /// <summary>
    /// 標準ゲームパッドを読む。Input Systemへのコンパイル参照は持たず、利用可能なら反射で読む。
    /// 見つからない場合は旧Inputの共通軸とJoystickButtonへ安全にフォールバックする。
    /// </summary>
    internal static class DebugMenuGamepad
    {
        private static bool _resolved;
        private static bool _useInputSystem;
        private static bool _inputSystemFailureLogged;

        private static PropertyInfo _gamepadCurrent;
        private static PropertyInfo _dpad;
        private static PropertyInfo _leftStick;
        private static PropertyInfo _buttonSouth;
        private static PropertyInfo _buttonEast;
        private static PropertyInfo _leftShoulder;
        private static PropertyInfo _rightShoulder;
        private static PropertyInfo _startButton;
        private static PropertyInfo _wasPressedThisFrame;
        private static MethodInfo _readVector2;

        /// <summary>標準ゲームパッドの現在状態をメニュー入力へ変換する。</summary>
        public static DebugMenuInputState Read()
        {
            Resolve();
            if (!_useInputSystem) return ReadLegacy();

            try
            {
                var gamepad = _gamepadCurrent.GetValue(null);
                if (gamepad == null) return default;

                var sample = new DebugMenuGamepadSample
                {
                    Dpad = ReadVector2(gamepad, _dpad),
                    LeftStick = ReadVector2(gamepad, _leftStick),
                    SouthPressed = ReadPressed(gamepad, _buttonSouth),
                    EastPressed = ReadPressed(gamepad, _buttonEast),
                    LeftShoulderPressed = ReadPressed(gamepad, _leftShoulder),
                    RightShoulderPressed = ReadPressed(gamepad, _rightShoulder),
                    StartPressed = ReadPressed(gamepad, _startButton),
                };
                return DebugMenuGamepadInput.Map(sample);
            }
            catch (Exception exception)
            {
                _useInputSystem = false;
                if (!_inputSystemFailureLogged)
                {
                    _inputSystemFailureLogged = true;
                    Debug.LogWarning($"[DebugMenu] Input SystemのGamepadを読めなかったため旧Inputへ切り替える。\n{exception.Message}");
                }

                return ReadLegacy();
            }
        }

        private static Vector2 ReadVector2(object gamepad, PropertyInfo property)
        {
            var control = property.GetValue(gamepad);
            return control == null ? Vector2.zero : (Vector2)_readVector2.Invoke(control, null);
        }

        private static bool ReadPressed(object gamepad, PropertyInfo property)
        {
            var control = property.GetValue(gamepad);
            return control != null && (bool)_wasPressedThisFrame.GetValue(control);
        }

        private static DebugMenuInputState ReadLegacy()
        {
            var sample = new DebugMenuGamepadSample
            {
                Dpad = new Vector2(
                    SafeLegacyAxisAny("DPad X", "DPadX", "D-Pad X"),
                    SafeLegacyAxisAny("DPad Y", "DPadY", "D-Pad Y")),
                LeftStick = new Vector2(SafeLegacyAxis("Horizontal"), SafeLegacyAxis("Vertical")),
                SouthPressed = SafeLegacyButton(KeyCode.JoystickButton0),
                EastPressed = SafeLegacyButton(KeyCode.JoystickButton1),
                LeftShoulderPressed = SafeLegacyButton(KeyCode.JoystickButton4),
                RightShoulderPressed = SafeLegacyButton(KeyCode.JoystickButton5),
                StartPressed = SafeLegacyButton(KeyCode.JoystickButton7),
            };
            return DebugMenuGamepadInput.Map(sample);
        }

        /// <summary>旧Inputでプロジェクトごとに異なる十字キー軸名を順に試す。</summary>
        private static float SafeLegacyAxisAny(params string[] axisNames)
        {
            for (var i = 0; i < axisNames.Length; i++)
            {
                var value = SafeLegacyAxis(axisNames[i]);
                if (!Mathf.Approximately(value, 0f)) return value;
            }

            return 0f;
        }

        private static float SafeLegacyAxis(string axisName)
        {
            try
            {
                return Input.GetAxisRaw(axisName);
            }
            catch (Exception)
            {
                // 旧Inputが無効、または軸が未登録なら入力無しとしてメニューを生かす。
                return 0f;
            }
        }

        private static bool SafeLegacyButton(KeyCode key)
        {
            try
            {
                return Input.GetKeyDown(key);
            }
            catch (Exception)
            {
                // 旧Inputが無効でもデバッグメニュー本体は止めない。
                return false;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var gamepadType = Type.GetType("UnityEngine.InputSystem.Gamepad, Unity.InputSystem");
            var vector2ControlType = Type.GetType("UnityEngine.InputSystem.Controls.Vector2Control, Unity.InputSystem");
            var buttonControlType = Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem");
            if (gamepadType == null || vector2ControlType == null || buttonControlType == null) return;

            _gamepadCurrent = gamepadType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _dpad = gamepadType.GetProperty("dpad", BindingFlags.Public | BindingFlags.Instance);
            _leftStick = gamepadType.GetProperty("leftStick", BindingFlags.Public | BindingFlags.Instance);
            _buttonSouth = gamepadType.GetProperty("buttonSouth", BindingFlags.Public | BindingFlags.Instance);
            _buttonEast = gamepadType.GetProperty("buttonEast", BindingFlags.Public | BindingFlags.Instance);
            _leftShoulder = gamepadType.GetProperty("leftShoulder", BindingFlags.Public | BindingFlags.Instance);
            _rightShoulder = gamepadType.GetProperty("rightShoulder", BindingFlags.Public | BindingFlags.Instance);
            _startButton = gamepadType.GetProperty("startButton", BindingFlags.Public | BindingFlags.Instance);
            _wasPressedThisFrame = buttonControlType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
            _readVector2 = vector2ControlType.GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            _useInputSystem =
                _gamepadCurrent != null &&
                _dpad != null &&
                _leftStick != null &&
                _buttonSouth != null &&
                _buttonEast != null &&
                _leftShoulder != null &&
                _rightShoulder != null &&
                _startButton != null &&
                _wasPressedThisFrame != null &&
                _readVector2 != null;
        }
    }
}
