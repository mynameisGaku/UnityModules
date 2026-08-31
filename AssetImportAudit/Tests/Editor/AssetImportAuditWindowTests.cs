using System;
using System.Reflection;
using AssetImportAudit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor.Tests
{
    public sealed class AssetImportAuditWindowTests
    {
        private const string FolderPath = "Assets/AssetImportAuditWindowTests";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private AssetImportAuditWindow _window;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets", "AssetImportAuditWindowTests");

            _window = ScriptableObject.CreateInstance<AssetImportAuditWindow>();
            SetField("_rootFolder", FolderPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
                UnityEngine.Object.DestroyImmediate(_window);
            AssetDatabase.DeleteAsset(FolderPath);
        }

        [Test]
        public void PlatformScope_IgnoresInvalidHiddenSharedSettings()
        {
            SetAuditScope(1);
            SetField("_maxTextureSize", 0);

            InvokePreview();

            Assert.That(GetField<AssetImportAuditPlan>("_plan"), Is.Not.Null);
            Assert.That(GetField<string>("_message"), Is.EqualTo("差分はありません。"));
        }

        [Test]
        public void SharedScope_IgnoresInvalidHiddenPlatformSettings()
        {
            SetAuditScope(0);
            SetField("_platformMaxTextureSize", 0);

            InvokePreview();

            Assert.That(GetField<AssetImportAuditPlan>("_plan"), Is.Not.Null);
            Assert.That(GetField<string>("_message"), Is.EqualTo("差分はありません。"));
        }

        [Test]
        public void VisibleMetadata_UsesJapaneseText()
        {
            Assert.That(AssetImportAuditWindow.WindowTitle, Is.EqualTo("テクスチャー取込設定監査"));
            Assert.That(AssetImportAuditWindow.MenuPath, Is.EqualTo("Tools/テクスチャー取込設定監査/開く"));
        }

        [TestCase("maxTextureSize", "最大テクスチャー寸法")]
        [TestCase("textureCompression", "圧縮方法")]
        [TestCase("mipmapEnabled", "ミップマップを生成")]
        [TestCase("sRGBTexture", "sRGBとして扱う")]
        [TestCase("isReadable", "読み取り・書き込みを許可")]
        [TestCase("filterMode", "画素の補間方法")]
        [TestCase("anisoLevel", "異方性レベル")]
        [TestCase("overridden", "個別設定を使用")]
        public void SettingNames_AreFormattedInJapanese(string settingName, string expected)
        {
            Assert.That(AssetImportAuditWindow.FormatSettingName(settingName), Is.EqualTo(expected));
        }

        [TestCase("textureCompression", "Uncompressed", "圧縮なし")]
        [TestCase("textureCompression", "Compressed", "標準圧縮")]
        [TestCase("textureCompression", "CompressedHQ", "高品質圧縮")]
        [TestCase("textureCompression", "CompressedLQ", "低品質圧縮")]
        [TestCase("filterMode", "Point", "点補間")]
        [TestCase("filterMode", "Bilinear", "二線形補間")]
        [TestCase("filterMode", "Trilinear", "三線形補間")]
        [TestCase("mipmapEnabled", "True", "有効")]
        [TestCase("mipmapEnabled", "False", "無効")]
        public void SettingValues_AreFormattedInJapanese(string settingName, string value, string expected)
        {
            Assert.That(AssetImportAuditWindow.FormatSettingValue(settingName, value), Is.EqualTo(expected));
        }

        [TestCase(AssetImportAuditTexturePlatform.Standalone, "パソコン")]
        [TestCase(AssetImportAuditTexturePlatform.Android, "Android")]
        [TestCase(AssetImportAuditTexturePlatform.iOS, "iOS")]
        public void Platforms_AreFormattedForUsers(AssetImportAuditTexturePlatform platform, string expected)
        {
            Assert.That(AssetImportAuditWindow.FormatPlatformName(platform), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownPlatform_IsNotPresentedAsSharedSettings()
        {
            Assert.That(AssetImportAuditWindow.FormatPlatformName((AssetImportAuditTexturePlatform)999), Is.EqualTo("対応していない対象機種"));
        }

        [TestCase("rootFolder", "対象フォルダーが存在しません。")]
        [TestCase("Compression", "圧縮方法に対応していない値が指定されています。")]
        [TestCase("FilterMode", "画素の補間方法に対応していない値が指定されています。")]
        [TestCase("AnisoLevel", "異方性レベルには0から16までの値を指定してください。")]
        public void KnownInputErrors_AreFormattedWithoutRuntimeParameterText(string parameterName, string expected)
        {
            var exception = parameterName == "rootFolder"
                ? new ArgumentException("対象フォルダーが存在しません。", parameterName)
                : new ArgumentOutOfRangeException(parameterName);

            Assert.That(AssetImportAuditWindow.TryFormatInputError(exception, out var message), Is.True);
            Assert.That(message, Is.EqualTo(expected));
            Assert.That(message, Does.Not.Contain("Parameter"));
        }

        [Test]
        public void UnexpectedArgumentError_IsNotExposedAsInputGuidance()
        {
            Assert.That(AssetImportAuditWindow.TryFormatInputError(new ArgumentException("内部情報", "unexpected"), out var message), Is.False);
            Assert.That(message, Is.Null);
        }

        [Test]
        public void UnknownSharedCompression_IsRejectedWithoutChangingToFirstChoice()
        {
            SetAuditScope(0);
            SetField("_compression", (TextureImporterCompression)999);

            InvokePreview();

            Assert.That(GetField<AssetImportAuditPlan>("_plan"), Is.Null);
            Assert.That(GetField<string>("_message"), Is.EqualTo("圧縮方法に対応していない値が指定されています。"));
            Assert.That(GetField<TextureImporterCompression>("_compression"), Is.EqualTo((TextureImporterCompression)999));
        }

        [Test]
        public void UnknownPlatform_IsRejectedWithoutChangingToComputerChoice()
        {
            SetAuditScope(1);
            SetField("_platform", (AssetImportAuditTexturePlatform)999);

            InvokePreview();

            Assert.That(GetField<AssetImportAuditPlan>("_plan"), Is.Null);
            Assert.That(GetField<string>("_message"), Is.EqualTo("対象機種に対応していない値が指定されています。"));
            Assert.That(GetField<AssetImportAuditTexturePlatform>("_platform"), Is.EqualTo((AssetImportAuditTexturePlatform)999));
        }

        [Test]
        public void MissingFolder_IsReportedWithoutRuntimeParameterText()
        {
            SetField("_rootFolder", FolderPath + "/存在しないフォルダー");

            InvokePreview();

            Assert.That(GetField<AssetImportAuditPlan>("_plan"), Is.Null);
            Assert.That(GetField<string>("_message"), Is.EqualTo("対象フォルダーが存在しません。"));
        }

        [TestCase(AssetImportAuditError.StalePlan, "差分確認後に対象が変更されました")]
        [TestCase(AssetImportAuditError.NoChanges, "反映対象がありません")]
        [TestCase(AssetImportAuditError.ApplyFailed, "取込設定の反映処理で問題が発生しました")]
        public void Errors_AreFormattedInJapanese(AssetImportAuditError error, string expected)
        {
            Assert.That(AssetImportAuditWindow.FormatError(error), Is.EqualTo(expected));
        }

        private void InvokePreview()
        {
            typeof(AssetImportAuditWindow).GetMethod("Preview", InstancePrivate)?.Invoke(_window, null);
        }

        private void SetAuditScope(int value)
        {
            var field = typeof(AssetImportAuditWindow).GetField("_auditScope", InstancePrivate);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_window, Enum.ToObject(field.FieldType, value));
        }

        private void SetField<T>(string name, T value)
        {
            var field = typeof(AssetImportAuditWindow).GetField(name, InstancePrivate);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_window, value);
        }

        private T GetField<T>(string name)
        {
            var field = typeof(AssetImportAuditWindow).GetField(name, InstancePrivate);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(_window);
        }
    }
}
