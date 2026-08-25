// SPDX-License-Identifier: MIT

#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;
#endif

namespace Haptics
{
    /// <summary>
    /// Android VibratorをJNI経由で使うdriver。API 26以降はVibrationEffect.createWaveformを試し、
    /// 不可ならVibrator.Vibrate(duration)へ劣化する。初期化に失敗した場合もcapability Noneとして安全に動作する。
    /// </summary>
    public sealed class UnityAndroidHapticsDriver : IHapticsDriver
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const int WaveformApiLevel = 26;

        private readonly AndroidJavaObject _vibrator;
        private readonly HapticsCapability _capability;
#endif

        /// <summary>現在端末のVibratorを取得し、hasVibrator/hasAmplitudeControlからcapabilityを決定する。</summary>
        public UnityAndroidHapticsDriver()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    var vibrator = ResolveSystemVibrator(activity);
                    if (vibrator == null)
                    {
                        Debug.LogWarning("[Haptics] Android Vibratorを取得できませんでした。capability Noneで動作します。");
                        return;
                    }

                    bool hasVibrator;
                    try
                    {
                        hasVibrator = vibrator.Call<bool>("hasVibrator");
                    }
                    catch (Exception)
                    {
                        hasVibrator = true;
                    }

                    bool hasAmplitudeControl;
                    try
                    {
                        hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                    }
                    catch (Exception)
                    {
                        hasAmplitudeControl = false;
                    }

                    int apiLevel;
                    try
                    {
                        using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            apiLevel = versionClass.GetStatic<int>("SDK_INT");
                        }
                    }
                    catch (Exception)
                    {
                        apiLevel = 0;
                    }

                    _vibrator = vibrator;
                    _capability = HapticsCapability.Vibrate |
                                  (hasAmplitudeControl
                                      ? HapticsCapability.AmplitudeControl
                                      : HapticsCapability.None) |
                                  (apiLevel >= WaveformApiLevel
                                      ? HapticsCapability.PatternWaveform
                                      : HapticsCapability.None);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Haptics] Android Vibratorの初期化に失敗しました。capability Noneで動作します: {exception.Message}");
            }
#endif
        }

        /// <summary>起動時に決定した端末capability。初期化失敗時はNone。</summary>
        public HapticsCapability Capability
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _capability;
#else
                return HapticsCapability.None;
#endif
            }
        }

        /// <summary>patternを波形または単発durationで振動させる。</summary>
        /// <param name="pattern">再生する検証済みpattern。</param>
        /// <returns>振動要求を受理した場合はtrue。</returns>
        public bool TryVibrate(HapticsPattern pattern)
        {
            if (pattern == null) return false;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null) return false;

            var steps = pattern.Steps;
            var amplitudes = new int[steps.Count];
            var durations = new long[steps.Count];
            var totalMilliseconds = 0;
            for (var index = 0; index < steps.Count; index++)
            {
                amplitudes[index] = Mathf.Clamp(
                    Mathf.RoundToInt(steps[index].Amplitude * 255f), 0, 255);
                durations[index] = steps[index].DurationMilliseconds;
                totalMilliseconds += steps[index].DurationMilliseconds;
            }

            try
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                           "createWaveform", amplitudes, durations, -1))
                {
                    _vibrator.Call("vibrate", effect);
                }

                return true;
            }
            catch (Exception)
            {
                try
                {
                    _vibrator.Call("vibrate", (long)totalMilliseconds);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
#else
            return false;
#endif
        }

        /// <summary>JNI global参照を解放する。service Dispose時に呼ばれる。</summary>
        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _vibrator?.Dispose();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject ResolveSystemVibrator(AndroidJavaObject activity)
        {
            try
            {
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                {
                    return manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                }
            }
            catch (Exception)
            {
                return activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
        }
#endif
    }
}
