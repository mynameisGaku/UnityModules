using UnityEngine;

namespace AudioControl
{
    internal sealed class AudioVoice
    {
        private enum FadeState
        {
            None,
            In,
            Out
        }

        private FadeState _fadeState;
        private double _fadeElapsed;
        private float _fadeDuration;
        private float _fadeStartVolume;
        private float _targetVolume;
        private int _framesSinceStart;

        internal AudioVoice(AudioSource source)
        {
            Source = source;
        }

        internal AudioSource Source { get; }

        internal AudioControlToken Token { get; private set; }

        internal long Sequence { get; private set; }

        internal int Priority => Token == null ? AudioPlayRequest.LowestPriority : Token.Priority;

        internal bool IsActive => Token != null;

        internal void Begin(AudioClip clip, AudioPlayRequest request, AudioControlToken token, long sequence)
        {
            Token = token;
            Sequence = sequence;
            _targetVolume = request.Volume;
            _fadeDuration = request.FadeInSeconds;
            _fadeElapsed = 0d;
            _fadeStartVolume = 0f;
            _framesSinceStart = 0;
            _fadeState = _fadeDuration > 0f ? FadeState.In : FadeState.None;

            Source.Stop();
            Source.clip = clip;
            Source.loop = request.Loop;
            Source.pitch = request.Pitch;
            Source.priority = request.Priority;
            Source.volume = _fadeState == FadeState.In ? 0f : _targetVolume;
            Source.Play();
        }

        internal void BeginFadeOut(float seconds)
        {
            if (seconds <= 0f)
            {
                StopAndReset();
                return;
            }

            _fadeState = FadeState.Out;
            _fadeElapsed = 0d;
            _fadeDuration = seconds;
            _fadeStartVolume = Source.volume;
        }

        internal bool Tick(float unscaledDeltaTime)
        {
            if (Token == null)
            {
                return false;
            }

            if (!Token.IsActive)
            {
                StopAndReset();
                return true;
            }

            _framesSinceStart++;
            if (_fadeState != FadeState.None)
            {
                _fadeElapsed += unscaledDeltaTime;
                var progress = _fadeDuration <= 0f ? 1f : Mathf.Clamp01((float)(_fadeElapsed / _fadeDuration));
                if (_fadeState == FadeState.In)
                {
                    Source.volume = Mathf.LerpUnclamped(0f, _targetVolume, progress);
                    if (progress >= 1f)
                    {
                        Source.volume = _targetVolume;
                        _fadeState = FadeState.None;
                    }
                }
                else
                {
                    Source.volume = Mathf.LerpUnclamped(_fadeStartVolume, 0f, progress);
                    if (progress >= 1f)
                    {
                        StopAndReset();
                        return true;
                    }
                }
            }

            if (!Source.loop && _framesSinceStart > 1 && !Source.isPlaying)
            {
                StopAndReset();
                return true;
            }

            return false;
        }

        internal void StopAndReset()
        {
            var token = Token;
            Token = null;
            Source.Stop();
            Source.clip = null;
            Source.loop = false;
            Source.pitch = 1f;
            Source.priority = 128;
            Source.volume = 1f;
            _fadeState = FadeState.None;
            _fadeElapsed = 0d;
            _framesSinceStart = 0;
            token?.DeactivateFromOwner();
        }
    }
}
