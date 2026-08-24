using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace InputDeviceDisplay.PlayMode.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class InputDeviceDisplayControllerTests
    {
        private readonly List<InputDevice> _devices = new List<InputDevice>();
        private GameObject _host;
        private InputDeviceDisplayController _controller;
        private InputSettings.BackgroundBehavior _baselineBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _baselineEditorInputBehaviorInPlayMode;
        private bool _hasBaselineBackgroundBehavior;
        private bool _hasBaselineEditorInputBehaviorInPlayMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _baselineBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            _baselineEditorInputBehaviorInPlayMode = InputSystem.settings.editorInputBehaviorInPlayMode;
            _hasBaselineBackgroundBehavior = true;
            _hasBaselineEditorInputBehaviorInPlayMode = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _host = new GameObject("Input Device Display PlayMode Test");
            _host.SetActive(false);
            _controller = _host.AddComponent<InputDeviceDisplayController>();
            _host.SetActive(true);
            yield return null;

            AssertFallbackState(_controller.State);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (_controller != null)
                {
                    _controller.enabled = false;
                }

                for (var index = _devices.Count - 1; index >= 0; index--)
                {
                    var device = _devices[index];
                    if (device != null && device.added)
                    {
                        InputSystem.RemoveDevice(device);
                    }
                }

                _devices.Clear();
                if (_host != null)
                {
                    UnityEngine.Object.Destroy(_host);
                }
            }
            finally
            {
                if (_hasBaselineBackgroundBehavior)
                {
                    InputSystem.settings.backgroundBehavior = _baselineBackgroundBehavior;
                    _hasBaselineBackgroundBehavior = false;
                }

                if (_hasBaselineEditorInputBehaviorInPlayMode)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = _baselineEditorInputBehaviorInPlayMode;
                    _hasBaselineEditorInputBehaviorInPlayMode = false;
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Lifecycle_DisableAndReenableTransitionsOncePerState()
        {
            var notifications = 0;
            _controller.StateChanged += _ => notifications++;

            _controller.enabled = false;

            Assert.That(_controller.State.IsReady, Is.False);
            Assert.That(_controller.State.HasDeviceActivity, Is.False);
            Assert.That(_controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.Unknown));
            Assert.That(_controller.State.Error, Is.EqualTo(InputDeviceDisplayError.ControllerUnavailable));
            Assert.That(notifications, Is.EqualTo(1));

            _controller.enabled = false;
            Assert.That(notifications, Is.EqualTo(1));

            _controller.enabled = true;
            AssertFallbackState(_controller.State);
            Assert.That(notifications, Is.EqualTo(2));

            _controller.enabled = true;
            Assert.That(notifications, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeviceAdditionAndConfigurationChange_DoNotSelectDevice()
        {
            var initial = _controller.State;
            var notifications = 0;
            _controller.StateChanged += _ => notifications++;
            var gamepad = AddDevice<Gamepad>();

            InputSystem.QueueConfigChangeEvent(gamepad);
            InputSystem.Update();

            Assert.That(_controller.State, Is.EqualTo(initial));
            Assert.That(notifications, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GamepadDriftAndReturnToCenter_DoNotChangeDisplayState()
        {
            var gamepad = AddDevice<Gamepad>();
            var notifications = 0;
            _controller.StateChanged += _ => notifications++;

            QueueGamepadState(gamepad, new GamepadState { leftStick = new Vector2(0.1f, 0f) });
            AssertFallbackState(_controller.State);
            Assert.That(notifications, Is.Zero);

            QueueGamepadState(gamepad, new GamepadState { leftStick = new Vector2(0.8f, 0f) });
            Assert.That(_controller.State.HasDeviceActivity, Is.True);
            Assert.That(_controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));
            Assert.That(_controller.State.DeviceId, Is.EqualTo(gamepad.deviceId));
            Assert.That(notifications, Is.EqualTo(1));

            QueueGamepadState(gamepad, new GamepadState());
            Assert.That(_controller.State.DeviceId, Is.EqualTo(gamepad.deviceId));
            Assert.That(notifications, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedActivityFromSameDevice_DoesNotDuplicateNotification()
        {
            var gamepad = AddDevice<Gamepad>();
            var notifications = 0;
            _controller.StateChanged += _ => notifications++;

            QueueButtonPress(gamepad);
            QueueGamepadState(gamepad, new GamepadState());
            QueueButtonPress(gamepad);

            Assert.That(_controller.State.DeviceId, Is.EqualTo(gamepad.deviceId));
            Assert.That(_controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));
            Assert.That(notifications, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameFamilyActivityFromDifferentDevice_UpdatesDeviceIdentity()
        {
            var first = AddDevice<Gamepad>();
            var second = AddDevice<Gamepad>();
            var states = new List<InputDeviceDisplayState>();
            _controller.StateChanged += states.Add;

            QueueButtonPress(first);
            QueueButtonPress(second);

            Assert.That(states, Has.Count.EqualTo(2));
            Assert.That(states[0].Style, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));
            Assert.That(states[1].Style, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));
            Assert.That(states[0].DeviceId, Is.EqualTo(first.deviceId));
            Assert.That(states[1].DeviceId, Is.EqualTo(second.deviceId));
            Assert.That(_controller.State.DeviceId, Is.EqualTo(second.deviceId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingCurrentDevice_ReturnsToFallback()
        {
            var gamepad = AddDevice<Gamepad>();
            QueueButtonPress(gamepad);
            Assert.That(_controller.State.DeviceId, Is.EqualTo(gamepad.deviceId));

            InputSystem.RemoveDevice(gamepad);

            AssertFallbackState(_controller.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingCurrentDevice_ReturnsToFallback()
        {
            var gamepad = AddDevice<Gamepad>();
            QueueButtonPress(gamepad);
            Assert.That(_controller.State.DeviceId, Is.EqualTo(gamepad.deviceId));

            InputSystem.DisableDevice(gamepad);

            AssertFallbackState(_controller.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnclassifiedDeviceActivity_ReturnsToConfiguredFallback()
        {
            var gamepad = AddDevice<Gamepad>();
            QueueButtonPress(gamepad);
            Assert.That(_controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));

            var joystick = AddDevice<Joystick>();
            InputSystem.QueueDeltaStateEvent(joystick.stick, Vector2.one);
            InputSystem.Update();

            AssertFallbackState(_controller.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SubscriberException_DoesNotStopLaterSubscribersOrController()
        {
            var gamepad = AddDevice<Gamepad>();
            var keyboard = AddDevice<Keyboard>();
            var failingCalls = 0;
            var healthyCalls = 0;
            _controller.StateChanged += _ =>
            {
                failingCalls++;
                throw new InvalidOperationException("expected observer failure");
            };
            _controller.StateChanged += _ => healthyCalls++;
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: expected observer failure");

            QueueButtonPress(gamepad);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();

            Assert.That(failingCalls, Is.EqualTo(2));
            Assert.That(healthyCalls, Is.EqualTo(2));
            Assert.That(_controller.State.IsReady, Is.True);
            Assert.That(_controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.KeyboardMouse));
            Assert.That(_controller.State.DeviceId, Is.EqualTo(keyboard.deviceId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ObservingActivity_DoesNotMarkInputEventHandled()
        {
            var gamepad = AddDevice<Gamepad>();
            bool? handledAfterController = null;
            Action<InputEventPtr, InputDevice> listener = (eventPtr, device) =>
            {
                if (ReferenceEquals(device, gamepad))
                {
                    handledAfterController = eventPtr.handled;
                }
            };
            InputSystem.onEvent += listener;
            try
            {
                QueueButtonPress(gamepad);
            }
            finally
            {
                InputSystem.onEvent -= listener;
            }

            Assert.That(handledAfterController, Is.False);
            Assert.That(_controller.State.DeviceId, Is.EqualTo(gamepad.deviceId));
            yield return null;
        }

        private TDevice AddDevice<TDevice>() where TDevice : InputDevice
        {
            var device = InputSystem.AddDevice<TDevice>();
            _devices.Add(device);
            Assert.That(device.enabled, Is.True, "Virtual test device must remain enabled without application focus.");
            return device;
        }

        private static void QueueButtonPress(Gamepad gamepad)
        {
            QueueGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.South));
        }

        private static void QueueGamepadState(Gamepad gamepad, GamepadState state)
        {
            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
        }

        private static void AssertFallbackState(InputDeviceDisplayState state)
        {
            Assert.That(state.IsReady, Is.True);
            Assert.That(state.HasDeviceActivity, Is.False);
            Assert.That(state.Style, Is.EqualTo(InputDeviceDisplayStyle.KeyboardMouse));
            Assert.That(state.DeviceId, Is.EqualTo(InputDevice.InvalidDeviceId));
            Assert.That(state.LayoutName, Is.Empty);
            Assert.That(state.Error, Is.EqualTo(InputDeviceDisplayError.None));
        }
    }
}
