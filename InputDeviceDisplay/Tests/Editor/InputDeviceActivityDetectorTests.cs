using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace InputDeviceDisplay.Editor.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class InputDeviceActivityDetectorTests
    {
        private readonly List<InputDevice> _devices = new List<InputDevice>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _devices.Count - 1; index >= 0; index--)
            {
                var device = _devices[index];
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            _devices.Clear();
        }

        [Test]
        public void HasActivity_InvalidEventOrDeviceReturnsFalse()
        {
            var gamepad = AddDevice<Gamepad>();

            Assert.That(
                InputDeviceActivityDetector.HasActivity(default, gamepad, 0.2f, 0.5f),
                Is.False);
            Assert.That(
                InputDeviceActivityDetector.HasActivity(default, null, 0.2f, 0.5f),
                Is.False);
        }

        [Test]
        public void HasActivity_GamepadButtonPressIsActiveButReleaseIsNot()
        {
            var gamepad = AddDevice<Gamepad>();

            var pressed = ObserveActivity(
                gamepad,
                () => InputSystem.QueueStateEvent(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South)));
            var released = ObserveActivity(
                gamepad,
                () => InputSystem.QueueStateEvent(gamepad, new GamepadState()));

            Assert.That(pressed, Is.True);
            Assert.That(released, Is.False);
        }

        [Test]
        public void HasActivity_GamepadDriftAndReturnToCenterAreNotActive()
        {
            var gamepad = AddDevice<Gamepad>();

            var drift = ObserveActivity(
                gamepad,
                () => InputSystem.QueueStateEvent(
                    gamepad,
                    new GamepadState { leftStick = new Vector2(0.1f, 0f) }));
            var actuated = ObserveActivity(
                gamepad,
                () => InputSystem.QueueStateEvent(
                    gamepad,
                    new GamepadState { leftStick = new Vector2(0.8f, 0f) }));
            var centered = ObserveActivity(
                gamepad,
                () => InputSystem.QueueStateEvent(gamepad, new GamepadState()));

            Assert.That(drift, Is.False);
            Assert.That(actuated, Is.True);
            Assert.That(centered, Is.False);
        }

        [Test]
        public void HasActivity_MouseUsesButtonsDeltaAndScrollButNotAbsolutePosition()
        {
            var mouse = AddDevice<Mouse>();

            var positionOnly = ObserveActivity(
                mouse,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = new Vector2(320f, 180f) }));
            var subthresholdDelta = ObserveActivity(
                mouse,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState
                    {
                        position = new Vector2(320f, 180f),
                        delta = new Vector2(0.49f, 0f)
                    }));
            var thresholdDelta = ObserveActivity(
                mouse,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState
                    {
                        position = new Vector2(320f, 180f),
                        delta = new Vector2(0.5f, 0f)
                    }));
            var thresholdScroll = ObserveActivity(
                mouse,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState
                    {
                        position = new Vector2(320f, 180f),
                        scroll = new Vector2(0f, 0.5f)
                    }));
            var buttonPress = ObserveActivity(
                mouse,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = new Vector2(320f, 180f) }.WithButton(MouseButton.Left)));
            var buttonRelease = ObserveActivity(
                mouse,
                () => InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = new Vector2(320f, 180f) }));

            Assert.That(positionOnly, Is.False);
            Assert.That(subthresholdDelta, Is.False);
            Assert.That(thresholdDelta, Is.True);
            Assert.That(thresholdScroll, Is.True);
            Assert.That(buttonPress, Is.True);
            Assert.That(buttonRelease, Is.False);
        }

        [Test]
        public void HasActivity_TouchPressIsActiveButReleaseIsNot()
        {
            var touchscreen = AddDevice<Touchscreen>();

            var pressed = ObserveActivity(
                touchscreen,
                () => InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Began,
                        position = new Vector2(10f, 20f),
                        pressure = 1f
                    }));
            var subthresholdMove = ObserveActivity(
                touchscreen,
                () => InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Moved,
                        position = new Vector2(10.49f, 20f),
                        delta = new Vector2(0.49f, 0f),
                        pressure = 1f
                    }));
            var thresholdPositionMove = ObserveActivity(
                touchscreen,
                () => InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Moved,
                        position = new Vector2(10.99f, 20f),
                        pressure = 1f
                    }));
            var thresholdDeltaMove = ObserveActivity(
                touchscreen,
                () => InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Moved,
                        position = new Vector2(11.49f, 20f),
                        delta = new Vector2(0.5f, 0f),
                        pressure = 1f
                    }));
            var released = ObserveActivity(
                touchscreen,
                () => InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Ended,
                        position = new Vector2(12f, 20f),
                        delta = new Vector2(0.51f, 0f)
                    }));

            Assert.That(pressed, Is.True);
            Assert.That(subthresholdMove, Is.False);
            Assert.That(thresholdPositionMove, Is.True);
            Assert.That(thresholdDeltaMove, Is.True);
            Assert.That(released, Is.False);
        }

        [Test]
        public void HasActivity_InvalidThresholdsRejectOtherwiseActiveEvent()
        {
            var zeroThresholdGamepad = AddDevice<Gamepad>();
            var tooLargeThresholdGamepad = AddDevice<Gamepad>();
            var zeroMouseThresholdGamepad = AddDevice<Gamepad>();

            Assert.That(
                ObserveActivity(
                    zeroThresholdGamepad,
                    () => InputSystem.QueueStateEvent(
                        zeroThresholdGamepad,
                        new GamepadState().WithButton(GamepadButton.South)),
                    gamepadThreshold: 0f),
                Is.False);
            Assert.That(
                ObserveActivity(
                    tooLargeThresholdGamepad,
                    () => InputSystem.QueueStateEvent(
                        tooLargeThresholdGamepad,
                        new GamepadState().WithButton(GamepadButton.South)),
                    gamepadThreshold: 1.01f),
                Is.False);
            Assert.That(
                ObserveActivity(
                    zeroMouseThresholdGamepad,
                    () => InputSystem.QueueStateEvent(
                        zeroMouseThresholdGamepad,
                        new GamepadState().WithButton(GamepadButton.South)),
                    mouseThreshold: 0f),
                Is.False);
        }

        private TDevice AddDevice<TDevice>() where TDevice : InputDevice
        {
            var device = InputSystem.AddDevice<TDevice>();
            _devices.Add(device);
            return device;
        }

        private static bool ObserveActivity(
            InputDevice expectedDevice,
            Action queueEvent,
            float gamepadThreshold = 0.2f,
            float mouseThreshold = 0.5f)
        {
            var eventSeen = false;
            var activity = false;
            Action<InputEventPtr, InputDevice> listener = (eventPtr, device) =>
            {
                if (!ReferenceEquals(device, expectedDevice))
                {
                    return;
                }

                eventSeen = true;
                activity |= InputDeviceActivityDetector.HasActivity(
                    eventPtr,
                    device,
                    gamepadThreshold,
                    mouseThreshold);
            };
            InputSystem.onEvent += listener;
            try
            {
                queueEvent();
                InputSystem.Update();
            }
            finally
            {
                InputSystem.onEvent -= listener;
            }

            Assert.That(eventSeen, Is.True, "Queued Input System event was not observed.");
            return activity;
        }
    }
}
