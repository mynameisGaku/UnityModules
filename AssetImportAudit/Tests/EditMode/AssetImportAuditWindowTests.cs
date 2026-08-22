using System;
using System.Reflection;
using AssetImportAudit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Tests
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
            Assert.That(GetField<string>("_message"), Is.EqualTo("No differences found."));
        }

        [Test]
        public void SharedScope_IgnoresInvalidHiddenPlatformSettings()
        {
            SetAuditScope(0);
            SetField("_platformMaxTextureSize", 0);

            InvokePreview();

            Assert.That(GetField<AssetImportAuditPlan>("_plan"), Is.Not.Null);
            Assert.That(GetField<string>("_message"), Is.EqualTo("No differences found."));
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
