// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlayerOptions.Samples
{
    /// <summary>application ownerとしてserviceを保持し、Load、Set、Apply、Saveを別操作で示す。</summary>
    [AddComponentMenu("StudioGaku/Player Options Basics Controller")]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlayerOptionsBasicsController : MonoBehaviour
    {
        public const string ReadyElementName = "player-options-basics-ready";
        public const string WidthFieldName = "player-options-width";
        public const string HeightFieldName = "player-options-height";
        public const string FullScreenModeFieldName = "player-options-full-screen-mode";
        public const string RefreshNumeratorFieldName = "player-options-refresh-numerator";
        public const string RefreshDenominatorFieldName = "player-options-refresh-denominator";
        public const string TargetFrameRateFieldName = "player-options-target-frame-rate";
        public const string MasterVolumeFieldName = "player-options-master-volume";
        public const string QualityFieldName = "player-options-quality";
        public const string LoadButtonName = "player-options-load";
        public const string SetButtonName = "player-options-set";
        public const string ApplyButtonName = "player-options-apply";
        public const string SaveButtonName = "player-options-save";
        public const string StateLabelName = "player-options-state";
        public const string ResultLabelName = "player-options-result";
        public const string WarningLabelName = "player-options-warning";
        public const string ErrorLabelName = "player-options-error";

        [SerializeField]
        [Tooltip("このsampleが使用するPlayerPrefs key。空の場合はmodule標準keyを使います。")]
        private string _storageKey = PlayerOptionsService.DefaultStorageKey;

        private PlayerOptionsService _service;
        private IntegerField _widthField;
        private IntegerField _heightField;
        private DropdownField _fullScreenModeField;
        private LongField _refreshNumeratorField;
        private LongField _refreshDenominatorField;
        private IntegerField _targetFrameRateField;
        private Slider _masterVolumeField;
        private DropdownField _qualityField;
        private Button _loadButton;
        private Button _setButton;
        private Button _applyButton;
        private Button _saveButton;
        private Label _stateLabel;
        private Label _resultLabel;
        private Label _warningLabel;
        private Label _errorLabel;
        private bool _isReady;

        /// <summary>UIとapplication-owned serviceの初期化が完了した場合はtrue。</summary>
        public bool IsReady => _isReady;

        /// <summary>UIDocumentをbindし、serviceを生成してLoad成功時だけApplyする。</summary>
        private void OnEnable()
        {
            _isReady = false;
            var document = GetComponent<UIDocument>();
            var root = document?.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[Player Options Basics] UIDocumentのrootを取得できません。", this);
                enabled = false;
                return;
            }

            if (root.Q<VisualElement>(ReadyElementName) == null) BuildFallbackView(root);
            if (!TryBindView(root))
            {
                Debug.LogError("[Player Options Basics] 必要なUI elementが見つかりません。", this);
                enabled = false;
                return;
            }

            BindButtons();
            try
            {
                var key = string.IsNullOrWhiteSpace(_storageKey)
                    ? PlayerOptionsService.DefaultStorageKey
                    : _storageKey;
                _service = PlayerOptionsService.CreateDefault(key);
                _service.StateChanged += HandleStateChanged;
                PopulateState(_service.State);
                _isReady = true;

                var load = _service.Load();
                if (!load.IsSuccess)
                {
                    PresentResult("Startup Load", load);
                    return;
                }

                PopulateState(load.State);
                var apply = _service.Apply();
                PresentStartup(load, apply);
            }
            catch (Exception exception)
            {
                _isReady = false;
                PresentLocalFailure("Startup", exception.Message);
                SetOperationButtonsEnabled(false);
                Debug.LogException(exception, this);
            }
        }

        /// <summary>購読とButton callbackを解除し、scene再有効化時の重複操作を防ぐ。</summary>
        private void OnDisable()
        {
            if (_service != null) _service.StateChanged -= HandleStateChanged;
            UnbindButtons();
            _service = null;
            _isReady = false;
        }

        /// <summary>UXMLまたはtest fallbackから必要なcontrolを安定名で取得する。</summary>
        private bool TryBindView(VisualElement root)
        {
            _widthField = root.Q<IntegerField>(WidthFieldName);
            _heightField = root.Q<IntegerField>(HeightFieldName);
            _fullScreenModeField = root.Q<DropdownField>(FullScreenModeFieldName);
            _refreshNumeratorField = root.Q<LongField>(RefreshNumeratorFieldName);
            _refreshDenominatorField = root.Q<LongField>(RefreshDenominatorFieldName);
            _targetFrameRateField = root.Q<IntegerField>(TargetFrameRateFieldName);
            _masterVolumeField = root.Q<Slider>(MasterVolumeFieldName);
            _qualityField = root.Q<DropdownField>(QualityFieldName);
            _loadButton = root.Q<Button>(LoadButtonName);
            _setButton = root.Q<Button>(SetButtonName);
            _applyButton = root.Q<Button>(ApplyButtonName);
            _saveButton = root.Q<Button>(SaveButtonName);
            _stateLabel = root.Q<Label>(StateLabelName);
            _resultLabel = root.Q<Label>(ResultLabelName);
            _warningLabel = root.Q<Label>(WarningLabelName);
            _errorLabel = root.Q<Label>(ErrorLabelName);

            var complete = _widthField != null &&
                           _heightField != null &&
                           _fullScreenModeField != null &&
                           _refreshNumeratorField != null &&
                           _refreshDenominatorField != null &&
                           _targetFrameRateField != null &&
                           _masterVolumeField != null &&
                           _qualityField != null &&
                           _loadButton != null &&
                           _setButton != null &&
                           _applyButton != null &&
                           _saveButton != null &&
                           _stateLabel != null &&
                           _resultLabel != null &&
                           _warningLabel != null &&
                           _errorLabel != null;
            if (!complete) return false;

            _fullScreenModeField.choices = new List<string>(Enum.GetNames(typeof(FullScreenMode)));
            RefreshQualityChoices();
            return true;
        }

        /// <summary>実UXMLを使わないPlayMode testでも同じ操作contractを検証できる最小viewを作る。</summary>
        private static void BuildFallbackView(VisualElement root)
        {
            var ready = new VisualElement { name = ReadyElementName };
            root.Add(ready);
            ready.Add(new IntegerField("Width") { name = WidthFieldName });
            ready.Add(new IntegerField("Height") { name = HeightFieldName });
            ready.Add(new DropdownField("Window Mode") { name = FullScreenModeFieldName });
            ready.Add(new LongField("Refresh Numerator") { name = RefreshNumeratorFieldName });
            ready.Add(new LongField("Refresh Denominator") { name = RefreshDenominatorFieldName });
            ready.Add(new IntegerField("Target Frame Rate") { name = TargetFrameRateFieldName });
            ready.Add(new Slider("Master Volume", 0f, 1f) { name = MasterVolumeFieldName });
            ready.Add(new DropdownField("Quality") { name = QualityFieldName });
            ready.Add(new Button { name = LoadButtonName, text = "Load" });
            ready.Add(new Button { name = SetButtonName, text = "Set State" });
            ready.Add(new Button { name = ApplyButtonName, text = "Apply" });
            ready.Add(new Button { name = SaveButtonName, text = "Save" });
            ready.Add(new Label { name = StateLabelName });
            ready.Add(new Label { name = ResultLabelName });
            ready.Add(new Label { name = WarningLabelName });
            ready.Add(new Label { name = ErrorLabelName });
        }

        private void BindButtons()
        {
            _loadButton.clicked += HandleLoadClicked;
            _setButton.clicked += HandleSetClicked;
            _applyButton.clicked += HandleApplyClicked;
            _saveButton.clicked += HandleSaveClicked;
        }

        private void UnbindButtons()
        {
            if (_loadButton != null) _loadButton.clicked -= HandleLoadClicked;
            if (_setButton != null) _setButton.clicked -= HandleSetClicked;
            if (_applyButton != null) _applyButton.clicked -= HandleApplyClicked;
            if (_saveButton != null) _saveButton.clicked -= HandleSaveClicked;
        }

        private void HandleLoadClicked()
        {
            if (_service == null) return;
            var result = _service.Load();
            if (result.IsSuccess) PopulateState(result.State);
            PresentResult("Load", result);
        }

        private void HandleSetClicked()
        {
            if (_service == null) return;
            if (!TryReadState(out var state, out var message))
            {
                PresentLocalFailure("SetState", message);
                return;
            }

            var result = _service.SetState(state);
            if (result.IsSuccess) PopulateState(result.State);
            PresentResult("SetState", result);
        }

        private void HandleApplyClicked()
        {
            if (_service == null) return;
            PresentResult("Apply", _service.Apply());
        }

        private void HandleSaveClicked()
        {
            if (_service == null) return;
            PresentResult("Save", _service.Save());
        }

        private void HandleStateChanged(PlayerOptionsState state)
        {
            PopulateState(state);
        }

        /// <summary>UI値からstrict validationへ渡す完全なstateを作る。</summary>
        private bool TryReadState(out PlayerOptionsState state, out string message)
        {
            state = default;
            message = string.Empty;
            if (!Enum.TryParse(_fullScreenModeField.value, out FullScreenMode fullScreenMode) ||
                !Enum.IsDefined(typeof(FullScreenMode), fullScreenMode))
            {
                message = "window modeを選択してください。";
                return false;
            }

            var numerator = _refreshNumeratorField.value;
            var denominator = _refreshDenominatorField.value;
            if (numerator < 0 || numerator > uint.MaxValue ||
                denominator < 0 || denominator > uint.MaxValue)
            {
                message = "refresh rateの分子と分母は0以上uint上限以下にしてください。";
                return false;
            }

            var qualityIndex = _qualityField.index;
            var qualityName = _qualityField.value;
            if (qualityIndex < 0 || string.IsNullOrEmpty(qualityName))
            {
                message = "qualityを選択してください。";
                return false;
            }

            var refreshRate = new RefreshRate
            {
                numerator = (uint)numerator,
                denominator = (uint)denominator,
            };
            var display = new PlayerDisplayOptions(
                _widthField.value,
                _heightField.value,
                fullScreenMode,
                refreshRate);
            var quality = new PlayerQualityOptions(qualityIndex, qualityName);
            state = new PlayerOptionsState(
                display,
                _targetFrameRateField.value,
                _masterVolumeField.value,
                quality);
            return true;
        }

        /// <summary>service stateを全inputと読み取り専用statusへ同期する。</summary>
        private void PopulateState(PlayerOptionsState state)
        {
            RefreshQualityChoices();
            _widthField?.SetValueWithoutNotify(state.Display.Width);
            _heightField?.SetValueWithoutNotify(state.Display.Height);
            _fullScreenModeField?.SetValueWithoutNotify(state.Display.FullScreenMode.ToString());
            _refreshNumeratorField?.SetValueWithoutNotify(state.Display.PreferredRefreshRate.numerator);
            _refreshDenominatorField?.SetValueWithoutNotify(state.Display.PreferredRefreshRate.denominator);
            _targetFrameRateField?.SetValueWithoutNotify(state.TargetFrameRate);
            _masterVolumeField?.SetValueWithoutNotify(state.MasterVolume);
            if (_qualityField != null &&
                state.Quality.LevelIndex >= 0 &&
                state.Quality.LevelIndex < _qualityField.choices.Count)
            {
                _qualityField.index = state.Quality.LevelIndex;
            }

            if (_stateLabel != null)
            {
                var refresh = state.Display.PreferredRefreshRate;
                _stateLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "State: {0}x{1} {2}, refresh {3}/{4}, target {5}, volume {6:F2}, quality {7}:{8}",
                    state.Display.Width,
                    state.Display.Height,
                    state.Display.FullScreenMode,
                    refresh.numerator,
                    refresh.denominator,
                    state.TargetFrameRate,
                    state.MasterVolume,
                    state.Quality.LevelIndex,
                    state.Quality.LevelName);
            }
        }

        private void RefreshQualityChoices()
        {
            if (_qualityField == null) return;
            _qualityField.choices = new List<string>(QualitySettings.names ?? Array.Empty<string>());
        }

        private void PresentStartup(PlayerOptionsResult load, PlayerOptionsResult apply)
        {
            var warnings = load.Warnings | apply.Warnings;
            _resultLabel.text = string.Format(
                CultureInfo.InvariantCulture,
                "Startup Load → Apply: Load={0}, Apply={1}, UsedDefaults={2}, WasAdjusted={3}, RequiresSave={4}, AffectedFields={5}, RollbackFailedFields={6}, OutcomeUnknownFields={7}",
                load.IsSuccess ? "Success" : "Failure",
                apply.IsSuccess ? "Success" : "Failure",
                load.UsedDefaults,
                load.WasAdjusted,
                load.RequiresSave,
                apply.AffectedFields,
                apply.RollbackFailedFields,
                apply.OutcomeUnknownFields);
            _warningLabel.text = $"Warnings: {(warnings == PlayerOptionsWarning.None ? "None" : warnings.ToString())}";
            _errorLabel.text = apply.IsSuccess
                ? "Error: None"
                : $"Error: {apply.Error} — {apply.Message}";
            PopulateState(apply.State);
        }

        private void PresentResult(string operation, PlayerOptionsResult result)
        {
            _resultLabel.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1}, UsedDefaults={2}, WasAdjusted={3}, RequiresSave={4}, AffectedFields={5}, RollbackFailedFields={6}, OutcomeUnknownFields={7}",
                operation,
                result.IsSuccess ? "Success" : "Failure",
                result.UsedDefaults,
                result.WasAdjusted,
                result.RequiresSave,
                result.AffectedFields,
                result.RollbackFailedFields,
                result.OutcomeUnknownFields);
            _warningLabel.text = $"Warnings: {(result.Warnings == PlayerOptionsWarning.None ? "None" : result.Warnings.ToString())}";
            _errorLabel.text = result.IsSuccess
                ? "Error: None"
                : $"Error: {result.Error} — {result.Message}";
            PopulateState(result.State);
        }

        private void PresentLocalFailure(string operation, string message)
        {
            if (_resultLabel != null) _resultLabel.text = $"{operation}: Failure";
            if (_warningLabel != null) _warningLabel.text = "Warnings: None";
            if (_errorLabel != null) _errorLabel.text = $"Error: InvalidOptions — {message}";
        }

        private void SetOperationButtonsEnabled(bool enabledState)
        {
            _loadButton?.SetEnabled(enabledState);
            _setButton?.SetEnabled(enabledState);
            _applyButton?.SetEnabled(enabledState);
            _saveButton?.SetEnabled(enabledState);
        }
    }
}
