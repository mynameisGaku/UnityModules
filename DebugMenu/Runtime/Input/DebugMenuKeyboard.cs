using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// キーボードの読み取り。Input System が入っていればそちらを、無ければ旧 Input を使う。
    /// <para>
    /// <b>Input System への参照を張らずに</b>使えるようリフレクションで解決している。
    /// アセンブリ定義に参照を書くと、パッケージが入っていないプロジェクトで
    /// コンパイルが通らなくなる。任意の Input System を必須依存にしないための分離。
    /// </para>
    /// <para>
    /// 解決は起動時の 1 回だけで、以降はキャッシュしたデリゲート越しに呼ぶ。
    /// 毎フレームのリフレクションにはならない。
    /// </para>
    /// </summary>
    internal static class DebugMenuKeyboard
    {
        private static bool _resolved;
        private static bool _useInputSystem;

        private static PropertyInfo _keyboardCurrent;
        private static PropertyInfo _keyIndexer;
        private static PropertyInfo _isPressed;
        private static PropertyInfo _wasPressedThisFrame;
        private static Type _keyEnumType;

        private static readonly Dictionary<KeyCode, object> KeyEnumCache = new Dictionary<KeyCode, object>();

        /// <summary>Input System 経由で読んでいるか。診断用。</summary>
        public static bool UsingInputSystem
        {
            get
            {
                Resolve();
                return _useInputSystem;
            }
        }

        /// <summary>押されているか。</summary>
        /// <param name="key">読むキー。</param>
        public static bool IsHeld(KeyCode key)
        {
            Resolve();

            if (!_useInputSystem) return SafeLegacy(() => Input.GetKey(key));

            var control = GetControl(key);
            return control != null && (bool)_isPressed.GetValue(control);
        }

        /// <summary>このフレームで押されたか。</summary>
        /// <param name="key">読むキー。</param>
        public static bool WasPressed(KeyCode key)
        {
            Resolve();

            if (!_useInputSystem) return SafeLegacy(() => Input.GetKeyDown(key));

            var control = GetControl(key);
            return control != null && (bool)_wasPressedThisFrame.GetValue(control);
        }

        private static object GetControl(KeyCode key)
        {
            var keyboard = _keyboardCurrent.GetValue(null);
            if (keyboard == null) return null;

            if (!KeyEnumCache.TryGetValue(key, out var keyValue))
            {
                keyValue = ToInputSystemKey(key);
                KeyEnumCache[key] = keyValue;
            }

            return keyValue == null ? null : _keyIndexer.GetValue(keyboard, new[] { keyValue });
        }

        /// <summary>
        /// <see cref="KeyCode"/> を Input System の <c>Key</c> へ写す。
        /// 名前が違うものだけ個別に対応させ、残りは同名として扱う。
        /// </summary>
        private static object ToInputSystemKey(KeyCode key)
        {
            var name = key switch
            {
                KeyCode.Return => "Enter",
                KeyCode.KeypadEnter => "NumpadEnter",
                KeyCode.Alpha0 => "Digit0",
                KeyCode.Alpha1 => "Digit1",
                KeyCode.Alpha2 => "Digit2",
                KeyCode.Alpha3 => "Digit3",
                KeyCode.Alpha4 => "Digit4",
                KeyCode.Alpha5 => "Digit5",
                KeyCode.Alpha6 => "Digit6",
                KeyCode.Alpha7 => "Digit7",
                KeyCode.Alpha8 => "Digit8",
                KeyCode.Alpha9 => "Digit9",
                KeyCode.LeftControl => "LeftCtrl",
                KeyCode.RightControl => "RightCtrl",
                _ => key.ToString(),
            };

            try
            {
                return Enum.Parse(_keyEnumType, name);
            }
            catch (ArgumentException)
            {
                // 対応する Key が無いキーは黙って無視する。デバッグメニューの
                // 割り当てが 1 つ効かないだけで、止める理由にはならない。
                return null;
            }
        }

        private static bool SafeLegacy(Func<bool> read)
        {
            try
            {
                return read();
            }
            catch (InvalidOperationException)
            {
                // 旧 Input が無効なプロジェクト。Input System も見つからなかった以上、
                // 読める手段が無いので黙って押されていない扱いにする。
                return false;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            _keyEnumType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");

            if (keyboardType == null || _keyEnumType == null)
            {
                _useInputSystem = false;
                return;
            }

            _keyboardCurrent = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _keyIndexer = keyboardType.GetProperty("Item", new[] { _keyEnumType });

            var buttonControlType = Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem");
            if (buttonControlType != null)
            {
                _isPressed = buttonControlType.GetProperty("isPressed");
                _wasPressedThisFrame = buttonControlType.GetProperty("wasPressedThisFrame");
            }

            _useInputSystem =
                _keyboardCurrent != null &&
                _keyIndexer != null &&
                _isPressed != null &&
                _wasPressedThisFrame != null;

            if (!_useInputSystem)
            {
                Debug.LogWarning("[DebugMenu] Input System は見つかったが、期待した形と違うので旧 Input を使う。");
            }
        }
    }
}
