using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace InputDeviceDisplay
{
    /// <summary>
    /// Input Systemの実操作を監視し、画面へ表示する入力端末の表記体系を通知する。
    /// GameObjectが所有し、静的な端末所有状態は作らない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputDeviceDisplayController : MonoBehaviour
    {
        [SerializeField]
        private InputDeviceDisplayStyle _fallbackStyle = InputDeviceDisplayStyle.KeyboardMouse;

        [SerializeField, Range(0.01f, 1f)]
        private float _gamepadActivityThreshold = 0.2f;

        [SerializeField, Min(0.001f)]
        private float _mouseActivityThreshold = 0.5f;

        [SerializeField]
        private InputDeviceDisplayLayoutOverride[] _exactLayoutOverrides =
            Array.Empty<InputDeviceDisplayLayoutOverride>();

        private ObserverSlot[] _stateChangedObservers = Array.Empty<ObserverSlot>();
        private InputDeviceDisplayState _state = CreateUnavailableState();
        private int _stateVersion;
        private bool _isSubscribed;

        /// <summary>現在の入力端末、表記体系、監視状態を表す最新スナップショット。</summary>
        public InputDeviceDisplayState State => _state;

        /// <summary>
        /// 入力端末、表記体系、または監視状態が変わったときに最新スナップショットを通知する。
        /// 通知先の例外は他の通知先とControllerへ伝播しない。
        /// </summary>
        public event Action<InputDeviceDisplayState> StateChanged
        {
            add => _stateChangedObservers = AddObservers(_stateChangedObservers, value);
            remove => _stateChangedObservers = RemoveObservers(_stateChangedObservers, value);
        }

        private void OnEnable()
        {
            BeginListening();
        }

        private void OnDisable()
        {
            EndListening();
        }

        private void OnDestroy()
        {
            EndListening();
        }

        /// <summary>テストfixtureから有効化前の閾値、fallback、layout上書きを設定する。</summary>
        /// <param name="fallbackStyle">操作端末が未選択のときに使用する表記体系。</param>
        /// <param name="gamepadActivityThreshold">ゲームパッドと汎用端末の最小操作量。</param>
        /// <param name="mouseActivityThreshold">マウス移動とscrollの最小操作量。</param>
        /// <param name="exactLayoutOverrides">layout名を完全一致で置き換える設定。</param>
        internal void ConfigureForTests(
            InputDeviceDisplayStyle fallbackStyle,
            float gamepadActivityThreshold,
            float mouseActivityThreshold,
            InputDeviceDisplayLayoutOverride[] exactLayoutOverrides)
        {
            _fallbackStyle = fallbackStyle;
            _gamepadActivityThreshold = gamepadActivityThreshold;
            _mouseActivityThreshold = mouseActivityThreshold;
            _exactLayoutOverrides = CloneOverrides(exactLayoutOverrides);
        }

        /// <summary>通常のMonoBehaviour lifecycleが動かないEditMode testで入力監視を開始する。</summary>
        internal void BeginListeningForTests()
        {
            BeginListening();
        }

        private void BeginListening()
        {
            if (_isSubscribed) return;
            if (!HasValidConfiguration())
            {
                SetState(new InputDeviceDisplayState(
                    false,
                    false,
                    IsDefinedStyle(_fallbackStyle) ? _fallbackStyle : InputDeviceDisplayStyle.Unknown,
                    InputDevice.InvalidDeviceId,
                    string.Empty,
                    InputDeviceDisplayError.InvalidConfiguration));
                return;
            }

            _isSubscribed = true;
            try
            {
                InputSystem.onEvent += OnInputEvent;
                InputSystem.onDeviceChange += OnDeviceChange;
                SetFallbackState();
            }
            catch (Exception exception)
            {
                TryUnsubscribe();
                TryLogException(exception);
                SetState(CreateUnavailableState());
            }
        }

        private void EndListening()
        {
            TryUnsubscribe();
            SetState(CreateUnavailableState());
        }

        private void TryUnsubscribe()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;
            try
            {
                InputSystem.onEvent -= OnInputEvent;
            }
            catch (Exception exception)
            {
                TryLogException(exception);
            }

            try
            {
                InputSystem.onDeviceChange -= OnDeviceChange;
            }
            catch (Exception exception)
            {
                TryLogException(exception);
            }
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!_isSubscribed || !_state.IsReady || device == null || !device.added || !device.enabled) return;
            if (!InputDeviceActivityDetector.HasActivity(
                    eventPtr,
                    device,
                    _gamepadActivityThreshold,
                    _mouseActivityThreshold))
            {
                return;
            }

            var style = InputDeviceDisplayClassifier.Classify(device, _exactLayoutOverrides);
            if (style == InputDeviceDisplayStyle.Unknown)
            {
                SetFallbackState();
                return;
            }

            if (_state.HasDeviceActivity && _state.DeviceId == device.deviceId && _state.Style == style &&
                string.Equals(_state.LayoutName, device.layout, StringComparison.Ordinal))
            {
                return;
            }

            SetState(new InputDeviceDisplayState(
                true,
                true,
                style,
                device.deviceId,
                device.layout,
                InputDeviceDisplayError.None));
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!_isSubscribed || !_state.HasDeviceActivity || device == null || _state.DeviceId != device.deviceId)
            {
                return;
            }

            if (change == InputDeviceChange.Removed ||
                change == InputDeviceChange.Disconnected ||
                change == InputDeviceChange.Disabled)
            {
                SetFallbackState();
            }
        }

        private bool HasValidConfiguration()
        {
            if (_fallbackStyle == InputDeviceDisplayStyle.Unknown || !IsDefinedStyle(_fallbackStyle)) return false;
            if (!IsFinitePositive(_gamepadActivityThreshold) || _gamepadActivityThreshold > 1f) return false;
            if (!IsFinitePositive(_mouseActivityThreshold)) return false;
            if (_exactLayoutOverrides == null) return true;

            for (var i = 0; i < _exactLayoutOverrides.Length; i++)
            {
                var item = _exactLayoutOverrides[i];
                if (item == null || string.IsNullOrWhiteSpace(item.LayoutName)) return false;
                if (!string.Equals(item.LayoutName, item.LayoutName.Trim(), StringComparison.Ordinal)) return false;
                if (item.Style == InputDeviceDisplayStyle.Unknown || !IsDefinedStyle(item.Style)) return false;
                for (var previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    if (string.Equals(
                            _exactLayoutOverrides[previousIndex].LayoutName,
                            item.LayoutName,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void SetFallbackState()
        {
            SetState(new InputDeviceDisplayState(
                true,
                false,
                _fallbackStyle,
                InputDevice.InvalidDeviceId,
                string.Empty,
                InputDeviceDisplayError.None));
        }

        private void SetState(InputDeviceDisplayState state)
        {
            if (_state == state) return;
            _state = state;
            var version = ++_stateVersion;
            var observers = _stateChangedObservers;
            for (var i = 0; i < observers.Length; i++)
            {
                if (version != _stateVersion) break;
                InvokeObserver(observers[i], state);
            }
        }

        private static void InvokeObserver(ObserverSlot observer, InputDeviceDisplayState state)
        {
            try
            {
                observer.Observer(state);
                observer.IsFailing = false;
            }
            catch (Exception exception)
            {
                if (observer.IsFailing) return;
                observer.IsFailing = true;
                TryLogException(exception);
            }
        }

        private static void TryLogException(Exception exception)
        {
            try
            {
                Debug.LogException(exception);
            }
            catch (Exception)
            {
                // 終了中にログ機構を利用できなくても、購読解除と残りの通知を続ける。
            }
        }

        private static ObserverSlot[] AddObservers(
            ObserverSlot[] observers,
            Action<InputDeviceDisplayState> observer)
        {
            if (observer == null) return observers;
            var additions = observer.GetInvocationList();
            var result = new ObserverSlot[observers.Length + additions.Length];
            Array.Copy(observers, result, observers.Length);
            for (var i = 0; i < additions.Length; i++)
            {
                result[observers.Length + i] = new ObserverSlot((Action<InputDeviceDisplayState>)additions[i]);
            }

            return result;
        }

        private static ObserverSlot[] RemoveObservers(
            ObserverSlot[] observers,
            Action<InputDeviceDisplayState> observer)
        {
            if (observer == null || observers.Length == 0) return observers;
            var removals = observer.GetInvocationList();
            for (var start = observers.Length - removals.Length; start >= 0; start--)
            {
                var matches = true;
                for (var i = 0; i < removals.Length; i++)
                {
                    if (!Equals(observers[start + i].Observer, removals[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches) continue;
                var result = new ObserverSlot[observers.Length - removals.Length];
                Array.Copy(observers, 0, result, 0, start);
                Array.Copy(observers, start + removals.Length, result, start, observers.Length - start - removals.Length);
                return result;
            }

            return observers;
        }

        private static InputDeviceDisplayLayoutOverride[] CloneOverrides(
            InputDeviceDisplayLayoutOverride[] source)
        {
            if (source == null) return null;
            var result = new InputDeviceDisplayLayoutOverride[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }

        private static bool IsDefinedStyle(InputDeviceDisplayStyle style)
        {
            return style >= InputDeviceDisplayStyle.Unknown && style <= InputDeviceDisplayStyle.Touch;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static InputDeviceDisplayState CreateUnavailableState()
        {
            return new InputDeviceDisplayState(
                false,
                false,
                InputDeviceDisplayStyle.Unknown,
                InputDevice.InvalidDeviceId,
                string.Empty,
                InputDeviceDisplayError.ControllerUnavailable);
        }

        private sealed class ObserverSlot
        {
            /// <summary>個別に呼び出す通知先を保持する。</summary>
            /// <param name="observer">保持する単一の通知先。</param>
            internal ObserverSlot(Action<InputDeviceDisplayState> observer)
            {
                Observer = observer;
            }

            /// <summary>呼び出す単一の通知先。</summary>
            internal Action<InputDeviceDisplayState> Observer { get; }

            /// <summary>前回の呼出しが失敗し、同じ連続失敗を記録済みならtrue。</summary>
            internal bool IsFailing { get; set; }
        }
    }
}
