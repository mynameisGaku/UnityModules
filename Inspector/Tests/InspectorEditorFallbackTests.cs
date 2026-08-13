using System;
using Inspector.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Inspector.Tests
{
    /// <summary>全 Object 向け Inspector と専用 CustomEditor の選択順を検査する。</summary>
    public sealed class InspectorEditorFallbackTests
    {
        [Test]
        public void InspectorEditor_IsRegisteredAsFallback()
        {
            var attribute = (CustomEditor)Attribute.GetCustomAttribute(typeof(InspectorEditor), typeof(CustomEditor));

            Assert.IsNotNull(attribute);
            Assert.IsTrue(attribute.isFallback);
        }

        [Test]
        public void PlainObject_UsesInspectorFallback()
        {
            var target = ScriptableObject.CreateInstance<FallbackTarget>();
            UnityEditor.Editor editor = null;

            try
            {
                editor = UnityEditor.Editor.CreateEditor(target);

                Assert.IsInstanceOf<InspectorEditor>(editor);
            }
            finally
            {
                if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DedicatedEditor_TakesPriorityOverInspectorFallback()
        {
            var target = ScriptableObject.CreateInstance<DedicatedTarget>();
            UnityEditor.Editor editor = null;

            try
            {
                editor = UnityEditor.Editor.CreateEditor(target);

                Assert.IsInstanceOf<DedicatedTargetEditor>(editor);
            }
            finally
            {
                if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class FallbackTarget : ScriptableObject
        {
        }

        private sealed class DedicatedTarget : ScriptableObject
        {
        }

        [CustomEditor(typeof(DedicatedTarget))]
        private sealed class DedicatedTargetEditor : UnityEditor.Editor
        {
        }
    }
}
