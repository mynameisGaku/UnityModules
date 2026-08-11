using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu.Tests
{
    public sealed class DebugMenuAppearanceGamepadTests
    {
        private const float Tolerance = 0.0001f;

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
