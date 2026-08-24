// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PlayerOptions.Samples.Runtime.Tests
{
    /// <summary>sampleの実ButtonがLoad、Set、Saveを分離し、test所有keyだけへ保存することを確かめる。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class PlayerOptionsBasicsButtonTests
    {
        private GameObject _host;
        private UIDocument _document;
        private PlayerOptionsBasicsController _sample;
        private PanelSettings _panelSettings;
        private ThemeStyleSheet _themeStyleSheet;
        private RenderTexture _targetTexture;
        private string _storageKey;
        private int _originalTargetFrameRate;
        private float _originalMasterVolume;
        private int _originalQualityLevel;

        /// <summary>test専用PlayerPrefs keyと実panelを作り、sampleのstartup Load→Applyを待つ。</summary>
        [UnitySetUp]
        public IEnumerator CreateSampleView()
        {
            _storageKey = $"com.studiogaku.player-options.sample-tests.{Guid.NewGuid():N}";
            PlayerPrefs.DeleteKey(_storageKey);
            _originalTargetFrameRate = Application.targetFrameRate;
            _originalMasterVolume = AudioListener.volume;
            _originalQualityLevel = QualitySettings.GetQualityLevel();

            _targetTexture = new RenderTexture(960, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "Player Options Basics Tests Target",
            };
            Assert.That(_targetTexture.Create(), Is.True, "PlayMode検証用RenderTextureを作れません。");
            _themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            _themeStyleSheet.name = "Player Options Basics Tests Theme";
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.targetTexture = _targetTexture;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.themeStyleSheet = _themeStyleSheet;

            _host = new GameObject("Player Options Basics Tests");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<PlayerOptionsBasicsController>();
            SetStorageKey(_sample, _storageKey);
            _host.SetActive(true);
            _document.rootVisualElement.style.width = 960f;
            _document.rootVisualElement.style.height = 720f;

            yield return WaitUntil(
                () => _sample.IsReady && Find<VisualElement>(PlayerOptionsBasicsController.ReadyElementName) != null,
                3d,
                "Player Options Basicsが3秒以内に準備されませんでした。");
        }

        /// <summary>test所有object、global値、PlayerPrefs keyを復元・削除する。</summary>
        [UnityTearDown]
        public IEnumerator DestroySampleView()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            yield return null;

            Application.targetFrameRate = _originalTargetFrameRate;
            AudioListener.volume = _originalMasterVolume;
            if (_originalQualityLevel >= 0 && _originalQualityLevel < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(_originalQualityLevel, true);
            }

            if (!string.IsNullOrEmpty(_storageKey))
            {
                PlayerPrefs.DeleteKey(_storageKey);
                PlayerPrefs.Save();
            }

            if (_panelSettings != null) UnityEngine.Object.DestroyImmediate(_panelSettings);
            if (_themeStyleSheet != null) UnityEngine.Object.DestroyImmediate(_themeStyleSheet);
            if (_targetTexture != null)
            {
                _targetTexture.Release();
                UnityEngine.Object.DestroyImmediate(_targetTexture);
            }

            _host = null;
            _document = null;
            _sample = null;
            _panelSettings = null;
            _themeStyleSheet = null;
            _targetTexture = null;
            _storageKey = null;
        }

        /// <summary>4つの操作Buttonとstate、warning、error表示が安定名で存在する。</summary>
        [UnityTest]
        public IEnumerator ReadyView_ContainsSeparateOperationsAndResultLabels()
        {
            Assert.That(Find<Button>(PlayerOptionsBasicsController.LoadButtonName), Is.Not.Null);
            Assert.That(Find<Button>(PlayerOptionsBasicsController.SetButtonName), Is.Not.Null);
            Assert.That(Find<Button>(PlayerOptionsBasicsController.ApplyButtonName), Is.Not.Null);
            Assert.That(Find<Button>(PlayerOptionsBasicsController.SaveButtonName), Is.Not.Null);
            Assert.That(Find<Label>(PlayerOptionsBasicsController.StateLabelName)?.text, Does.StartWith("State:"));
            var result = Find<Label>(PlayerOptionsBasicsController.ResultLabelName)?.text;
            Assert.That(result, Does.Contain("Startup Load → Apply"));
            Assert.That(result, Does.Contain("AffectedFields="));
            Assert.That(result, Does.Contain("RollbackFailedFields="));
            Assert.That(result, Does.Contain("OutcomeUnknownFields="));
            Assert.That(Find<Label>(PlayerOptionsBasicsController.WarningLabelName)?.text, Does.StartWith("Warnings:"));
            Assert.That(Find<Label>(PlayerOptionsBasicsController.ErrorLabelName)?.text, Does.StartWith("Error:"));
            yield break;
        }

        /// <summary>Set Stateはglobal volumeへ触れず、Saveだけがtest keyへ書き、Loadで再読込できる。</summary>
        [UnityTest]
        public IEnumerator SetSaveLoad_KeepOperationsSeparate_AndUseOwnedStorageKey()
        {
            var volume = Find<Slider>(PlayerOptionsBasicsController.MasterVolumeFieldName);
            var changedVolume = _originalMasterVolume >= 0.5f ? 0.25f : 0.75f;
            volume.SetValueWithoutNotify(changedVolume);

            InvokeBoundClick(Find<Button>(PlayerOptionsBasicsController.SetButtonName));
            Assert.That(Find<Label>(PlayerOptionsBasicsController.ResultLabelName).text, Does.StartWith("SetState: Success"));
            Assert.That(AudioListener.volume, Is.EqualTo(_originalMasterVolume).Within(0.0001f), "SetStateがmaster volumeをUnityへ適用しました。");
            Assert.That(PlayerPrefs.HasKey(_storageKey), Is.False, "SetStateがPlayerPrefsへ書きました。");

            InvokeBoundClick(Find<Button>(PlayerOptionsBasicsController.SaveButtonName));
            Assert.That(Find<Label>(PlayerOptionsBasicsController.ResultLabelName).text, Does.StartWith("Save: Success"));
            Assert.That(PlayerPrefs.HasKey(_storageKey), Is.True, "Saveがtest所有keyへ書きませんでした。");
            Assert.That(AudioListener.volume, Is.EqualTo(_originalMasterVolume).Within(0.0001f), "Saveがmaster volumeをUnityへ適用しました。");

            InvokeBoundClick(Find<Button>(PlayerOptionsBasicsController.LoadButtonName));
            Assert.That(Find<Label>(PlayerOptionsBasicsController.ResultLabelName).text, Does.StartWith("Load: Success"));
            Assert.That(volume.value, Is.EqualTo(changedVolume).Within(0.0001f));
            yield break;
        }

        private T Find<T>(string name) where T : VisualElement => _document?.rootVisualElement?.Q<T>(name);

        private static void SetStorageKey(PlayerOptionsBasicsController sample, string storageKey)
        {
            var field = typeof(PlayerOptionsBasicsController).GetField("_storageKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "sampleのtest用storage seamを取得できません。");
            field.SetValue(sample, storageKey);
        }

        private static void InvokeBoundClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, $"{button.name}が無効です。");
            var invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(EventBase) },
                null);
            Assert.That(invoke, Is.Not.Null, "UI ToolkitのButton callback入口を取得できません。");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private static IEnumerator WaitUntil(Func<bool> condition, double timeoutSeconds, string failureMessage)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(failureMessage);
                yield return null;
            }
        }
    }
}
