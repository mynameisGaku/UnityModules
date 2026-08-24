using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputDeviceDisplay.Editor.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class InputDeviceDisplayConfigurationTests
    {
        private readonly List<GameObject> _hosts = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _hosts.Count - 1; index >= 0; index--)
            {
                if (_hosts[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_hosts[index]);
                }
            }

            _hosts.Clear();
        }

        [Test]
        public void Configuration_ValidValuesStartAtConfiguredFallback()
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.Touch,
                0.25f,
                1f,
                new[]
                {
                    new InputDeviceDisplayLayoutOverride(
                        "ProjectGamepad",
                        InputDeviceDisplayStyle.XboxStyleGamepad)
                });

            Assert.That(controller.State.IsReady, Is.True);
            Assert.That(controller.State.HasDeviceActivity, Is.False);
            Assert.That(controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.Touch));
            Assert.That(controller.State.DeviceId, Is.EqualTo(InputDevice.InvalidDeviceId));
            Assert.That(controller.State.LayoutName, Is.Empty);
            Assert.That(controller.State.Error, Is.EqualTo(InputDeviceDisplayError.None));
        }

        [TestCase(InputDeviceDisplayStyle.Unknown)]
        [TestCase((InputDeviceDisplayStyle)99)]
        public void Configuration_RejectsUnknownOrUndefinedFallback(InputDeviceDisplayStyle fallback)
        {
            var controller = CreateController(fallback, 0.2f, 0.5f, Array.Empty<InputDeviceDisplayLayoutOverride>());

            AssertInvalidConfiguration(controller);
            Assert.That(controller.State.Style, Is.EqualTo(InputDeviceDisplayStyle.Unknown));
        }

        [TestCase(0f)]
        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        public void Configuration_RejectsOutOfRangeGamepadThreshold(float threshold)
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                threshold,
                0.5f,
                Array.Empty<InputDeviceDisplayLayoutOverride>());

            AssertInvalidConfiguration(controller);
        }

        [TestCase(0f)]
        [TestCase(-0.01f)]
        public void Configuration_RejectsNonPositiveMouseThreshold(float threshold)
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                threshold,
                Array.Empty<InputDeviceDisplayLayoutOverride>());

            AssertInvalidConfiguration(controller);
        }

        [Test]
        public void Configuration_RejectsNonFiniteThresholds()
        {
            AssertInvalidConfiguration(CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                float.NaN,
                0.5f,
                Array.Empty<InputDeviceDisplayLayoutOverride>()));
            AssertInvalidConfiguration(CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                float.PositiveInfinity,
                0.5f,
                Array.Empty<InputDeviceDisplayLayoutOverride>()));
            AssertInvalidConfiguration(CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                float.NegativeInfinity,
                Array.Empty<InputDeviceDisplayLayoutOverride>()));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" Gamepad")]
        [TestCase("Gamepad ")]
        public void Configuration_RejectsBlankOrUntrimmedOverrideLayout(string layout)
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                0.5f,
                new[]
                {
                    new InputDeviceDisplayLayoutOverride(
                        layout,
                        InputDeviceDisplayStyle.GenericGamepad)
                });

            AssertInvalidConfiguration(controller);
        }

        [TestCase(InputDeviceDisplayStyle.Unknown)]
        [TestCase((InputDeviceDisplayStyle)99)]
        public void Configuration_RejectsUnknownOrUndefinedOverrideStyle(InputDeviceDisplayStyle style)
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                0.5f,
                new[] { new InputDeviceDisplayLayoutOverride("Gamepad", style) });

            AssertInvalidConfiguration(controller);
        }

        [Test]
        public void Configuration_RejectsNullOverrideEntry()
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                0.5f,
                new InputDeviceDisplayLayoutOverride[] { null });

            AssertInvalidConfiguration(controller);
        }

        [Test]
        public void Configuration_RejectsDuplicateExactLayout()
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                0.5f,
                new[]
                {
                    new InputDeviceDisplayLayoutOverride(
                        "Gamepad",
                        InputDeviceDisplayStyle.XboxStyleGamepad),
                    new InputDeviceDisplayLayoutOverride(
                        "Gamepad",
                        InputDeviceDisplayStyle.PlayStationStyleGamepad)
                });

            AssertInvalidConfiguration(controller);
        }

        [Test]
        public void Configuration_LayoutDuplicateCheckIsCaseSensitive()
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                0.5f,
                new[]
                {
                    new InputDeviceDisplayLayoutOverride(
                        "Gamepad",
                        InputDeviceDisplayStyle.XboxStyleGamepad),
                    new InputDeviceDisplayLayoutOverride(
                        "gamepad",
                        InputDeviceDisplayStyle.PlayStationStyleGamepad)
                });

            Assert.That(controller.State.IsReady, Is.True);
            Assert.That(controller.State.Error, Is.EqualTo(InputDeviceDisplayError.None));
        }

        [Test]
        public void Configuration_NullOverrideArrayIsValid()
        {
            var controller = CreateController(
                InputDeviceDisplayStyle.KeyboardMouse,
                0.2f,
                0.5f,
                null);

            Assert.That(controller.State.IsReady, Is.True);
            Assert.That(controller.State.Error, Is.EqualTo(InputDeviceDisplayError.None));
        }

        private InputDeviceDisplayController CreateController(
            InputDeviceDisplayStyle fallback,
            float gamepadThreshold,
            float mouseThreshold,
            InputDeviceDisplayLayoutOverride[] overrides)
        {
            var host = new GameObject("Input Device Display Configuration Test");
            host.SetActive(false);
            _hosts.Add(host);
            var controller = host.AddComponent<InputDeviceDisplayController>();
            controller.ConfigureForTests(fallback, gamepadThreshold, mouseThreshold, overrides);
            controller.BeginListeningForTests();
            host.SetActive(true);
            return controller;
        }

        private static void AssertInvalidConfiguration(InputDeviceDisplayController controller)
        {
            Assert.That(controller.State.IsReady, Is.False);
            Assert.That(controller.State.HasDeviceActivity, Is.False);
            Assert.That(controller.State.DeviceId, Is.EqualTo(InputDevice.InvalidDeviceId));
            Assert.That(controller.State.LayoutName, Is.Empty);
            Assert.That(controller.State.Error, Is.EqualTo(InputDeviceDisplayError.InvalidConfiguration));
        }
    }
}
