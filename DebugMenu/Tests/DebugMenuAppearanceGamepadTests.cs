using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DebugMenu.Tests
{
    public sealed class DebugMenuAppearanceGamepadTests
    {
        private const float Tolerance = 0.0001f;

        [SetUp]
        public void SetUp() => InvokePrivateStaticMethod(typeof(DebugMenuController).Assembly, "DebugMenu.DebugMenuPauseCoordinator", "Reset");

        [Test]
        public void GamepadSample_MapsDirectionsAndStandardButtons()
        {
            var sample = new DebugMenuGamepadSample
            {
                Dpad = new Vector2(1f, -1f),
                LeftStick = new Vector2(-0.75f, 0.8f),
                SouthPressed = true,
                EastPressed = true,
                LeftShoulderPressed = true,
                RightShoulderPressed = true,
                StartPressed = true,
            };

            var state = DebugMenuGamepadInput.Map(sample);

            Assert.IsTrue(state.Up);
            Assert.IsTrue(state.Down);
            Assert.IsTrue(state.Left);
            Assert.IsTrue(state.Right);
            Assert.IsTrue(state.Decide);
            Assert.IsTrue(state.Cancel);
            Assert.IsTrue(state.PreviousPage);
            Assert.IsTrue(state.NextPage);
            Assert.IsTrue(state.ToggleMenu);
        }

        [Test]
        public void GamepadSample_UsesConfigurableStickDeadZone()
        {
            var sample = new DebugMenuGamepadSample { LeftStick = new Vector2(0.49f, -0.51f) };

            var standard = DebugMenuGamepadInput.Map(sample);
            var sensitive = DebugMenuGamepadInput.Map(sample, 0.25f);

            Assert.IsFalse(standard.Right);
            Assert.IsTrue(standard.Down);
            Assert.IsTrue(sensitive.Right);
        }

        [Test]
        public void InputState_CombineKeepsCommandsFromBothDevices()
        {
            var keyboard = new DebugMenuInputState
            {
                Up = true,
                Decide = true,
                PageUp = true,
                PreviousPage = true,
                ToggleFavorite = true,
                Search = true,
                Undo = true,
            };
            var gamepad = new DebugMenuInputState
            {
                ToggleMenu = true,
                Down = true,
                Left = true,
                Right = true,
                Cancel = true,
                PageDown = true,
                NextPage = true,
                ResetValue = true,
                Redo = true,
            };

            var combined = DebugMenuInputState.Combine(keyboard, gamepad);

            foreach (DebugMenuCommand command in Enum.GetValues(typeof(DebugMenuCommand)))
            {
                if (command == DebugMenuCommand.None) continue;
                Assert.IsTrue(combined.IsHeld(command), $"{command} が合成結果から落ちている");
            }
        }

        [Test]
        public void ToggleCommand_ChangesMenuVisibility()
        {
            var menu = new DebugMenuRoot();

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ToggleMenu);
            Assert.IsTrue(menu.IsVisible);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ToggleMenu);
            Assert.IsFalse(menu.IsVisible);
        }

        [Test]
        public void AppearancePage_UsesExpectedRangesAndStableSaveKeys()
        {
            var theme = new DebugMenuTheme();
            var requests = 0;
            var appearance = new DebugMenuAppearancePage(theme, () => requests++);

            Assert.AreEqual("Appearance", appearance.Page.Name);
            Assert.AreEqual(6, appearance.Page.Root.Children.Count);

            var fontSize = (DebugInt)appearance.Page.Root.Children[0];
            var guiScale = (DebugFloat)appearance.Page.Root.Children[1];
            var rowHeight = (DebugFloat)appearance.Page.Root.Children[2];
            var panelMargin = (DebugFloat)appearance.Page.Root.Children[3];
            var topMargin = (DebugFloat)appearance.Page.Root.Children[4];

            Assert.AreEqual(8, fontSize.Min);
            Assert.AreEqual(48, fontSize.Max);
            Assert.AreEqual("debug-menu.appearance.font-size", fontSize.SaveKey);
            Assert.That(guiScale.Min, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(guiScale.Max, Is.EqualTo(4f).Within(Tolerance));
            Assert.AreEqual("debug-menu.appearance.gui-scale", guiScale.SaveKey);
            Assert.That(rowHeight.Min, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(rowHeight.Max, Is.EqualTo(96f).Within(Tolerance));
            Assert.AreEqual("debug-menu.appearance.row-height", rowHeight.SaveKey);
            Assert.AreEqual("debug-menu.appearance.panel-margin", panelMargin.SaveKey);
            Assert.AreEqual("debug-menu.appearance.top-margin", topMargin.SaveKey);

            fontSize.Value = 99;
            Assert.AreEqual(48, theme.FontSize);
            Assert.AreEqual(1, requests);
        }

        [TestCase(DebugMenuAppearancePreset.Compact, 14, 0.85f, 18f, 16f, 12f)]
        [TestCase(DebugMenuAppearancePreset.Standard, 20, 1f, 20f, 24f, 16f)]
        [TestCase(DebugMenuAppearancePreset.Large, 28, 1.25f, 24f, 32f, 24f)]
        public void AppearancePreset_ChangesFiveValuesWithOneRefresh(
            DebugMenuAppearancePreset preset,
            int fontSize,
            float guiScale,
            float rowHeight,
            float panelMargin,
            float topMargin)
        {
            var theme = new DebugMenuTheme
            {
                FontSize = 17,
                GuiScale = 0.9f,
                RowHeight = 21f,
                PanelMargin = 7f,
                TopMargin = 9f,
            };
            var requests = 0;
            var appearance = new DebugMenuAppearancePage(theme, () => requests++);

            appearance.ApplyPreset(preset);

            Assert.AreEqual(fontSize, theme.FontSize);
            Assert.That(theme.GuiScale, Is.EqualTo(guiScale).Within(Tolerance));
            Assert.That(theme.RowHeight, Is.EqualTo(rowHeight).Within(Tolerance));
            Assert.That(theme.PanelMargin, Is.EqualTo(panelMargin).Within(Tolerance));
            Assert.That(theme.TopMargin, Is.EqualTo(topMargin).Within(Tolerance));
            Assert.AreEqual(1, requests);

            appearance.Reset();
            Assert.AreEqual(17, theme.FontSize);
            Assert.That(theme.GuiScale, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(theme.RowHeight, Is.EqualTo(21f).Within(Tolerance));
            Assert.That(theme.PanelMargin, Is.EqualTo(7f).Within(Tolerance));
            Assert.That(theme.TopMargin, Is.EqualTo(9f).Within(Tolerance));
            Assert.AreEqual(2, requests);
        }

        [TestCase(0, 32f)]
        [TestCase(1, 1.5f)]
        [TestCase(2, 36f)]
        [TestCase(3, 42f)]
        [TestCase(4, 30f)]
        public void AppearanceValue_ApplyCallbackFailureRollsBackAndAllowsRetry(int rowIndex, float requestedValue)
        {
            var theme = new DebugMenuTheme
            {
                FontSize = 17,
                GuiScale = 0.9f,
                RowHeight = 21f,
                PanelMargin = 7f,
                TopMargin = 9f,
            };
            var throwOnApply = true;
            var requests = 0;
            var appearance = new DebugMenuAppearancePage(theme, () =>
            {
                requests++;
                if (throwOnApply) throw new InvalidOperationException("appearance apply failed");
            });
            var row = appearance.Page.Root.Children[rowIndex];

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            var failed = rowIndex == 0
                ? row.TrySetInt(Mathf.RoundToInt(requestedValue))
                : row.TrySetFloat(requestedValue);

            Assert.IsFalse(failed);
            Assert.AreEqual(17, theme.FontSize);
            Assert.That(theme.GuiScale, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(theme.RowHeight, Is.EqualTo(21f).Within(Tolerance));
            Assert.That(theme.PanelMargin, Is.EqualTo(7f).Within(Tolerance));
            Assert.That(theme.TopMargin, Is.EqualTo(9f).Within(Tolerance));
            Assert.IsTrue(row.HasError);

            throwOnApply = false;
            var recovered = rowIndex == 0
                ? row.TrySetInt(Mathf.RoundToInt(requestedValue))
                : row.TrySetFloat(requestedValue);

            Assert.IsTrue(recovered, "同じ値を指定した再試行が早期returnされた");
            Assert.AreEqual(2, requests);
            Assert.IsFalse(row.HasError);
        }

        [Test]
        public void AppearancePreset_ApplyCallbackFailureRollsBackAllValuesAndAllowsRetry()
        {
            var theme = new DebugMenuTheme
            {
                FontSize = 17,
                GuiScale = 0.9f,
                RowHeight = 21f,
                PanelMargin = 7f,
                TopMargin = 9f,
            };
            var throwOnApply = true;
            var appearance = new DebugMenuAppearancePage(theme, () =>
            {
                if (throwOnApply) throw new InvalidOperationException("preset apply failed");
            });
            var menu = new DebugMenuRoot();
            menu.AddPage(appearance.Page);
            var presets = appearance.Page.Root.Children[5];
            var compact = presets.Children[0];
            Assert.IsTrue(appearance.Page.FocusOn(compact));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(menu.Decide);
            Assert.AreEqual(17, theme.FontSize);
            Assert.That(theme.GuiScale, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(theme.RowHeight, Is.EqualTo(21f).Within(Tolerance));
            Assert.That(theme.PanelMargin, Is.EqualTo(7f).Within(Tolerance));
            Assert.That(theme.TopMargin, Is.EqualTo(9f).Within(Tolerance));
            Assert.IsTrue(compact.HasError);

            throwOnApply = false;
            menu.Decide();
            Assert.AreEqual(14, theme.FontSize);
            Assert.That(theme.GuiScale, Is.EqualTo(0.85f).Within(Tolerance));
            Assert.IsFalse(compact.HasError);
        }

        [Test]
        public void Controller_RegistersAppearanceBeforeSettingsAndExposesToastService()
        {
            var gameObject = new GameObject("DebugMenuAppearanceControllerTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);

            try
            {
                gameObject.SetActive(true);
                InvokePrivateMethod(controller, "Awake");

                Assert.AreSame(controller.AppearancePage.Page, controller.Menu.FindPage("Appearance"));
                Assert.NotNull(controller.Toasts);
                Assert.Less(
                    FindPageIndex(controller.Menu, "Appearance"),
                    FindPageIndex(controller.Menu, "Settings"),
                    "Appearance は保存値の読み込み前に登録できる順序でなければならない");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Controller_CustomInputProviderIsNotReadWhileHidden()
        {
            var gameObject = new GameObject("DebugMenuCustomInputContractTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);

            try
            {
                gameObject.SetActive(true);
                InvokePrivateMethod(controller, "Awake");
                WritePrivateField(controller, "_view", new DebugMenuView(controller.Menu, controller.Theme, controller.Toasts));

                var calls = 0;
                controller.InputProvider = () =>
                {
                    calls++;
                    return new DebugMenuInputState { Down = true };
                };

                InvokeUpdate(controller);
                Assert.AreEqual(0, calls, "非表示中に差し替え入力を読んでいる");

                controller.Menu.SetVisible(true);
                InvokeUpdate(controller);
                Assert.AreEqual(1, calls, "表示中の差し替え入力が読まれていない");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Controller_InputProviderFailureIsIsolatedAndSameWarningIsSuppressed()
        {
            var gameObject = new GameObject("DebugMenuInputFailureIsolationTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);

            try
            {
                gameObject.SetActive(true);
                InvokePrivateMethod(controller, "Awake");
                WritePrivateField(controller, "_view", new DebugMenuView(controller.Menu, controller.Theme, controller.Toasts));
                controller.Menu.SetVisible(true);

                var calls = 0;
                controller.InputProvider = () =>
                {
                    calls++;
                    throw new InvalidOperationException("input provider failed");
                };

                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        @"(?s)\[DebugMenu\].*入力プロバイダー.*InvalidOperationException.*input provider failed"));

                Assert.DoesNotThrow(() => InvokeUpdate(controller));
                Assert.DoesNotThrow(() => InvokeUpdate(controller));
                Assert.AreEqual(2, calls);
                Assert.IsTrue(controller.Menu.IsVisible, "入力取得の失敗でメニューが閉じている");
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Controller_InputProviderRecoveryAcceptsHeldCommandAsNewInput()
        {
            var gameObject = new GameObject("DebugMenuInputRecoveryTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);

            try
            {
                gameObject.SetActive(true);
                InvokePrivateMethod(controller, "Awake");
                WritePrivateField(controller, "_view", new DebugMenuView(controller.Menu, controller.Theme, controller.Toasts));

                var page = controller.Menu.AddPage("Input Recovery");
                page.Root.Add(new DebugElement("First"));
                page.Root.Add(new DebugElement("Second"));
                page.Root.Add(new DebugElement("Third"));
                controller.Menu.SetRootPage(page);
                controller.Menu.SetVisible(true);

                var shouldThrow = false;
                controller.InputProvider = () =>
                {
                    if (shouldThrow) throw new InvalidOperationException("temporary input failure");
                    return new DebugMenuInputState { Down = true };
                };

                InvokeUpdate(controller);
                Assert.AreEqual(1, page.CursorIndex);

                shouldThrow = true;
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(@"(?s)\[DebugMenu\].*入力プロバイダー.*temporary input failure"));
                Assert.DoesNotThrow(() => InvokeUpdate(controller));
                Assert.AreEqual(1, page.CursorIndex, "取得失敗時に直前の入力を再利用している");

                shouldThrow = false;
                InvokeUpdate(controller);
                Assert.AreEqual(2, page.CursorIndex, "回復後の入力を押しっぱなしの続きとして捨てている");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyTheme_WaitsForActivePointerInteraction()
        {
            var gameObject = new GameObject("DebugMenuPointerThemeApplyTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);

            try
            {
                gameObject.SetActive(true);
                InvokePrivateMethod(controller, "Awake");
                var view = new DebugMenuView(controller.Menu, controller.Theme, controller.Toasts);
                var row = new DebugRowView(controller.Theme);
                row.Bind(new DebugRow(new DebugFloat("Scale", 1f).WithRange(0f, 2f), 0), true, 0);
                WritePrivateField(row, "_sliderPointerId", 1);
                ReadPrivateField<ListView>(view, "_list").hierarchy.Add(row);
                WritePrivateField(controller, "_view", view);

                controller.ApplyTheme();

                Assert.IsTrue(row.HasActivePointerInteraction);
                Assert.AreSame(view, ReadPrivateField<DebugMenuView>(controller, "_view"));
                Assert.IsTrue(ReadPrivateField<bool>(controller, "_themeRefreshPending"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Controllers_RestoreTimeScaleAfterLastPauseOwnerCloses()
        {
            var firstObject = new GameObject("DebugMenuFirstPauseOwner");
            var secondObject = new GameObject("DebugMenuSecondPauseOwner");
            firstObject.SetActive(false);
            secondObject.SetActive(false);
            var first = firstObject.AddComponent<DebugMenuController>();
            var second = secondObject.AddComponent<DebugMenuController>();
            WritePrivateField(first, "_persistValues", false);
            WritePrivateField(second, "_persistValues", false);
            var original = Time.timeScale;

            try
            {
                Time.timeScale = 0.75f;
                InvokePrivateMethod(first, "Awake");
                InvokePrivateMethod(second, "Awake");

                first.Menu.SetVisible(true);
                second.Menu.SetVisible(true);
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(Tolerance));

                first.Menu.SetVisible(false);
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(Tolerance), "別の表示中メニューを無視して再開した");

                second.Menu.SetVisible(false);
                Assert.That(Time.timeScale, Is.EqualTo(0.75f).Within(Tolerance));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                Time.timeScale = original;
            }
        }

        [Test]
        public void Controller_DoesNotOverwriteExternalTimeScaleChangeOnRelease()
        {
            var gameObject = new GameObject("DebugMenuExternalTimeScaleTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);
            var original = Time.timeScale;

            try
            {
                Time.timeScale = 0.8f;
                InvokePrivateMethod(controller, "Awake");
                controller.Menu.SetVisible(true);
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(Tolerance));

                Time.timeScale = 0.35f;
                controller.Menu.SetVisible(false);

                Assert.That(Time.timeScale, Is.EqualTo(0.35f).Within(Tolerance), "外部の時間倍率を古い値で上書きした");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                Time.timeScale = original;
            }
        }

        [Test]
        public void Controller_DestroyWhileVisibleRestoresTimeScale()
        {
            var gameObject = new GameObject("DebugMenuDestroyedPauseOwner");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<DebugMenuController>();
            WritePrivateField(controller, "_persistValues", false);
            var original = Time.timeScale;

            try
            {
                Time.timeScale = 0.65f;
                InvokePrivateMethod(controller, "Awake");
                controller.Menu.SetVisible(true);
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(Tolerance));

                InvokePrivateMethod(controller, "OnDestroy");

                Assert.That(Time.timeScale, Is.EqualTo(0.65f).Within(Tolerance), "表示中の破棄で時間倍率を戻していない");
            }
            finally
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
                Time.timeScale = original;
            }
        }

        [Test]
        public void Controllers_DestroyingOneOwnerKeepsPauseUntilLastOwnerCloses()
        {
            var firstObject = new GameObject("DebugMenuDestroyedFirstPauseOwner");
            var secondObject = new GameObject("DebugMenuRemainingPauseOwner");
            firstObject.SetActive(false);
            secondObject.SetActive(false);
            var first = firstObject.AddComponent<DebugMenuController>();
            var second = secondObject.AddComponent<DebugMenuController>();
            WritePrivateField(first, "_persistValues", false);
            WritePrivateField(second, "_persistValues", false);
            var original = Time.timeScale;

            try
            {
                Time.timeScale = 0.55f;
                InvokePrivateMethod(first, "Awake");
                InvokePrivateMethod(second, "Awake");
                first.Menu.SetVisible(true);
                second.Menu.SetVisible(true);

                InvokePrivateMethod(first, "OnDestroy");
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(Tolerance), "残る所有者を無視して時間を再開した");

                second.Menu.SetVisible(false);
                Assert.That(Time.timeScale, Is.EqualTo(0.55f).Within(Tolerance), "最後の所有者が閉じても時間倍率を戻していない");
            }
            finally
            {
                if (firstObject != null) UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                Time.timeScale = original;
            }
        }

        private static int FindPageIndex(DebugMenuRoot menu, string name)
        {
            for (var i = 0; i < menu.Pages.Count; i++)
            {
                if (menu.Pages[i].Name == name) return i;
            }

            return -1;
        }

        private static void InvokeUpdate(DebugMenuController controller)
        {
            InvokePrivateMethod(controller, "Update");
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"{methodName} が見つからない");
            method.Invoke(target, null);
        }

        private static void InvokePrivateStaticMethod(Assembly assembly, string typeName, string methodName)
        {
            var type = assembly.GetType(typeName);
            Assert.NotNull(type, $"{typeName} が見つからない");
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, $"{methodName} が見つからない");
            method.Invoke(null, null);
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"{fieldName} が見つからない");
            return (T)field.GetValue(target);
        }

        private static void WritePrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"{fieldName} が見つからない");
            field.SetValue(target, value);
        }
    }
}
