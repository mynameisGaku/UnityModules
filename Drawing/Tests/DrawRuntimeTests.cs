using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Drawing.Tests
{
    /// <summary>Play Mode の切り替えと不正な既定値に対する実行時の防御を確かめる。</summary>
    public sealed class DrawRuntimeTests
    {
        private static readonly MethodInfo ResetDrawMethod = typeof(Draw).GetMethod(
            "ResetStaticState",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo ResetRendererMethod = typeof(DrawRenderer).GetMethod(
            "ResetStaticState",
            BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            InvokeStatic(ResetDrawMethod);
            InvokeStatic(ResetRendererMethod);
        }

        [TearDown]
        public void TearDown()
        {
            InvokeStatic(ResetDrawMethod);
            InvokeStatic(ResetRendererMethod);
        }

        [Test]
        public void DrawDefaults_SanitizeNonFiniteAndNegativeValues()
        {
            Draw.Color = new Color(float.NaN, 0f, 0f, 1f);
            Draw.Duration = float.PositiveInfinity;
            Draw.Thickness = float.NegativeInfinity;

            Assert.AreEqual(Color.white, Draw.Color);
            Assert.AreEqual(0f, Draw.Duration);
            Assert.AreEqual(1f, Draw.Thickness);

            Draw.Duration = -1f;
            Draw.Thickness = 0f;

            Assert.AreEqual(0f, Draw.Duration);
            Assert.AreEqual(1f, Draw.Thickness);
        }

        [Test]
        public void DrawStaticState_ReturnsToDefaultsAtSubsystemRegistration()
        {
            Draw.Color = Color.red;
            Draw.Duration = 3f;
            Draw.Thickness = 4f;
            Draw.DepthTest = false;

            InvokeStatic(ResetDrawMethod);

            Assert.AreEqual(Color.white, Draw.Color);
            Assert.AreEqual(0f, Draw.Duration);
            Assert.AreEqual(1f, Draw.Thickness);
            Assert.IsTrue(Draw.DepthTest);
        }

        [Test]
        public void RendererStaticState_ClearsTheQuitGuardAtSubsystemRegistration()
        {
            var quittingField = typeof(DrawRenderer).GetField("_quitting", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(quittingField);

            quittingField.SetValue(null, true);
            InvokeStatic(ResetRendererMethod);

            Assert.IsFalse((bool)quittingField.GetValue(null));
        }

        [Test]
        public void Ensure_DoesNotCreateARendererOutsidePlayMode()
        {
            Assert.IsFalse(Application.isPlaying);
            Assert.IsNull(DrawRenderer.Ensure());
        }

        [Test]
        public void RendererResources_CanBeDestroyedSafelyInEditMode()
        {
            var host = new GameObject("Drawing EditMode Destruction Test");
            var renderer = host.AddComponent<DrawRenderer>();
            var ensureResources = typeof(DrawRenderer).GetMethod("EnsureResources", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                Assert.IsNotNull(ensureResources);
                Assert.IsTrue((bool)ensureResources.Invoke(renderer, null));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CameraUnavailableRepaint_DoesNotLeaveSingleFrameLabelsForLater()
        {
            var host = new GameObject("Drawing Camera Absence Test");
            var renderer = host.AddComponent<DrawRenderer>();

            try
            {
                renderer.Buffer.AddLabel(Vector3.zero, "1 フレーム", Color.white, 0f, 1, waitForFirstSubmission: true);
                renderer.Buffer.AddLabel(Vector3.zero, "期限内", Color.white, 100f, 1);

                Assert.IsFalse(renderer.PrepareLabelRepaint(null));
                Assert.AreEqual(1, renderer.Buffer.Labels.Count);
                Assert.AreEqual("期限内", renderer.Buffer.Labels[0].Text);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAssembly_ExportsOnlyDrawAndDrawScope()
        {
            var exported = typeof(Draw).Assembly.GetExportedTypes()
                .Where(type => type.Namespace == typeof(Draw).Namespace)
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { typeof(Draw), typeof(DrawScope) }, exported);
        }

        /// <summary>必須の非公開初期化処理を呼び、見つからない場合は明示的に失敗させる。</summary>
        private static void InvokeStatic(MethodInfo method)
        {
            Assert.IsNotNull(method);
            method.Invoke(null, null);
        }
    }
}
