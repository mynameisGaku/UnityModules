using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace InputDeviceDisplay.Editor.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class InputDeviceDisplayClassifierTests
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
        public void Classify_NullDeviceIsUnknown()
        {
            Assert.That(
                InputDeviceDisplayClassifier.Classify(null, null),
                Is.EqualTo(InputDeviceDisplayStyle.Unknown));
        }

        [TestCase("Keyboard", InputDeviceDisplayStyle.KeyboardMouse)]
        [TestCase("Mouse", InputDeviceDisplayStyle.KeyboardMouse)]
        [TestCase("XInputController", InputDeviceDisplayStyle.XboxStyleGamepad)]
        [TestCase("DualShockGamepad", InputDeviceDisplayStyle.PlayStationStyleGamepad)]
        [TestCase("SwitchProControllerHID", InputDeviceDisplayStyle.SwitchStyleGamepad)]
        [TestCase("Gamepad", InputDeviceDisplayStyle.GenericGamepad)]
        [TestCase("Touchscreen", InputDeviceDisplayStyle.Touch)]
        public void Classify_UsesDeviceTypeHierarchy(string layout, InputDeviceDisplayStyle expected)
        {
            var device = AddDevice(layout);

            var actual = InputDeviceDisplayClassifier.Classify(device, null);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Classify_ExactLayoutOverridePrecedesKnownDeviceType()
        {
            var device = AddDevice("XInputController");
            var overrides = new[]
            {
                new InputDeviceDisplayLayoutOverride(
                    device.layout,
                    InputDeviceDisplayStyle.SwitchStyleGamepad)
            };

            var actual = InputDeviceDisplayClassifier.Classify(device, overrides);

            Assert.That(actual, Is.EqualTo(InputDeviceDisplayStyle.SwitchStyleGamepad));
        }

        [Test]
        public void Classify_LayoutOverrideUsesOrdinalExactMatchAndSkipsNullEntries()
        {
            var device = AddDevice("Gamepad");
            var overrides = new[]
            {
                null,
                new InputDeviceDisplayLayoutOverride(
                    device.layout.ToLowerInvariant(),
                    InputDeviceDisplayStyle.PlayStationStyleGamepad)
            };

            var actual = InputDeviceDisplayClassifier.Classify(device, overrides);

            Assert.That(actual, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));
        }

        [Test]
        public void Classify_DoesNotGuessFamilyFromManufacturerOrProduct()
        {
            var device = AddDevice(new InputDeviceDescription
            {
                interfaceName = "Test",
                deviceClass = "Gamepad",
                manufacturer = "Sony Microsoft Nintendo",
                product = "DualShock Xbox Switch Pro Controller"
            });

            var actual = InputDeviceDisplayClassifier.Classify(device, null);

            Assert.That(device, Is.InstanceOf<Gamepad>());
            Assert.That(actual, Is.EqualTo(InputDeviceDisplayStyle.GenericGamepad));
        }

        private InputDevice AddDevice(string layout)
        {
            return Track(InputSystem.AddDevice(layout));
        }

        private InputDevice AddDevice(InputDeviceDescription description)
        {
            return Track(InputSystem.AddDevice(description));
        }

        private InputDevice Track(InputDevice device)
        {
            _devices.Add(device);
            return device;
        }
    }
}
