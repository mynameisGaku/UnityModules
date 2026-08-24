// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>Editor testが共有する有効stateとfake境界を作る。</summary>
    internal static class PlayerOptionsTestData
    {
        /// <summary>既定のfake runtimeと完全一致するtyped defaultを作る。</summary>
        internal static PlayerOptionsState CreateDefaultState()
        {
            return new PlayerOptionsState(
                new PlayerDisplayOptions(
                    1920,
                    1080,
                    FullScreenMode.Windowed,
                    CreateRefreshRate(60, 1)),
                60,
                0.75f,
                new PlayerQualityOptions(1, "High"));
        }

        /// <summary>指定値だけを持つstateを作る。</summary>
        internal static PlayerOptionsState CreateState(
            int width = 1920,
            int height = 1080,
            FullScreenMode fullScreenMode = FullScreenMode.Windowed,
            uint refreshNumerator = 60,
            uint refreshDenominator = 1,
            int targetFrameRate = 60,
            float masterVolume = 0.75f,
            int qualityIndex = 1,
            string qualityName = "High")
        {
            return new PlayerOptionsState(
                new PlayerDisplayOptions(
                    width,
                    height,
                    fullScreenMode,
                    CreateRefreshRate(refreshNumerator, refreshDenominator)),
                targetFrameRate,
                masterVolume,
                new PlayerQualityOptions(qualityIndex, qualityName));
        }

        /// <summary>Unity refresh rate値を作る。</summary>
        internal static RefreshRate CreateRefreshRate(uint numerator, uint denominator)
        {
            return new RefreshRate
            {
                numerator = numerator,
                denominator = denominator,
            };
        }

        /// <summary>fake runtimeへ渡す解像度候補を作る。</summary>
        internal static Resolution CreateResolution(
            int width,
            int height,
            uint refreshNumerator,
            uint refreshDenominator)
        {
            return new Resolution
            {
                width = width,
                height = height,
                refreshRateRatio = CreateRefreshRate(refreshNumerator, refreshDenominator),
            };
        }

        /// <summary>fake境界を使うserviceを作る。</summary>
        internal static PlayerOptionsService CreateService(
            FakePlayerOptionsRuntime runtime,
            FakePlayerOptionsStorage storage = null,
            PlayerOptionsState? defaults = null,
            PlayerOptionsMigrationPipeline migrations = null)
        {
            return new PlayerOptionsService(
                defaults ?? CreateDefaultState(),
                storage ?? new FakePlayerOptionsStorage(),
                runtime,
                migrations ?? PlayerOptionsMigrationPipeline.Default);
        }

        /// <summary>codec経由でcurrent schema文書を作る。</summary>
        internal static string Encode(PlayerOptionsState state)
        {
            var codec = new PlayerOptionsDocumentCodec(PlayerOptionsMigrationPipeline.Default);
            if (!codec.TryEncode(state, out var contents, out var message))
            {
                throw new InvalidOperationException(message);
            }

            return contents;
        }
    }

    /// <summary>先行regressionからfield diagnosticsを名前とbit値で厳格に確認する。</summary>
    internal static class PlayerOptionsResultAssertions
    {
        /// <summary>指定result propertyのfield flagsが期待名だけを含むことを確認する。</summary>
        internal static void AssertFields(
            PlayerOptionsResult result,
            string propertyName,
            params string[] expectedFieldNames)
        {
            var fieldType = typeof(PlayerOptionsService).Assembly.GetType(
                "PlayerOptions.PlayerOptionsField");
            Assert.That(fieldType, Is.Not.Null, "PlayerOptionsFieldを公開してください。");
            var property = typeof(PlayerOptionsResult).GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Result.{propertyName}を公開してください。");
            Assert.That(property.PropertyType, Is.EqualTo(fieldType));

            var expected = 0;
            for (var index = 0; index < expectedFieldNames.Length; index++)
            {
                expected |= Convert.ToInt32(Enum.Parse(fieldType, expectedFieldNames[index]));
            }

            var actual = Convert.ToInt32(property.GetValue(result));
            Assert.That(actual, Is.EqualTo(expected), propertyName);
        }
    }

    /// <summary>存在、raw値、例外、呼出し回数を決定論的に制御するstorage。</summary>
    internal sealed class FakePlayerOptionsStorage : IPlayerOptionsStorage
    {
        /// <summary>文書が存在する場合はtrue。</summary>
        internal bool Exists { get; set; }

        /// <summary>読込時に返し、成功書込時に置き換えるraw文書。</summary>
        internal string Contents { get; set; }

        /// <summary>TryReadから送出する例外。</summary>
        internal Exception ReadException { get; set; }

        /// <summary>Writeから送出する例外。</summary>
        internal Exception WriteException { get; set; }

        /// <summary>TryRead呼出し回数。</summary>
        internal int ReadCount { get; private set; }

        /// <summary>Write呼出し回数。</summary>
        internal int WriteCount { get; private set; }

        /// <inheritdoc/>
        public bool TryRead(out string contents)
        {
            ReadCount++;
            if (ReadException != null) throw ReadException;
            contents = Exists ? Contents : null;
            return Exists;
        }

        /// <inheritdoc/>
        public void Write(string contents)
        {
            WriteCount++;
            if (WriteException != null) throw WriteException;
            Contents = contents;
            Exists = true;
        }
    }

    /// <summary>Unity global値の観測、適用、失敗、call orderを決定論的に再現するruntime。</summary>
    internal sealed class FakePlayerOptionsRuntime : IPlayerOptionsRuntime
    {
        private Exception _runtimeReadException;

        /// <summary>有効な既定値を持つfake runtimeを作る。</summary>
        internal FakePlayerOptionsRuntime()
        {
            IsMainThreadValue = true;
            ScreenWidthValue = 1920;
            ScreenHeightValue = 1080;
            FullScreenModeValue = FullScreenMode.Windowed;
            CurrentRefreshRateValue = PlayerOptionsTestData.CreateRefreshRate(60, 1);
            ResolutionValues = Array.Empty<Resolution>();
            TargetFrameRateValue = 60;
            MasterVolumeValue = 0.75f;
            QualityLevelValue = 1;
            QualityNameValues = new[] { "Low", "High", "Ultra" };
            RenderFrameIntervalValue = 1;
        }

        /// <summary>setterと画面要求の順序。</summary>
        internal List<string> Calls { get; } = new List<string>();

        /// <summary>main-thread判定値。</summary>
        internal bool IsMainThreadValue { get; set; }

        /// <summary>main-thread判定で送出する例外。</summary>
        internal Exception MainThreadException { get; set; }

        /// <summary>runtime getter全般から送出する例外。</summary>
        internal Exception RuntimeReadException
        {
            get => _runtimeReadException;
            set => _runtimeReadException = value;
        }

        /// <summary>観測画面幅。</summary>
        internal int ScreenWidthValue { get; set; }

        /// <summary>観測画面高さ。</summary>
        internal int ScreenHeightValue { get; set; }

        /// <summary>観測画面mode。</summary>
        internal FullScreenMode FullScreenModeValue { get; set; }

        /// <summary>観測refresh rate。</summary>
        internal RefreshRate CurrentRefreshRateValue { get; set; }

        /// <summary>列挙解像度。</summary>
        internal Resolution[] ResolutionValues { get; set; }

        /// <summary>観測target frame rate。</summary>
        internal int TargetFrameRateValue { get; set; }

        /// <summary>観測master volume。</summary>
        internal float MasterVolumeValue { get; set; }

        /// <summary>観測quality index。</summary>
        internal int QualityLevelValue { get; set; }

        /// <summary>観測quality名一覧。</summary>
        internal string[] QualityNameValues { get; set; }

        /// <summary>観測vSync値。</summary>
        internal int VSyncCountValue { get; set; }

        /// <summary>観測render interval。</summary>
        internal int RenderFrameIntervalValue { get; set; }

        /// <summary>vSync観測だけを失敗させる。</summary>
        internal bool ThrowOnVSyncRead { get; set; }

        /// <summary>render interval観測だけを失敗させる。</summary>
        internal bool ThrowOnRenderFrameIntervalRead { get; set; }

        /// <summary>次のquality setter失敗回数。</summary>
        internal int QualitySetFailuresRemaining;

        /// <summary>指定回のquality setterだけを失敗させる。0は無効。</summary>
        internal int QualitySetFailureOnCall { get; set; }

        /// <summary>指定回のquality setterだけreadback値更新を抑止する。0は無効。</summary>
        internal int IgnoreQualitySetOnCall { get; set; }

        /// <summary>quality setter呼出し回数。</summary>
        internal int QualitySetCallCount { get; private set; }

        /// <summary>全quality setterを失敗させる。</summary>
        internal bool FailEveryQualitySet { get; set; }

        /// <summary>次のtarget setter失敗回数。</summary>
        internal int TargetSetFailuresRemaining;

        /// <summary>指定回のtarget setterだけを失敗させる。0は無効。</summary>
        internal int TargetSetFailureOnCall { get; set; }

        /// <summary>指定回のtarget setterだけreadback値更新を抑止する。0は無効。</summary>
        internal int IgnoreTargetSetOnCall { get; set; }

        /// <summary>target setter呼出し回数。</summary>
        internal int TargetSetCallCount { get; private set; }

        /// <summary>全target setterを失敗させる。</summary>
        internal bool FailEveryTargetSet { get; set; }

        /// <summary>次のvolume setter失敗回数。</summary>
        internal int VolumeSetFailuresRemaining;

        /// <summary>指定回のvolume setterだけを失敗させる。0は無効。</summary>
        internal int VolumeSetFailureOnCall { get; set; }

        /// <summary>指定回のvolume setterだけreadback値更新を抑止する。0は無効。</summary>
        internal int IgnoreVolumeSetOnCall { get; set; }

        /// <summary>volume setter呼出し回数。</summary>
        internal int VolumeSetCallCount { get; private set; }

        /// <summary>全volume setterを失敗させる。</summary>
        internal bool FailEveryVolumeSet { get; set; }

        /// <summary>次のresolution setter失敗回数。</summary>
        internal int ResolutionSetFailuresRemaining { get; set; }

        /// <summary>quality setterのreadback値更新を抑止する。</summary>
        internal bool IgnoreQualitySet { get; set; }

        /// <summary>target setterのreadback値更新を抑止する。</summary>
        internal bool IgnoreTargetSet { get; set; }

        /// <summary>volume setterのreadback値更新を抑止する。</summary>
        internal bool IgnoreVolumeSet { get; set; }

        /// <summary>最後の画面要求。</summary>
        internal PlayerDisplayOptions LastResolutionRequest { get; private set; }

        /// <summary>最後の画面要求がrefresh rate付きならtrue。</summary>
        internal bool LastResolutionSpecifiedRefreshRate { get; private set; }

        /// <summary>observer例外log回数。</summary>
        internal int ObserverLogCount { get; private set; }

        /// <summary>observer例外log自体を失敗させる。</summary>
        internal bool ThrowOnObserverLog { get; set; }

        /// <inheritdoc/>
        public bool IsMainThread
        {
            get
            {
                if (MainThreadException != null) throw MainThreadException;
                return IsMainThreadValue;
            }
        }

        /// <inheritdoc/>
        public int ScreenWidth
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return ScreenWidthValue;
            }
        }

        /// <inheritdoc/>
        public int ScreenHeight
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return ScreenHeightValue;
            }
        }

        /// <inheritdoc/>
        public FullScreenMode FullScreenMode
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return FullScreenModeValue;
            }
        }

        /// <inheritdoc/>
        public RefreshRate CurrentRefreshRate
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return CurrentRefreshRateValue;
            }
        }

        /// <inheritdoc/>
        public Resolution[] Resolutions
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return ResolutionValues;
            }
        }

        /// <inheritdoc/>
        public int TargetFrameRate
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return TargetFrameRateValue;
            }
        }

        /// <inheritdoc/>
        public float MasterVolume
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return MasterVolumeValue;
            }
        }

        /// <inheritdoc/>
        public int QualityLevel
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return QualityLevelValue;
            }
        }

        /// <inheritdoc/>
        public string[] QualityNames
        {
            get
            {
                ThrowIfRuntimeReadFails();
                return QualityNameValues;
            }
        }

        /// <inheritdoc/>
        public int VSyncCount
        {
            get
            {
                if (ThrowOnVSyncRead) throw new InvalidOperationException("vSync read failure");
                return VSyncCountValue;
            }
        }

        /// <inheritdoc/>
        public int RenderFrameInterval
        {
            get
            {
                if (ThrowOnRenderFrameIntervalRead)
                {
                    throw new InvalidOperationException("render interval read failure");
                }

                return RenderFrameIntervalValue;
            }
        }

        /// <inheritdoc/>
        public void SetQualityLevel(int levelIndex)
        {
            Calls.Add($"quality:{levelIndex}");
            QualitySetCallCount++;
            if (QualitySetCallCount == QualitySetFailureOnCall)
            {
                throw new InvalidOperationException("quality selected-call setter failure");
            }

            ThrowSetterFailure(ref QualitySetFailuresRemaining, FailEveryQualitySet, "quality");
            if (!IgnoreQualitySet && QualitySetCallCount != IgnoreQualitySetOnCall)
            {
                QualityLevelValue = levelIndex;
            }
        }

        /// <inheritdoc/>
        public void SetTargetFrameRate(int targetFrameRate)
        {
            Calls.Add($"target:{targetFrameRate}");
            TargetSetCallCount++;
            if (TargetSetCallCount == TargetSetFailureOnCall)
            {
                throw new InvalidOperationException("target selected-call setter failure");
            }

            ThrowSetterFailure(ref TargetSetFailuresRemaining, FailEveryTargetSet, "target");
            if (!IgnoreTargetSet && TargetSetCallCount != IgnoreTargetSetOnCall)
            {
                TargetFrameRateValue = targetFrameRate;
            }
        }

        /// <inheritdoc/>
        public void SetMasterVolume(float masterVolume)
        {
            Calls.Add($"volume:{masterVolume.ToString("R", CultureInfo.InvariantCulture)}");
            VolumeSetCallCount++;
            if (VolumeSetCallCount == VolumeSetFailureOnCall)
            {
                throw new InvalidOperationException("volume selected-call setter failure");
            }

            ThrowSetterFailure(ref VolumeSetFailuresRemaining, FailEveryVolumeSet, "volume");
            if (!IgnoreVolumeSet && VolumeSetCallCount != IgnoreVolumeSetOnCall)
            {
                MasterVolumeValue = masterVolume;
            }
        }

        /// <inheritdoc/>
        public void SetResolution(PlayerDisplayOptions display, bool specifyRefreshRate)
        {
            Calls.Add($"resolution:{display.Width}x{display.Height}:{specifyRefreshRate}");
            if (ResolutionSetFailuresRemaining > 0)
            {
                ResolutionSetFailuresRemaining--;
                throw new InvalidOperationException("resolution setter failure");
            }

            LastResolutionRequest = display;
            LastResolutionSpecifiedRefreshRate = specifyRefreshRate;
        }

        /// <inheritdoc/>
        public void LogObserverException(Exception exception)
        {
            ObserverLogCount++;
            if (ThrowOnObserverLog) throw new InvalidOperationException("observer log failure");
        }

        private void ThrowIfRuntimeReadFails()
        {
            if (_runtimeReadException != null) throw _runtimeReadException;
        }

        private static void ThrowSetterFailure(
            ref int failuresRemaining,
            bool failEveryCall,
            string settingName)
        {
            if (failEveryCall)
            {
                throw new InvalidOperationException($"{settingName} setter failure");
            }

            if (failuresRemaining <= 0) return;
            failuresRemaining--;
            throw new InvalidOperationException($"{settingName} setter failure");
        }
    }

    /// <summary>migration登録規則と呼出しを確認するfake。</summary>
    internal sealed class FakePlayerOptionsMigration : IPlayerOptionsDocumentMigration
    {
        /// <summary>変換元version。</summary>
        internal FakePlayerOptionsMigration(int sourceVersion, int targetVersion)
        {
            SourceVersion = sourceVersion;
            TargetVersion = targetVersion;
        }

        /// <inheritdoc/>
        public int SourceVersion { get; }

        /// <inheritdoc/>
        public int TargetVersion { get; }

        /// <summary>migration呼出し回数。</summary>
        internal int CallCount { get; private set; }

        /// <summary>migration callbackから送出する例外。</summary>
        internal Exception MigrationException { get; set; }

        /// <inheritdoc/>
        public bool TryMigrate(
            PlayerOptionsDocument source,
            out PlayerOptionsDocument migrated,
            out string message)
        {
            CallCount++;
            if (MigrationException != null) throw MigrationException;
            migrated = source;
            migrated.SchemaVersion = TargetVersion;
            message = string.Empty;
            return true;
        }
    }
}
