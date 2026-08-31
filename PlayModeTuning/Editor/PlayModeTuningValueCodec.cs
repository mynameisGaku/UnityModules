using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PlayModeTuning.Editor
{
    /// <summary>対応する最上位SerializedPropertyを、浮動小数点の情報を失わず符号化して書き戻します。</summary>
    internal static class PlayModeTuningValueCodec
    {
        internal const int MaximumStringUtf8Bytes = 4096;

        internal static bool IsSupportedShape(SerializedPropertyType propertyType, int depth, bool isArray)
        {
            if (depth != 0)
                return false;
            if (propertyType == SerializedPropertyType.String)
                return true;
            if (isArray)
                return false;
            switch (propertyType)
            {
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.BoundsInt:
                case SerializedPropertyType.Quaternion:
                    return true;
                default:
                    return false;
            }
        }

        internal static string PropertyTypeName(SerializedProperty property)
        {
            return property.propertyType.ToString();
        }

        internal static string NumericTypeName(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Integer && property.propertyType != SerializedPropertyType.Float)
                return string.Empty;
            return property.numericType.ToString();
        }

        internal static bool TryEncode(SerializedProperty property, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            encoded = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            if (property == null || !IsSupportedShape(property.propertyType, property.depth, property.isArray))
                return Fail(PlayModeTuningError.UnsupportedProperty, "対応する最上位の単一値または値型項目だけを記録できます。", out error, out message);

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Boolean, property.boolValue ? "1" : "0", property.boolValue ? "真" : "偽");
                        return true;
                    case SerializedPropertyType.Integer:
                        return TryEncodeInteger(property, out encoded, out error, out message);
                    case SerializedPropertyType.Character:
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Character, property.intValue.ToString(CultureInfo.InvariantCulture), "U+" + property.intValue.ToString("X4", CultureInfo.InvariantCulture));
                        return true;
                    case SerializedPropertyType.Float:
                        return TryEncodeFloatProperty(property, out encoded, out error, out message);
                    case SerializedPropertyType.String:
                        var text = property.stringValue ?? string.Empty;
                        if (Encoding.UTF8.GetByteCount(text) > MaximumStringUtf8Bytes)
                            return Fail(PlayModeTuningError.StringTooLong, "選んだ文字列がUTF-8で4096バイトの上限を超えています。", out error, out message);
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.String, Convert.ToBase64String(Encoding.UTF8.GetBytes(text)), text);
                        return true;
                    case SerializedPropertyType.Enum:
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Enum, property.intValue.ToString(CultureInfo.InvariantCulture), property.intValue.ToString(CultureInfo.InvariantCulture));
                        return true;
                    case SerializedPropertyType.LayerMask:
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.LayerMask, property.intValue.ToString(CultureInfo.InvariantCulture), property.intValue.ToString(CultureInfo.InvariantCulture));
                        return true;
                    case SerializedPropertyType.Color:
                        return TryEncodeColor(property.colorValue, out encoded, out error, out message);
                    case SerializedPropertyType.Vector2:
                        return TryEncodeVector2(property.vector2Value, out encoded, out error, out message);
                    case SerializedPropertyType.Vector3:
                        return TryEncodeVector3(property.vector3Value, out encoded, out error, out message);
                    case SerializedPropertyType.Vector4:
                        return TryEncodeVector4(property.vector4Value, out encoded, out error, out message);
                    case SerializedPropertyType.Vector2Int:
                        var vector2Int = property.vector2IntValue;
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Vector2Int, Join(vector2Int.x, vector2Int.y), DisplayIntegers(vector2Int.x, vector2Int.y));
                        return true;
                    case SerializedPropertyType.Vector3Int:
                        var vector3Int = property.vector3IntValue;
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Vector3Int, Join(vector3Int.x, vector3Int.y, vector3Int.z), DisplayIntegers(vector3Int.x, vector3Int.y, vector3Int.z));
                        return true;
                    case SerializedPropertyType.Rect:
                        var rect = property.rectValue;
                        return TryEncodeFloats(PlayModeTuningValueKind.Rect, new[] { rect.x, rect.y, rect.width, rect.height }, out encoded, out error, out message);
                    case SerializedPropertyType.RectInt:
                        var rectInt = property.rectIntValue;
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.RectInt, Join(rectInt.x, rectInt.y, rectInt.width, rectInt.height), DisplayIntegers(rectInt.x, rectInt.y, rectInt.width, rectInt.height));
                        return true;
                    case SerializedPropertyType.Bounds:
                        var bounds = property.boundsValue;
                        return TryEncodeFloats(PlayModeTuningValueKind.Bounds, new[] { bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z }, out encoded, out error, out message);
                    case SerializedPropertyType.BoundsInt:
                        var boundsInt = property.boundsIntValue;
                        encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.BoundsInt, Join(boundsInt.position.x, boundsInt.position.y, boundsInt.position.z, boundsInt.size.x, boundsInt.size.y, boundsInt.size.z), DisplayIntegers(boundsInt.position.x, boundsInt.position.y, boundsInt.position.z, boundsInt.size.x, boundsInt.size.y, boundsInt.size.z));
                        return true;
                    case SerializedPropertyType.Quaternion:
                        var quaternion = property.quaternionValue;
                        return TryEncodeFloats(PlayModeTuningValueKind.Quaternion, new[] { quaternion.x, quaternion.y, quaternion.z, quaternion.w }, out encoded, out error, out message);
                    default:
                        return Fail(PlayModeTuningError.UnsupportedProperty, "このシリアル化項目の型には対応していません。", out error, out message);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Fail(PlayModeTuningError.CaptureFailed, "選んだ値を記録形式へ変換できませんでした。詳しくはコンソールを確認してください。", out error, out message);
            }
        }

        internal static bool TryWrite(SerializedProperty property, PlayModeTuningEncodedValue encoded, out string message)
        {
            message = string.Empty;
            if (property == null || encoded == null || !IsSupportedShape(property.propertyType, property.depth, property.isArray))
            {
                message = "反映先の項目が見つからないか、対応していない型です。";
                return false;
            }
            if (!KindMatchesProperty(property, encoded.Kind))
            {
                message = "記録した値の種類が反映先の項目型と一致しません。";
                return false;
            }

            try
            {
                var parts = encoded.Payload.Split(',');
                switch (encoded.Kind)
                {
                    case PlayModeTuningValueKind.Boolean:
                        property.boolValue = StringComparer.Ordinal.Equals(encoded.Payload, "1");
                        return property.propertyType == SerializedPropertyType.Boolean;
                    case PlayModeTuningValueKind.SignedInteger:
                        return WriteSignedInteger(property, encoded.Payload);
                    case PlayModeTuningValueKind.UnsignedInteger:
                        return WriteUnsignedInteger(property, encoded.Payload);
                    case PlayModeTuningValueKind.Character:
                        property.intValue = int.Parse(encoded.Payload, CultureInfo.InvariantCulture);
                        return property.propertyType == SerializedPropertyType.Character;
                    case PlayModeTuningValueKind.Float:
                        property.floatValue = DecodeFloat(encoded.Payload);
                        return property.propertyType == SerializedPropertyType.Float && property.numericType == SerializedPropertyNumericType.Float;
                    case PlayModeTuningValueKind.Double:
                        property.doubleValue = DecodeDouble(encoded.Payload);
                        return property.propertyType == SerializedPropertyType.Float && property.numericType == SerializedPropertyNumericType.Double;
                    case PlayModeTuningValueKind.String:
                        property.stringValue = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Payload));
                        return property.propertyType == SerializedPropertyType.String;
                    case PlayModeTuningValueKind.Enum:
                        property.intValue = int.Parse(encoded.Payload, CultureInfo.InvariantCulture);
                        return property.propertyType == SerializedPropertyType.Enum;
                    case PlayModeTuningValueKind.LayerMask:
                        property.intValue = int.Parse(encoded.Payload, CultureInfo.InvariantCulture);
                        return property.propertyType == SerializedPropertyType.LayerMask;
                    case PlayModeTuningValueKind.Color:
                        property.colorValue = new Color(DecodeFloat(parts[0]), DecodeFloat(parts[1]), DecodeFloat(parts[2]), DecodeFloat(parts[3]));
                        return property.propertyType == SerializedPropertyType.Color;
                    case PlayModeTuningValueKind.Vector2:
                        property.vector2Value = new Vector2(DecodeFloat(parts[0]), DecodeFloat(parts[1]));
                        return property.propertyType == SerializedPropertyType.Vector2;
                    case PlayModeTuningValueKind.Vector3:
                        property.vector3Value = new Vector3(DecodeFloat(parts[0]), DecodeFloat(parts[1]), DecodeFloat(parts[2]));
                        return property.propertyType == SerializedPropertyType.Vector3;
                    case PlayModeTuningValueKind.Vector4:
                        property.vector4Value = new Vector4(DecodeFloat(parts[0]), DecodeFloat(parts[1]), DecodeFloat(parts[2]), DecodeFloat(parts[3]));
                        return property.propertyType == SerializedPropertyType.Vector4;
                    case PlayModeTuningValueKind.Vector2Int:
                        property.vector2IntValue = new Vector2Int(ParseInt(parts[0]), ParseInt(parts[1]));
                        return property.propertyType == SerializedPropertyType.Vector2Int;
                    case PlayModeTuningValueKind.Vector3Int:
                        property.vector3IntValue = new Vector3Int(ParseInt(parts[0]), ParseInt(parts[1]), ParseInt(parts[2]));
                        return property.propertyType == SerializedPropertyType.Vector3Int;
                    case PlayModeTuningValueKind.Rect:
                        property.rectValue = new Rect(DecodeFloat(parts[0]), DecodeFloat(parts[1]), DecodeFloat(parts[2]), DecodeFloat(parts[3]));
                        return property.propertyType == SerializedPropertyType.Rect;
                    case PlayModeTuningValueKind.RectInt:
                        property.rectIntValue = new RectInt(ParseInt(parts[0]), ParseInt(parts[1]), ParseInt(parts[2]), ParseInt(parts[3]));
                        return property.propertyType == SerializedPropertyType.RectInt;
                    case PlayModeTuningValueKind.Bounds:
                        property.boundsValue = new Bounds(new Vector3(DecodeFloat(parts[0]), DecodeFloat(parts[1]), DecodeFloat(parts[2])), new Vector3(DecodeFloat(parts[3]), DecodeFloat(parts[4]), DecodeFloat(parts[5])));
                        return property.propertyType == SerializedPropertyType.Bounds;
                    case PlayModeTuningValueKind.BoundsInt:
                        property.boundsIntValue = new BoundsInt(new Vector3Int(ParseInt(parts[0]), ParseInt(parts[1]), ParseInt(parts[2])), new Vector3Int(ParseInt(parts[3]), ParseInt(parts[4]), ParseInt(parts[5])));
                        return property.propertyType == SerializedPropertyType.BoundsInt;
                    case PlayModeTuningValueKind.Quaternion:
                        property.quaternionValue = new Quaternion(DecodeFloat(parts[0]), DecodeFloat(parts[1]), DecodeFloat(parts[2]), DecodeFloat(parts[3]));
                        return property.propertyType == SerializedPropertyType.Quaternion;
                    default:
                        message = "記録した値の種類には対応していません。";
                        return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                message = "記録した値を書き込めませんでした。詳しくはコンソールを確認してください。";
                return false;
            }
        }

        internal static string EncodeFloat(float value)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return unchecked((uint)bits).ToString("X8", CultureInfo.InvariantCulture);
        }

        internal static float DecodeFloat(string value)
        {
            var bits = uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        internal static string EncodeDouble(double value)
        {
            var bits = BitConverter.ToInt64(BitConverter.GetBytes(value), 0);
            return unchecked((ulong)bits).ToString("X16", CultureInfo.InvariantCulture);
        }

        internal static double DecodeDouble(string value)
        {
            var bits = ulong.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
        }

        internal static bool TryCreateCanonicalDisplay(PlayModeTuningEncodedValue encoded, out string display)
        {
            display = string.Empty;
            if (encoded == null)
                return false;
            try
            {
                switch (encoded.Kind)
                {
                    case PlayModeTuningValueKind.Boolean:
                        if (!StringComparer.Ordinal.Equals(encoded.Payload, "0") && !StringComparer.Ordinal.Equals(encoded.Payload, "1"))
                            return false;
                        display = StringComparer.Ordinal.Equals(encoded.Payload, "1") ? "真" : "偽";
                        return true;
                    case PlayModeTuningValueKind.SignedInteger:
                        display = long.Parse(encoded.Payload, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                        return true;
                    case PlayModeTuningValueKind.UnsignedInteger:
                        display = ulong.Parse(encoded.Payload, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                        return true;
                    case PlayModeTuningValueKind.Character:
                        var character = int.Parse(encoded.Payload, CultureInfo.InvariantCulture);
                        if (character < char.MinValue || character > char.MaxValue)
                            return false;
                        display = "U+" + character.ToString("X4", CultureInfo.InvariantCulture);
                        return true;
                    case PlayModeTuningValueKind.Float:
                        var floatValue = DecodeFloat(encoded.Payload);
                        if (!IsFinite(floatValue))
                            return false;
                        display = DisplayFloat(floatValue);
                        return true;
                    case PlayModeTuningValueKind.Double:
                        var doubleValue = DecodeDouble(encoded.Payload);
                        if (!IsFinite(doubleValue))
                            return false;
                        display = DisplayDouble(doubleValue);
                        return true;
                    case PlayModeTuningValueKind.String:
                        var bytes = Convert.FromBase64String(encoded.Payload);
                        if (bytes.Length > MaximumStringUtf8Bytes || !StringComparer.Ordinal.Equals(Convert.ToBase64String(bytes), encoded.Payload))
                            return false;
                        display = new UTF8Encoding(false, true).GetString(bytes);
                        return true;
                    case PlayModeTuningValueKind.Enum:
                    case PlayModeTuningValueKind.LayerMask:
                        display = int.Parse(encoded.Payload, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                        return true;
                    case PlayModeTuningValueKind.Color:
                    case PlayModeTuningValueKind.Vector2:
                    case PlayModeTuningValueKind.Vector3:
                    case PlayModeTuningValueKind.Vector4:
                    case PlayModeTuningValueKind.Rect:
                    case PlayModeTuningValueKind.Bounds:
                    case PlayModeTuningValueKind.Quaternion:
                        return TryCreateFloatTupleDisplay(encoded.Payload, FloatTupleCount(encoded.Kind), out display);
                    case PlayModeTuningValueKind.Vector2Int:
                    case PlayModeTuningValueKind.Vector3Int:
                    case PlayModeTuningValueKind.RectInt:
                    case PlayModeTuningValueKind.BoundsInt:
                        return TryCreateIntegerTupleDisplay(encoded.Payload, IntegerTupleCount(encoded.Kind), out display);
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                display = string.Empty;
                return false;
            }
        }

        private static bool TryEncodeInteger(SerializedProperty property, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            encoded = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            switch (property.numericType)
            {
                case SerializedPropertyNumericType.Int8:
                case SerializedPropertyNumericType.Int16:
                case SerializedPropertyNumericType.Int32:
                    encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.SignedInteger, property.intValue.ToString(CultureInfo.InvariantCulture), property.intValue.ToString(CultureInfo.InvariantCulture));
                    return true;
                case SerializedPropertyNumericType.Int64:
                    encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.SignedInteger, property.longValue.ToString(CultureInfo.InvariantCulture), property.longValue.ToString(CultureInfo.InvariantCulture));
                    return true;
                case SerializedPropertyNumericType.UInt8:
                case SerializedPropertyNumericType.UInt16:
                case SerializedPropertyNumericType.UInt32:
                    encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.UnsignedInteger, property.uintValue.ToString(CultureInfo.InvariantCulture), property.uintValue.ToString(CultureInfo.InvariantCulture));
                    return true;
                case SerializedPropertyNumericType.UInt64:
                    encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.UnsignedInteger, property.ulongValue.ToString(CultureInfo.InvariantCulture), property.ulongValue.ToString(CultureInfo.InvariantCulture));
                    return true;
                default:
                    return Fail(PlayModeTuningError.UnsupportedProperty, "この整数型には対応していません。", out error, out message);
            }
        }

        private static bool TryEncodeFloatProperty(SerializedProperty property, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            encoded = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            if (property.numericType == SerializedPropertyNumericType.Float)
            {
                var value = property.floatValue;
                if (!IsFinite(value))
                    return Fail(PlayModeTuningError.NonFiniteValue, "非数と無限大には対応していません。", out error, out message);
                encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Float, EncodeFloat(value), DisplayFloat(value));
                return true;
            }
            if (property.numericType == SerializedPropertyNumericType.Double)
            {
                var value = property.doubleValue;
                if (!IsFinite(value))
                    return Fail(PlayModeTuningError.NonFiniteValue, "非数と無限大には対応していません。", out error, out message);
                encoded = new PlayModeTuningEncodedValue(PlayModeTuningValueKind.Double, EncodeDouble(value), DisplayDouble(value));
                return true;
            }
            return Fail(PlayModeTuningError.UnsupportedProperty, "この浮動小数点型には対応していません。", out error, out message);
        }

        private static bool TryEncodeColor(Color value, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            return TryEncodeFloats(PlayModeTuningValueKind.Color, new[] { value.r, value.g, value.b, value.a }, out encoded, out error, out message);
        }

        private static bool TryEncodeVector2(Vector2 value, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            return TryEncodeFloats(PlayModeTuningValueKind.Vector2, new[] { value.x, value.y }, out encoded, out error, out message);
        }

        private static bool TryEncodeVector3(Vector3 value, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            return TryEncodeFloats(PlayModeTuningValueKind.Vector3, new[] { value.x, value.y, value.z }, out encoded, out error, out message);
        }

        private static bool TryEncodeVector4(Vector4 value, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            return TryEncodeFloats(PlayModeTuningValueKind.Vector4, new[] { value.x, value.y, value.z, value.w }, out encoded, out error, out message);
        }

        private static bool TryEncodeFloats(PlayModeTuningValueKind kind, float[] values, out PlayModeTuningEncodedValue encoded, out PlayModeTuningError error, out string message)
        {
            encoded = null;
            error = PlayModeTuningError.None;
            message = string.Empty;
            if (values.Any(value => !IsFinite(value)))
                return Fail(PlayModeTuningError.NonFiniteValue, "非数と無限大には対応していません。", out error, out message);
            encoded = new PlayModeTuningEncodedValue(kind, string.Join(",", values.Select(EncodeFloat)), string.Join(", ", values.Select(DisplayFloat)));
            return true;
        }

        private static bool WriteSignedInteger(SerializedProperty property, string payload)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
                return false;
            if (property.numericType == SerializedPropertyNumericType.Int64)
                property.longValue = long.Parse(payload, CultureInfo.InvariantCulture);
            else if (property.numericType == SerializedPropertyNumericType.Int8 || property.numericType == SerializedPropertyNumericType.Int16 || property.numericType == SerializedPropertyNumericType.Int32)
                property.intValue = int.Parse(payload, CultureInfo.InvariantCulture);
            else
                return false;
            return true;
        }

        private static bool WriteUnsignedInteger(SerializedProperty property, string payload)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
                return false;
            if (property.numericType == SerializedPropertyNumericType.UInt64)
                property.ulongValue = ulong.Parse(payload, CultureInfo.InvariantCulture);
            else if (property.numericType == SerializedPropertyNumericType.UInt8 || property.numericType == SerializedPropertyNumericType.UInt16 || property.numericType == SerializedPropertyNumericType.UInt32)
                property.uintValue = uint.Parse(payload, CultureInfo.InvariantCulture);
            else
                return false;
            return true;
        }

        private static bool KindMatchesProperty(SerializedProperty property, PlayModeTuningValueKind kind)
        {
            switch (kind)
            {
                case PlayModeTuningValueKind.Boolean:
                    return property.propertyType == SerializedPropertyType.Boolean;
                case PlayModeTuningValueKind.SignedInteger:
                    return property.propertyType == SerializedPropertyType.Integer && (property.numericType == SerializedPropertyNumericType.Int8 || property.numericType == SerializedPropertyNumericType.Int16 || property.numericType == SerializedPropertyNumericType.Int32 || property.numericType == SerializedPropertyNumericType.Int64);
                case PlayModeTuningValueKind.UnsignedInteger:
                    return property.propertyType == SerializedPropertyType.Integer && (property.numericType == SerializedPropertyNumericType.UInt8 || property.numericType == SerializedPropertyNumericType.UInt16 || property.numericType == SerializedPropertyNumericType.UInt32 || property.numericType == SerializedPropertyNumericType.UInt64);
                case PlayModeTuningValueKind.Character:
                    return property.propertyType == SerializedPropertyType.Character;
                case PlayModeTuningValueKind.Float:
                    return property.propertyType == SerializedPropertyType.Float && property.numericType == SerializedPropertyNumericType.Float;
                case PlayModeTuningValueKind.Double:
                    return property.propertyType == SerializedPropertyType.Float && property.numericType == SerializedPropertyNumericType.Double;
                case PlayModeTuningValueKind.String:
                    return property.propertyType == SerializedPropertyType.String;
                case PlayModeTuningValueKind.Enum:
                    return property.propertyType == SerializedPropertyType.Enum;
                case PlayModeTuningValueKind.LayerMask:
                    return property.propertyType == SerializedPropertyType.LayerMask;
                case PlayModeTuningValueKind.Color:
                    return property.propertyType == SerializedPropertyType.Color;
                case PlayModeTuningValueKind.Vector2:
                    return property.propertyType == SerializedPropertyType.Vector2;
                case PlayModeTuningValueKind.Vector3:
                    return property.propertyType == SerializedPropertyType.Vector3;
                case PlayModeTuningValueKind.Vector4:
                    return property.propertyType == SerializedPropertyType.Vector4;
                case PlayModeTuningValueKind.Vector2Int:
                    return property.propertyType == SerializedPropertyType.Vector2Int;
                case PlayModeTuningValueKind.Vector3Int:
                    return property.propertyType == SerializedPropertyType.Vector3Int;
                case PlayModeTuningValueKind.Rect:
                    return property.propertyType == SerializedPropertyType.Rect;
                case PlayModeTuningValueKind.RectInt:
                    return property.propertyType == SerializedPropertyType.RectInt;
                case PlayModeTuningValueKind.Bounds:
                    return property.propertyType == SerializedPropertyType.Bounds;
                case PlayModeTuningValueKind.BoundsInt:
                    return property.propertyType == SerializedPropertyType.BoundsInt;
                case PlayModeTuningValueKind.Quaternion:
                    return property.propertyType == SerializedPropertyType.Quaternion;
                default:
                    return false;
            }
        }

        private static bool TryCreateFloatTupleDisplay(string payload, int expectedCount, out string display)
        {
            display = string.Empty;
            var parts = payload.Split(',');
            if (expectedCount == 0 || parts.Length != expectedCount)
                return false;
            var values = new string[parts.Length];
            for (var index = 0; index < parts.Length; index++)
            {
                if (parts[index].Length != 8)
                    return false;
                var value = DecodeFloat(parts[index]);
                if (!IsFinite(value))
                    return false;
                values[index] = DisplayFloat(value);
            }
            display = string.Join(", ", values);
            return true;
        }

        private static bool TryCreateIntegerTupleDisplay(string payload, int expectedCount, out string display)
        {
            display = string.Empty;
            var parts = payload.Split(',');
            if (expectedCount == 0 || parts.Length != expectedCount)
                return false;
            var values = new string[parts.Length];
            for (var index = 0; index < parts.Length; index++)
                values[index] = int.Parse(parts[index], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            display = string.Join(", ", values);
            return true;
        }

        private static int FloatTupleCount(PlayModeTuningValueKind kind)
        {
            switch (kind)
            {
                case PlayModeTuningValueKind.Vector2:
                    return 2;
                case PlayModeTuningValueKind.Vector3:
                    return 3;
                case PlayModeTuningValueKind.Bounds:
                    return 6;
                case PlayModeTuningValueKind.Color:
                case PlayModeTuningValueKind.Vector4:
                case PlayModeTuningValueKind.Rect:
                case PlayModeTuningValueKind.Quaternion:
                    return 4;
                default:
                    return 0;
            }
        }

        private static int IntegerTupleCount(PlayModeTuningValueKind kind)
        {
            switch (kind)
            {
                case PlayModeTuningValueKind.Vector2Int:
                    return 2;
                case PlayModeTuningValueKind.Vector3Int:
                    return 3;
                case PlayModeTuningValueKind.RectInt:
                    return 4;
                case PlayModeTuningValueKind.BoundsInt:
                    return 6;
                default:
                    return 0;
            }
        }

        private static bool Fail(PlayModeTuningError value, string text, out PlayModeTuningError error, out string message)
        {
            error = value;
            message = text;
            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string DisplayFloat(float value)
        {
            return StringComparer.Ordinal.Equals(EncodeFloat(value), "80000000") ? "-0" : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string DisplayDouble(double value)
        {
            return StringComparer.Ordinal.Equals(EncodeDouble(value), "8000000000000000") ? "-0" : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string Join(params int[] values)
        {
            return string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        }

        private static string DisplayIntegers(params int[] values)
        {
            return string.Join(", ", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
