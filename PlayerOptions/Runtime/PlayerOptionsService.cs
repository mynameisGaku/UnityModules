// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>
    /// applicationが所有し、typed player optionの読込、変更、適用、保存を明示的に分離する。
    /// 全操作は作成時と同じUnity main threadから呼ぶ。
    /// </summary>
    public sealed class PlayerOptionsService
    {
        /// <summary>標準PlayerPrefs backendが使用するproject内key。</summary>
        public const string DefaultStorageKey = "com.studiogaku.player-options.document";

        private readonly object _observerLock = new object();
        private readonly HashSet<Action<PlayerOptionsState>> _failingObservers =
            new HashSet<Action<PlayerOptionsState>>();
        private readonly IPlayerOptionsStorage _storage;
        private readonly IPlayerOptionsRuntime _runtime;
        private readonly PlayerOptionsDocumentCodec _codec;
        private readonly PlayerOptionsState _defaults;

        private Action<PlayerOptionsState> _stateChanged;
        private PlayerOptionsState _state;
        private bool _isOperating;
        private bool _isDispatching;

        /// <summary>typed defaultと保存先を受け取り、application所有のserviceを作る。</summary>
        /// <param name="defaults">現在runtimeで利用できるtyped default。</param>
        /// <param name="storage">serviceより長く生存する同期storage。</param>
        /// <exception cref="ArgumentException">default値が不正または現在runtimeで利用できない。</exception>
        /// <exception cref="InvalidOperationException">main thread外、またはUnity runtimeを確認できない。</exception>
        public PlayerOptionsService(PlayerOptionsState defaults, IPlayerOptionsStorage storage)
            : this(
                defaults,
                storage,
                UnityPlayerOptionsRuntime.Instance,
                PlayerOptionsMigrationPipeline.Default)
        {
        }

        /// <summary>main thread上の現在Unity値をtyped defaultとして標準serviceを作る。</summary>
        /// <param name="storageKey">標準PlayerPrefs backendが使用する空白でないkey。</param>
        /// <returns>applicationが所有する新しいservice。</returns>
        /// <exception cref="ArgumentException">storage keyが不正。</exception>
        /// <exception cref="InvalidOperationException">main thread外、またはUnity runtimeを確認できない。</exception>
        public static PlayerOptionsService CreateDefault(string storageKey = DefaultStorageKey)
        {
            var runtime = UnityPlayerOptionsRuntime.Instance;
            EnsureMainThreadForConstruction(runtime);

            PlayerOptionsState defaults;
            try
            {
                defaults = CaptureDefaults(runtime);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"現在のUnity runtimeからtyped defaultを作れませんでした: {SafeMessage(exception)}",
                    exception);
            }

            return new PlayerOptionsService(
                defaults,
                new PlayerPrefsPlayerOptionsStorage(storageKey),
                runtime,
                PlayerOptionsMigrationPipeline.Default);
        }

        /// <summary>作成時に検証・正規化されたfallback用typed default。</summary>
        public PlayerOptionsState Defaults => _defaults;

        /// <summary>現在のin-memory option snapshot。LoadまたはSetStateだけが変更する。</summary>
        public PlayerOptionsState State => _state;

        /// <summary>
        /// LoadまたはSetStateがStateを実際に変更した後で通知する。
        /// 各購読先例外は他の購読先と操作結果へ伝播しない。
        /// </summary>
        public event Action<PlayerOptionsState> StateChanged
        {
            add
            {
                lock (_observerLock)
                {
                    _stateChanged += value;
                }
            }

            remove
            {
                lock (_observerLock)
                {
                    _stateChanged -= value;
                    if (value == null) return;

                    var removedObservers = value.GetInvocationList();
                    for (var index = 0; index < removedObservers.Length; index++)
                    {
                        _failingObservers.Remove((Action<PlayerOptionsState>)removedObservers[index]);
                    }
                }
            }
        }

        /// <summary>保存文書を読み、検証済みsnapshotだけをStateへ採用する。</summary>
        /// <returns>未存在、migration、fallback、破損、future schemaを区別する結果。</returns>
        public PlayerOptionsResult Load()
        {
            if (!TryBeginOperation(out var blocked)) return blocked;

            try
            {
                bool exists;
                string contents;
                try
                {
                    exists = _storage.TryRead(out contents);
                }
                catch (Exception exception)
                {
                    return PlayerOptionsResult.Failure(
                        _state,
                        PlayerOptionsError.StorageReadFailed,
                        $"保存先からplayer optionを読めませんでした: {SafeMessage(exception)}");
                }

                if (!exists)
                {
                    if (!TryValidateStrict(
                            _defaults,
                            PlayerOptionsError.RuntimeUnavailable,
                            out var normalizedDefaults,
                            out _,
                            out var defaultFailure))
                    {
                        return defaultFailure;
                    }

                    SetStateAndNotify(normalizedDefaults);
                    return PlayerOptionsResult.Success(
                        _state,
                        "保存値がないためtyped defaultを使用しました。",
                        usedDefaults: true);
                }

                if (!_codec.TryDecode(
                        contents,
                        out var loaded,
                        out var wasMigrated,
                        out var decodeError,
                        out var decodeMessage))
                {
                    return PlayerOptionsResult.Failure(_state, decodeError, decodeMessage);
                }

                PlayerOptionsState normalized;
                PlayerOptionsWarning warnings;
                bool usedDefaults;
                bool wasAdjusted;
                PlayerOptionsError validationError;
                string validationMessage;
                try
                {
                    if (!PlayerOptionsValidator.TryNormalizeLoaded(
                            loaded,
                            _defaults,
                            _runtime,
                            out normalized,
                            out warnings,
                            out usedDefaults,
                            out wasAdjusted,
                            out validationError,
                            out validationMessage))
                    {
                        return PlayerOptionsResult.Failure(
                            _state,
                            validationError,
                            validationMessage);
                    }
                }
                catch (Exception exception)
                {
                    return PlayerOptionsResult.Failure(
                        _state,
                        PlayerOptionsError.RuntimeUnavailable,
                        $"保存値を現在runtimeに照合できませんでした: {SafeMessage(exception)}");
                }

                var requiresSave = wasMigrated || wasAdjusted;
                SetStateAndNotify(normalized);
                return PlayerOptionsResult.Success(
                    _state,
                    requiresSave
                        ? "保存値を読み込み、現在schemaまたはruntimeへ補正しました。"
                        : "保存値を読み込みました。",
                    warnings,
                    usedDefaults,
                    requiresSave,
                    requiresSave);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>厳格に検証したsnapshotをStateへ設定する。Unity runtimeと保存先は変更しない。</summary>
        /// <param name="state">現在runtimeで利用できる完全なoption snapshot。</param>
        /// <returns>変更後状態または値の失敗理由。</returns>
        public PlayerOptionsResult SetState(PlayerOptionsState state)
        {
            if (!TryBeginOperation(out var blocked)) return blocked;

            try
            {
                if (!TryValidateStrict(
                        state,
                        PlayerOptionsError.InvalidOptions,
                        out var normalized,
                        out var warnings,
                        out var failure))
                {
                    return failure;
                }

                var changed = normalized != _state;
                SetStateAndNotify(normalized);
                return PlayerOptionsResult.Success(
                    _state,
                    changed ? "player optionのin-memory状態を変更しました。" : "player optionは変更されていません。",
                    warnings,
                    wasAdjusted: warnings != PlayerOptionsWarning.None);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>現在StateをUnity global optionへ適用する。Stateと保存先は変更しない。</summary>
        /// <returns>適用、画面要求、best-effort rollbackの結果。</returns>
        public PlayerOptionsResult Apply()
        {
            if (!TryBeginOperation(out var blocked)) return blocked;

            try
            {
                if (!TryValidateStrict(
                        _state,
                        PlayerOptionsError.InvalidOptions,
                        out var normalized,
                        out var validationWarnings,
                        out var failure))
                {
                    return failure;
                }

                try
                {
                    if (!PlayerOptionsRuntimeApplier.TryApply(
                            normalized,
                            _runtime,
                            out var applyError,
                            out var applyWarnings,
                            out var applyMessage,
                            out var affectedFields,
                            out var rollbackFailedFields,
                            out var outcomeUnknownFields))
                    {
                        return PlayerOptionsResult.Failure(
                            _state,
                            applyError,
                            applyMessage,
                            validationWarnings | applyWarnings,
                            affectedFields,
                            rollbackFailedFields,
                            outcomeUnknownFields);
                    }

                    return PlayerOptionsResult.Success(
                        _state,
                        applyMessage,
                        validationWarnings | applyWarnings,
                        wasAdjusted: validationWarnings != PlayerOptionsWarning.None,
                        affectedFields: affectedFields,
                        rollbackFailedFields: rollbackFailedFields,
                        outcomeUnknownFields: outcomeUnknownFields);
                }
                catch (Exception exception)
                {
                    return PlayerOptionsResult.Failure(
                        _state,
                        PlayerOptionsError.RuntimeUnavailable,
                        $"Unity runtimeへplayer optionを適用できませんでした: {SafeMessage(exception)}");
                }
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>現在Stateをcurrent schemaの一文書としてstorageへ同期保存する。</summary>
        /// <returns>検証、serialization、storage書込の結果。</returns>
        public PlayerOptionsResult Save()
        {
            if (!TryBeginOperation(out var blocked)) return blocked;

            try
            {
                if (!TryValidateStrict(
                        _state,
                        PlayerOptionsError.InvalidOptions,
                        out var normalized,
                        out var warnings,
                        out var failure))
                {
                    return failure;
                }

                if (!_codec.TryEncode(normalized, out var contents, out var serializationMessage))
                {
                    return PlayerOptionsResult.Failure(
                        _state,
                        PlayerOptionsError.SerializationFailed,
                        serializationMessage);
                }

                try
                {
                    _storage.Write(contents);
                }
                catch (Exception exception)
                {
                    return PlayerOptionsResult.Failure(
                        _state,
                        PlayerOptionsError.StorageWriteFailed,
                        $"保存先へplayer optionを書けませんでした: {SafeMessage(exception)}");
                }

                return PlayerOptionsResult.Success(
                    _state,
                    "player option文書をstorageへ書き込みました。",
                    warnings,
                    wasAdjusted: warnings != PlayerOptionsWarning.None);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>test runtimeとmigration seamを使って同じservice契約を作る。</summary>
        internal PlayerOptionsService(
            PlayerOptionsState defaults,
            IPlayerOptionsStorage storage,
            IPlayerOptionsRuntime runtime,
            PlayerOptionsMigrationPipeline migrations)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _codec = new PlayerOptionsDocumentCodec(
                migrations ?? throw new ArgumentNullException(nameof(migrations)));

            EnsureMainThreadForConstruction(runtime);
            try
            {
                if (!PlayerOptionsValidator.TryNormalizeStrict(
                        defaults,
                        runtime,
                        out var normalizedDefaults,
                        out _,
                        out var message))
                {
                    throw new ArgumentException($"typed defaultが不正です: {message}", nameof(defaults));
                }

                _defaults = normalizedDefaults;
                _state = normalizedDefaults;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"typed defaultをUnity runtimeに照合できませんでした: {SafeMessage(exception)}",
                    exception);
            }
        }

        private bool TryBeginOperation(out PlayerOptionsResult failure)
        {
            try
            {
                if (!_runtime.IsMainThread)
                {
                    failure = PlayerOptionsResult.Failure(
                        _state,
                        PlayerOptionsError.MainThreadRequired,
                        "player option操作はUnity main threadから呼んでください。");
                    return false;
                }
            }
            catch (Exception exception)
            {
                failure = PlayerOptionsResult.Failure(
                    _state,
                    PlayerOptionsError.RuntimeUnavailable,
                    $"Unity main threadを確認できませんでした: {SafeMessage(exception)}");
                return false;
            }

            if (_isOperating || _isDispatching)
            {
                failure = PlayerOptionsResult.Failure(
                    _state,
                    PlayerOptionsError.Busy,
                    "別のplayer option操作または変更通知を処理中です。");
                return false;
            }

            _isOperating = true;
            failure = default;
            return true;
        }

        private void EndOperation() => _isOperating = false;

        private bool TryValidateStrict(
            PlayerOptionsState state,
            PlayerOptionsError invalidError,
            out PlayerOptionsState normalized,
            out PlayerOptionsWarning warnings,
            out PlayerOptionsResult failure)
        {
            try
            {
                if (!PlayerOptionsValidator.TryNormalizeStrict(
                        state,
                        _runtime,
                        out normalized,
                        out warnings,
                        out var message))
                {
                    failure = PlayerOptionsResult.Failure(_state, invalidError, message);
                    return false;
                }

                failure = default;
                return true;
            }
            catch (Exception exception)
            {
                normalized = state;
                warnings = PlayerOptionsWarning.None;
                failure = PlayerOptionsResult.Failure(
                    _state,
                    PlayerOptionsError.RuntimeUnavailable,
                    $"player optionを現在runtimeに照合できませんでした: {SafeMessage(exception)}");
                return false;
            }
        }

        private void SetStateAndNotify(PlayerOptionsState next)
        {
            if (_state == next) return;

            _state = next;
            NotifyStateChanged(next);
        }

        private void NotifyStateChanged(PlayerOptionsState state)
        {
            Action<PlayerOptionsState> observers;
            lock (_observerLock)
            {
                observers = _stateChanged;
            }

            if (observers == null) return;

            _isDispatching = true;
            try
            {
                var invocationList = observers.GetInvocationList();
                for (var index = 0; index < invocationList.Length; index++)
                {
                    var observer = (Action<PlayerOptionsState>)invocationList[index];
                    try
                    {
                        observer(state);
                        MarkObserverHealthy(observer);
                    }
                    catch (Exception exception)
                    {
                        if (MarkObserverFailed(observer)) LogObserverException(exception);
                    }
                }
            }
            finally
            {
                _isDispatching = false;
            }
        }

        private void MarkObserverHealthy(Action<PlayerOptionsState> observer)
        {
            lock (_observerLock)
            {
                _failingObservers.Remove(observer);
            }
        }

        private bool MarkObserverFailed(Action<PlayerOptionsState> observer)
        {
            lock (_observerLock)
            {
                var currentObservers = _stateChanged;
                if (currentObservers == null) return true;

                var invocationList = currentObservers.GetInvocationList();
                for (var index = 0; index < invocationList.Length; index++)
                {
                    if (invocationList[index].Equals(observer))
                    {
                        return _failingObservers.Add(observer);
                    }
                }

                return true;
            }
        }

        private void LogObserverException(Exception exception)
        {
            try
            {
                _runtime.LogObserverException(exception);
            }
            catch (Exception)
            {
                // observer isolationを守るため、診断先の失敗も操作結果へ伝播させない。
            }
        }

        private static PlayerOptionsState CaptureDefaults(IPlayerOptionsRuntime runtime)
        {
            var currentRefreshRate = runtime.CurrentRefreshRate;
            if (currentRefreshRate.numerator == 0 || currentRefreshRate.denominator == 0)
            {
                currentRefreshRate = default;
            }

            var qualityNames = runtime.QualityNames;
            var qualityLevel = runtime.QualityLevel;
            if (qualityNames == null ||
                qualityLevel < 0 ||
                qualityLevel >= qualityNames.Length ||
                string.IsNullOrEmpty(qualityNames[qualityLevel]))
            {
                throw new InvalidOperationException("現在の品質levelをindexと名前の組として取得できません。");
            }

            var display = new PlayerDisplayOptions(
                runtime.ScreenWidth,
                runtime.ScreenHeight,
                runtime.FullScreenMode,
                currentRefreshRate);
            var quality = new PlayerQualityOptions(qualityLevel, qualityNames[qualityLevel]);
            return new PlayerOptionsState(
                display,
                runtime.TargetFrameRate,
                runtime.MasterVolume,
                quality);
        }

        private static void EnsureMainThreadForConstruction(IPlayerOptionsRuntime runtime)
        {
            bool isMainThread;
            try
            {
                isMainThread = runtime.IsMainThread;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Unity main threadを確認できませんでした: {SafeMessage(exception)}",
                    exception);
            }

            if (!isMainThread)
            {
                throw new InvalidOperationException(
                    "PlayerOptionsServiceはUnity main thread上で作成してください。");
            }
        }

        private static string SafeMessage(Exception exception)
        {
            var safeMessage = string.IsNullOrWhiteSpace(exception?.Message)
                ? exception?.GetType().Name ?? "Unknown error"
                : exception.Message;
            return safeMessage.Length <= 1024 ? safeMessage : safeMessage.Substring(0, 1024);
        }
    }
}
