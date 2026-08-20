using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace AudioControl
{
    /// <summary>所有するAudioSource poolへ再生要求を直列に割り当て、voice上限とfadeを管理します。</summary>
    [AddComponentMenu("Audio/Audio Control Controller")]
    [DisallowMultipleComponent]
    public sealed class AudioControlController : MonoBehaviour
    {
        /// <summary>設定できる最小voice数です。</summary>
        public const int MinimumVoiceLimit = 1;

        /// <summary>設定できる最大voice数です。</summary>
        public const int MaximumVoiceLimit = 32;

        [SerializeField]
        [Range(MinimumVoiceLimit, MaximumVoiceLimit)]
        private int _voiceLimit = 8;

        private readonly List<AudioVoice> _voices = new List<AudioVoice>();
        private readonly List<long> _pendingReleases = new List<long>();
        private AudioControlGeneration _generation;
        private int _ownerThreadId;
        private int _available;
        private bool _applicationExiting;
        private long _nextVoiceId;
        private long _nextSequence;
        private int _activeVoiceCount;

        /// <summary>このControllerが同時に所有できるvoice数を取得します。</summary>
        public int VoiceLimit => _voices.Count > 0 ? _voices.Count : Mathf.Clamp(_voiceLimit, MinimumVoiceLimit, MaximumVoiceLimit);

        /// <summary>現在handleに所有されているvoice数を取得します。</summary>
        public int ActiveVoiceCount => _activeVoiceCount;

        /// <summary>clipの再生を開始し、成功時に所有handleを返します。</summary>
        /// <param name="clip">再生するAudioClipです。</param>
        /// <param name="request">音量、pitch、loop、fade、priority、steal規則です。</param>
        /// <param name="handle">成功時にvoiceを所有するhandle、失敗時にnullです。</param>
        /// <param name="error">失敗理由、成功時にNoneです。</param>
        /// <returns>再生を受け付けた場合はtrueです。</returns>
        public bool TryPlay(AudioClip clip, AudioPlayRequest request, out AudioControlHandle handle, out AudioControlError error)
        {
            handle = null;
            error = GetAvailabilityError();
            if (error != AudioControlError.None)
            {
                return false;
            }

            if (clip == null)
            {
                error = AudioControlError.InvalidClip;
                return false;
            }

            if (!request.IsValid())
            {
                error = AudioControlError.InvalidRequest;
                return false;
            }

            DrainDeferredReleases();
            var voice = FindFreeVoice();
            if (voice == null)
            {
                voice = FindStealCandidate();
                if (!request.AllowSteal || voice == null || request.Priority > voice.Priority)
                {
                    error = AudioControlError.VoiceLimitReached;
                    return false;
                }

                ReleaseVoice(voice);
            }

            var token = new AudioControlToken(_generation, NextVoiceId(), request.Priority);
            try
            {
                voice.Begin(clip, request, token, NextSequence());
            }
            catch
            {
                token.DeactivateFromOwner();
                voice.StopAndReset();
                error = AudioControlError.PlaybackFailed;
                return false;
            }

            _activeVoiceCount++;
            handle = new AudioControlHandle(token);
            error = AudioControlError.None;
            return true;
        }

        /// <summary>指定handleのvoiceを即時または非スケールfadeで停止します。</summary>
        /// <param name="handle">このControllerの現在の有効期間に属するhandleです。</param>
        /// <param name="fadeOutSeconds">0以上60以下の非スケールfade秒数です。</param>
        /// <returns>受け付け結果です。</returns>
        public AudioControlError Stop(AudioControlHandle handle, float fadeOutSeconds = 0f)
        {
            var availability = GetAvailabilityError();
            if (availability != AudioControlError.None)
            {
                return availability;
            }

            if (!AudioPlayRequest.IsValidFadeDuration(fadeOutSeconds))
            {
                return AudioControlError.InvalidRequest;
            }

            if (handle == null || handle.Token.Generation != _generation)
            {
                return AudioControlError.ForeignHandle;
            }

            if (!handle.IsActive)
            {
                return AudioControlError.ReleasedHandle;
            }

            var voice = FindVoice(handle.Token.VoiceId);
            if (voice == null)
            {
                handle.Token.DeactivateFromOwner();
                return AudioControlError.ReleasedHandle;
            }

            if (fadeOutSeconds <= 0f)
            {
                ReleaseVoice(voice);
            }
            else
            {
                voice.BeginFadeOut(fadeOutSeconds);
            }

            return AudioControlError.None;
        }

        private void OnValidate()
        {
            _voiceLimit = Mathf.Clamp(_voiceLimit, MinimumVoiceLimit, MaximumVoiceLimit);
        }

        private void OnEnable()
        {
            _applicationExiting = false;
            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsureVoicePool();
            _generation = new AudioControlGeneration(_ownerThreadId, ReleaseImmediately);
            Volatile.Write(ref _available, 1);
        }

        private void Update()
        {
            if (Volatile.Read(ref _available) == 0)
            {
                return;
            }

            DrainDeferredReleases();
            for (var index = 0; index < _voices.Count; index++)
            {
                var voice = _voices[index];
                if (voice.IsActive && voice.Tick(Time.unscaledDeltaTime))
                {
                    _activeVoiceCount = Mathf.Max(0, _activeVoiceCount - 1);
                }
            }
        }

        private void OnDisable()
        {
            Shutdown(false);
        }

        private void OnDestroy()
        {
            Shutdown(false);
            for (var index = 0; index < _voices.Count; index++)
            {
                var source = _voices[index].Source;
                if (source != null)
                {
                    Destroy(source.gameObject);
                }
            }

            _voices.Clear();
        }

        private void OnApplicationQuit()
        {
            _applicationExiting = true;
            Shutdown(true);
        }

        private AudioControlError GetAvailabilityError()
        {
            if (_applicationExiting)
            {
                return AudioControlError.ApplicationExiting;
            }

            if (Volatile.Read(ref _available) == 0)
            {
                return AudioControlError.ControllerUnavailable;
            }

            return Thread.CurrentThread.ManagedThreadId == _ownerThreadId
                ? AudioControlError.None
                : AudioControlError.MainThreadRequired;
        }

        private void EnsureVoicePool()
        {
            if (_voices.Count > 0)
            {
                return;
            }

            var count = Mathf.Clamp(_voiceLimit, MinimumVoiceLimit, MaximumVoiceLimit);
            for (var index = 0; index < count; index++)
            {
                var host = new GameObject($"AudioControl Voice {index + 1:00}");
                host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
                host.transform.SetParent(transform, false);
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                source.pitch = 1f;
                source.priority = 128;
                _voices.Add(new AudioVoice(source));
            }
        }

        private AudioVoice FindFreeVoice()
        {
            for (var index = 0; index < _voices.Count; index++)
            {
                if (!_voices[index].IsActive)
                {
                    return _voices[index];
                }
            }

            return null;
        }

        private AudioVoice FindStealCandidate()
        {
            AudioVoice candidate = null;
            for (var index = 0; index < _voices.Count; index++)
            {
                var voice = _voices[index];
                if (!voice.IsActive)
                {
                    continue;
                }

                if (candidate == null || voice.Priority > candidate.Priority ||
                    voice.Priority == candidate.Priority && voice.Sequence < candidate.Sequence)
                {
                    candidate = voice;
                }
            }

            return candidate;
        }

        private AudioVoice FindVoice(long voiceId)
        {
            for (var index = 0; index < _voices.Count; index++)
            {
                var voice = _voices[index];
                if (voice.Token != null && voice.Token.VoiceId == voiceId)
                {
                    return voice;
                }
            }

            return null;
        }

        private void ReleaseImmediately(long voiceId)
        {
            if (Volatile.Read(ref _available) == 0)
            {
                return;
            }

            var voice = FindVoice(voiceId);
            if (voice != null)
            {
                ReleaseVoice(voice);
            }
        }

        private void DrainDeferredReleases()
        {
            _pendingReleases.Clear();
            _generation?.DrainPendingReleases(_pendingReleases);
            for (var index = 0; index < _pendingReleases.Count; index++)
            {
                var voice = FindVoice(_pendingReleases[index]);
                if (voice != null)
                {
                    ReleaseVoice(voice);
                }
            }

            // Disposeの即時callbackが失敗しても、非active tokenはここで必ず回収します。
            for (var index = 0; index < _voices.Count; index++)
            {
                var voice = _voices[index];
                if (voice.Token != null && !voice.Token.IsActive)
                {
                    ReleaseVoice(voice);
                }
            }
        }

        private void ReleaseVoice(AudioVoice voice)
        {
            if (!voice.IsActive)
            {
                return;
            }

            voice.StopAndReset();
            _activeVoiceCount = Mathf.Max(0, _activeVoiceCount - 1);
        }

        private void Shutdown(bool applicationExiting)
        {
            if (applicationExiting)
            {
                _applicationExiting = true;
            }

            Volatile.Write(ref _available, 0);
            _generation?.Close();
            _generation = null;
            _pendingReleases.Clear();
            for (var index = 0; index < _voices.Count; index++)
            {
                if (_voices[index].IsActive)
                {
                    _voices[index].StopAndReset();
                }
            }

            _activeVoiceCount = 0;
        }

        private long NextVoiceId()
        {
            _nextVoiceId = _nextVoiceId == long.MaxValue ? 1 : _nextVoiceId + 1;
            return _nextVoiceId;
        }

        private long NextSequence()
        {
            _nextSequence = _nextSequence == long.MaxValue ? 1 : _nextSequence + 1;
            return _nextSequence;
        }
    }
}
