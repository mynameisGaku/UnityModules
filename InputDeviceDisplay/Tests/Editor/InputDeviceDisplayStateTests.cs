using NUnit.Framework;

namespace InputDeviceDisplay.Editor.Tests
{
    internal sealed class InputDeviceDisplayStateTests
    {
        [Test]
        public void Constructor_ExposesValuesAndNormalizesNullLayout()
        {
            var state = new InputDeviceDisplayState(
                true,
                true,
                InputDeviceDisplayStyle.XboxStyleGamepad,
                42,
                null,
                InputDeviceDisplayError.None);

            Assert.That(state.IsReady, Is.True);
            Assert.That(state.HasDeviceActivity, Is.True);
            Assert.That(state.Style, Is.EqualTo(InputDeviceDisplayStyle.XboxStyleGamepad));
            Assert.That(state.DeviceId, Is.EqualTo(42));
            Assert.That(state.LayoutName, Is.Empty);
            Assert.That(state.Error, Is.EqualTo(InputDeviceDisplayError.None));
        }

        [Test]
        public void Equality_UsesEveryStateField()
        {
            var state = new InputDeviceDisplayState(
                true,
                true,
                InputDeviceDisplayStyle.GenericGamepad,
                7,
                "Gamepad",
                InputDeviceDisplayError.None);
            var equal = new InputDeviceDisplayState(
                true,
                true,
                InputDeviceDisplayStyle.GenericGamepad,
                7,
                "Gamepad",
                InputDeviceDisplayError.None);
            var differences = new[]
            {
                new InputDeviceDisplayState(false, true, InputDeviceDisplayStyle.GenericGamepad, 7, "Gamepad", InputDeviceDisplayError.None),
                new InputDeviceDisplayState(true, false, InputDeviceDisplayStyle.GenericGamepad, 7, "Gamepad", InputDeviceDisplayError.None),
                new InputDeviceDisplayState(true, true, InputDeviceDisplayStyle.XboxStyleGamepad, 7, "Gamepad", InputDeviceDisplayError.None),
                new InputDeviceDisplayState(true, true, InputDeviceDisplayStyle.GenericGamepad, 8, "Gamepad", InputDeviceDisplayError.None),
                new InputDeviceDisplayState(true, true, InputDeviceDisplayStyle.GenericGamepad, 7, "gamepad", InputDeviceDisplayError.None),
                new InputDeviceDisplayState(true, true, InputDeviceDisplayStyle.GenericGamepad, 7, "Gamepad", InputDeviceDisplayError.ControllerUnavailable)
            };

            Assert.That(state.Equals(equal), Is.True);
            Assert.That(state.Equals((object)equal), Is.True);
            Assert.That(state == equal, Is.True);
            Assert.That(state != equal, Is.False);
            Assert.That(state.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            for (var index = 0; index < differences.Length; index++)
            {
                Assert.That(state.Equals(differences[index]), Is.False, $"Difference {index} was ignored.");
                Assert.That(state != differences[index], Is.True, $"Difference {index} was ignored by operator !=.");
            }
        }

        [Test]
        public void Equality_DefaultStatesAreEqual()
        {
            Assert.That(default(InputDeviceDisplayState), Is.EqualTo(default(InputDeviceDisplayState)));
            Assert.That(default(InputDeviceDisplayState) == default, Is.True);
        }
    }
}
