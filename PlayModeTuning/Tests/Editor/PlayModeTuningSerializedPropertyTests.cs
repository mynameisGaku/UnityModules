using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningSerializedPropertyTests
    {
        private PlayModeTuningCodecFixture source;
        private PlayModeTuningCodecFixture destination;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<PlayModeTuningCodecFixture>();
            destination = ScriptableObject.CreateInstance<PlayModeTuningCodecFixture>();
            source.nestedValue = new PlayModeTuningNestedFixture { value = 1 };
            source.objectReference = source;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(destination);
        }

        [Test]
        public void UnityStringReportsArrayButCodecAcceptsIt()
        {
            var serialized = new SerializedObject(source);
            var property = serialized.FindProperty("stringValue");
            var succeeded = PlayModeTuningValueCodec.TryEncode(property, out _, out var error, out _);
            Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.String));
            Assert.That(property.isArray, Is.True);
            Assert.That(succeeded, Is.True);
            Assert.That(error, Is.EqualTo(PlayModeTuningError.None));
        }

        [Test]
        public void SignedUnsignedStringAndVector3RoundTripExactly()
        {
            source.intValue = -987654321;
            source.uintValue = 4000000001u;
            source.longValue = -8123456789012345678L;
            source.ulongValue = 17123456789012345678UL;
            source.floatValue = BitConverter.ToSingle(BitConverter.GetBytes(0xC1234567u), 0);
            source.doubleValue = BitConverter.ToDouble(BitConverter.GetBytes(0x400921FB54442D18ul), 0);
            source.stringValue = "captured-value";
            source.vector3Value = new Vector3(-7.25f, 8.5f, 9.75f);

            var sourceSerialized = new SerializedObject(source);
            var destinationSerialized = new SerializedObject(destination);
            sourceSerialized.UpdateIfRequiredOrScript();
            destinationSerialized.UpdateIfRequiredOrScript();
            foreach (var path in new[] { "intValue", "uintValue", "longValue", "ulongValue", "floatValue", "doubleValue", "stringValue", "vector3Value" })
            {
                Assert.That(PlayModeTuningValueCodec.TryEncode(sourceSerialized.FindProperty(path), out var value, out var error, out var message), Is.True, error + ": " + message);
                Assert.That(PlayModeTuningValueCodec.TryWrite(destinationSerialized.FindProperty(path), value, out message), Is.True, message);
            }
            destinationSerialized.ApplyModifiedPropertiesWithoutUndo();
            destinationSerialized.UpdateIfRequiredOrScript();

            Assert.That(destinationSerialized.FindProperty("intValue").intValue, Is.EqualTo(source.intValue));
            Assert.That(destinationSerialized.FindProperty("uintValue").uintValue, Is.EqualTo(source.uintValue));
            Assert.That(destinationSerialized.FindProperty("longValue").longValue, Is.EqualTo(source.longValue));
            Assert.That(destinationSerialized.FindProperty("ulongValue").ulongValue, Is.EqualTo(source.ulongValue));
            Assert.That(BitConverter.ToUInt32(BitConverter.GetBytes(destinationSerialized.FindProperty("floatValue").floatValue), 0), Is.EqualTo(0xC1234567u));
            Assert.That(BitConverter.ToUInt64(BitConverter.GetBytes(destinationSerialized.FindProperty("doubleValue").doubleValue), 0), Is.EqualTo(0x400921FB54442D18ul));
            Assert.That(destinationSerialized.FindProperty("stringValue").stringValue, Is.EqualTo("captured-value"));
            Assert.That(destinationSerialized.FindProperty("vector3Value").vector3Value, Is.EqualTo(source.vector3Value));
        }

        [Test]
        public void ContentHashChangesWhenSelectedValueChanges()
        {
            var serialized = new SerializedObject(source);
            serialized.UpdateIfRequiredOrScript();
            var property = serialized.FindProperty("intValue");
            var before = property.contentHash.ToString();
            property.intValue += 7;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.UpdateIfRequiredOrScript();
            var after = serialized.FindProperty("intValue").contentHash.ToString();
            Assert.That(after, Is.Not.EqualTo(before));
        }

        [TestCase("arrayValue")]
        [TestCase("listValue")]
        [TestCase("nestedValue")]
        [TestCase("objectReference")]
        [TestCase("curveValue")]
        [TestCase("gradientValue")]
        public void UnsupportedP0PropertiesAreRejectedByRealSerializedObject(string path)
        {
            var serialized = new SerializedObject(source);
            var succeeded = PlayModeTuningValueCodec.TryEncode(serialized.FindProperty(path), out _, out var error, out _);
            Assert.That(succeeded, Is.False);
            Assert.That(error, Is.EqualTo(PlayModeTuningError.UnsupportedProperty));
        }

        [Test]
        public void NonFiniteFloatAndVectorAreRejected()
        {
            source.floatValue = float.NaN;
            source.vector3Value = new Vector3(1f, float.PositiveInfinity, 3f);
            var serialized = new SerializedObject(source);
            Assert.That(PlayModeTuningValueCodec.TryEncode(serialized.FindProperty("floatValue"), out _, out var floatError, out _), Is.False);
            Assert.That(floatError, Is.EqualTo(PlayModeTuningError.NonFiniteValue));
            Assert.That(PlayModeTuningValueCodec.TryEncode(serialized.FindProperty("vector3Value"), out _, out var vectorError, out _), Is.False);
            Assert.That(vectorError, Is.EqualTo(PlayModeTuningError.NonFiniteValue));
        }

        [Test]
        public void Utf8StringOver4096BytesIsRejected()
        {
            source.stringValue = new string('\u3042', 1366);
            var serialized = new SerializedObject(source);
            Assert.That(PlayModeTuningValueCodec.TryEncode(serialized.FindProperty("stringValue"), out _, out var error, out _), Is.False);
            Assert.That(error, Is.EqualTo(PlayModeTuningError.StringTooLong));
        }
    }

    internal sealed class PlayModeTuningCodecFixture : ScriptableObject
    {
        public int intValue;
        public uint uintValue;
        public long longValue;
        public ulong ulongValue;
        public float floatValue;
        public double doubleValue;
        public string stringValue = string.Empty;
        public Vector3 vector3Value;
        public int[] arrayValue = Array.Empty<int>();
        public List<int> listValue = new List<int>();
        public PlayModeTuningNestedFixture nestedValue;
        public UnityEngine.Object objectReference;
        public AnimationCurve curveValue = new AnimationCurve();
        public Gradient gradientValue = new Gradient();
    }

    [Serializable]
    internal struct PlayModeTuningNestedFixture
    {
        public int value;
    }
}
