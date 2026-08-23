using System;
using NUnit.Framework;
using UnityEditor;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningValueCodecTests
    {
        [Test]
        public void StringIsAllowedBeforeGenericIsArrayRejection()
        {
            Assert.That(PlayModeTuningValueCodec.IsSupportedShape(SerializedPropertyType.String, 0, true), Is.True);
        }

        [Test]
        public void NonStringArrayIsRejected()
        {
            Assert.That(PlayModeTuningValueCodec.IsSupportedShape(SerializedPropertyType.Boolean, 0, true), Is.False);
        }

        [Test]
        public void NestedPropertyIsRejected()
        {
            Assert.That(PlayModeTuningValueCodec.IsSupportedShape(SerializedPropertyType.Vector3, 1, false), Is.False);
        }

        [TestCase(SerializedPropertyType.ObjectReference)]
        [TestCase(SerializedPropertyType.Generic)]
        [TestCase(SerializedPropertyType.AnimationCurve)]
        [TestCase(SerializedPropertyType.Gradient)]
        public void UnsupportedP0TypesAreRejected(SerializedPropertyType type)
        {
            Assert.That(PlayModeTuningValueCodec.IsSupportedShape(type, 0, false), Is.False);
        }

        [TestCase(SerializedPropertyType.Boolean)]
        [TestCase(SerializedPropertyType.Integer)]
        [TestCase(SerializedPropertyType.Character)]
        [TestCase(SerializedPropertyType.Float)]
        [TestCase(SerializedPropertyType.Enum)]
        [TestCase(SerializedPropertyType.LayerMask)]
        [TestCase(SerializedPropertyType.Color)]
        [TestCase(SerializedPropertyType.Vector2)]
        [TestCase(SerializedPropertyType.Vector3)]
        [TestCase(SerializedPropertyType.Vector4)]
        [TestCase(SerializedPropertyType.Vector2Int)]
        [TestCase(SerializedPropertyType.Vector3Int)]
        [TestCase(SerializedPropertyType.Rect)]
        [TestCase(SerializedPropertyType.RectInt)]
        [TestCase(SerializedPropertyType.Bounds)]
        [TestCase(SerializedPropertyType.BoundsInt)]
        [TestCase(SerializedPropertyType.Quaternion)]
        public void P0AllowListShapeIsAccepted(SerializedPropertyType type)
        {
            Assert.That(PlayModeTuningValueCodec.IsSupportedShape(type, 0, false), Is.True);
        }

        [Test]
        public void FloatRawBitsRoundTripMatchesP0()
        {
            const uint bits = 0xC1234567u;
            var value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            var encoded = PlayModeTuningValueCodec.EncodeFloat(value);
            var decoded = PlayModeTuningValueCodec.DecodeFloat(encoded);
            Assert.That(encoded, Is.EqualTo("C1234567"));
            Assert.That(BitConverter.ToUInt32(BitConverter.GetBytes(decoded), 0), Is.EqualTo(bits));
        }

        [Test]
        public void DoubleRawBitsRoundTripMatchesP0()
        {
            const ulong bits = 0x400921FB54442D18ul;
            var value = BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
            var encoded = PlayModeTuningValueCodec.EncodeDouble(value);
            var decoded = PlayModeTuningValueCodec.DecodeDouble(encoded);
            Assert.That(encoded, Is.EqualTo("400921FB54442D18"));
            Assert.That(BitConverter.ToUInt64(BitConverter.GetBytes(decoded), 0), Is.EqualTo(bits));
        }

        [Test]
        public void PositiveAndNegativeZeroHaveDistinctExactDisplays()
        {
            var positive = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Float, "00000000", string.Empty);
            var negative = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Float, "80000000", string.Empty);
            Assert.That(PlayModeTuningValueCodec.TryCreateCanonicalDisplay(positive, out var positiveDisplay), Is.True);
            Assert.That(PlayModeTuningValueCodec.TryCreateCanonicalDisplay(negative, out var negativeDisplay), Is.True);
            Assert.That(positiveDisplay, Is.EqualTo("0"));
            Assert.That(negativeDisplay, Is.EqualTo("-0"));
        }

        [Test]
        public void Utf8StringLimitIsExactly4096Bytes()
        {
            Assert.That(PlayModeTuningValueCodec.MaximumStringUtf8Bytes, Is.EqualTo(4096));
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(new string('a', 4096)), Is.EqualTo(4096));
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(new string('\u3042', 1366)), Is.GreaterThan(4096));
        }
    }
}
