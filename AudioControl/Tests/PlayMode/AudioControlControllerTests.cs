using System.Collections;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioControl.PlayMode.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class AudioControlControllerTests
    {
        private GameObject _host;
        private AudioControlController _controller;
        private AudioClip _clip;
        private float _originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalTimeScale = Time.timeScale;
            _clip = CreateClip("AudioControl Test", 2f);
            _host = new GameObject("AudioControl Test Host");
            _host.SetActive(false);
            _controller = _host.AddComponent<AudioControlController>();
            SetVoiceLimit(_controller, 2);
            _host.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _originalTimeScale;
            if (_host != null)
            {
                Object.Destroy(_host);
            }

            if (_clip != null)
            {
                Object.Destroy(_clip);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayAndDispose_OwnsOnlyOneVoice()
        {
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var handle, out var error), Is.True);
            Assert.That(error, Is.EqualTo(AudioControlError.None));
            Assert.That(handle.IsActive, Is.True);
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(1));

            handle.Dispose();

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VoicePool_UsesExactLimitAndOwnedTwoDimensionalSources()
        {
            var sources = _host.GetComponentsInChildren<AudioSource>(true);

            Assert.That(sources, Has.Length.EqualTo(2));
            foreach (var source in sources)
            {
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.spatialBlend, Is.EqualTo(0f));
                Assert.That(source.clip, Is.Null);
            }

            Assert.That(_controller.VoiceLimit, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonLoopClip_NaturallyReleasesHandle()
        {
            var shortClip = CreateClip("AudioControl Short", 0.05f);
            Assert.That(_controller.TryPlay(shortClip, AudioPlayRequest.Default, out var handle, out _), Is.True);

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
            Object.Destroy(shortClip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidFadeOut_DoesNotChangeActiveVoice()
        {
            var loop = new AudioPlayRequest(1f, 1f, true, 0f, 128, true);
            Assert.That(_controller.TryPlay(_clip, loop, out var handle, out _), Is.True);

            Assert.That(_controller.Stop(handle, float.NaN), Is.EqualTo(AudioControlError.InvalidRequest));
            Assert.That(_controller.Stop(handle, AudioPlayRequest.MaximumFadeDuration + 0.01f), Is.EqualTo(AudioControlError.InvalidRequest));

            Assert.That(handle.IsActive, Is.True);
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VoiceLimit_StealsLowestPriorityOldestVoice()
        {
            var low = new AudioPlayRequest(1f, 1f, true, 0f, 200, true);
            var equal = new AudioPlayRequest(1f, 1f, true, 0f, 200, true);
            var high = new AudioPlayRequest(1f, 1f, true, 0f, 20, true);
            Assert.That(_controller.TryPlay(_clip, low, out var first, out _), Is.True);
            Assert.That(_controller.TryPlay(_clip, equal, out var second, out _), Is.True);

            Assert.That(_controller.TryPlay(_clip, high, out var replacement, out var error), Is.True);

            Assert.That(error, Is.EqualTo(AudioControlError.None));
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(replacement.IsActive, Is.True);
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LowerPriorityOrNoSteal_LeavesCurrentVoicesUntouched()
        {
            var current = new AudioPlayRequest(1f, 1f, true, 0f, 10, true);
            Assert.That(_controller.TryPlay(_clip, current, out var first, out _), Is.True);
            Assert.That(_controller.TryPlay(_clip, current, out var second, out _), Is.True);
            var lower = new AudioPlayRequest(1f, 1f, true, 0f, 200, true);
            var noSteal = new AudioPlayRequest(1f, 1f, true, 0f, 0, false);

            Assert.That(_controller.TryPlay(_clip, lower, out var lowerHandle, out var lowerError), Is.False);
            Assert.That(_controller.TryPlay(_clip, noSteal, out var deniedHandle, out var deniedError), Is.False);

            Assert.That(lowerHandle, Is.Null);
            Assert.That(deniedHandle, Is.Null);
            Assert.That(lowerError, Is.EqualTo(AudioControlError.VoiceLimitReached));
            Assert.That(deniedError, Is.EqualTo(AudioControlError.VoiceLimitReached));
            Assert.That(first.IsActive && second.IsActive, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FadeInAndFadeOut_AdvanceWhileTimeScaleIsZero()
        {
            Time.timeScale = 0f;
            var request = new AudioPlayRequest(0.8f, 1f, true, 0.05f, 100, true);
            Assert.That(_controller.TryPlay(_clip, request, out var handle, out _), Is.True);
            var source = FindActiveSource(_host);
            Assert.That(source.volume, Is.EqualTo(0f).Within(0.001f));

            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(source.volume, Is.EqualTo(0.8f).Within(0.08f));
            Assert.That(_controller.Stop(handle, 0.05f), Is.EqualTo(AudioControlError.None));
            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WorkerDispose_IsAppliedOnNextUpdate()
        {
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var handle, out _), Is.True);
            var thread = new Thread(handle.Dispose);
            thread.Start();
            thread.Join();

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(1));
            yield return null;

            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableInvalidatesGeneration_AndStaleHandleCannotAffectNewVoice()
        {
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var stale, out _), Is.True);
            _controller.enabled = false;
            Assert.That(stale.IsActive, Is.False);
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
            _controller.enabled = true;
            yield return null;
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var current, out _), Is.True);

            stale.Dispose();

            Assert.That(current.IsActive, Is.True);
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ForeignAndReleasedHandles_ReturnExactErrors()
        {
            var otherHost = new GameObject("Other AudioControl");
            var other = otherHost.AddComponent<AudioControlController>();
            yield return null;
            Assert.That(other.TryPlay(_clip, AudioPlayRequest.Default, out var foreign, out _), Is.True);
            Assert.That(_controller.Stop(foreign), Is.EqualTo(AudioControlError.ForeignHandle));
            foreign.Dispose();
            Assert.That(other.Stop(foreign), Is.EqualTo(AudioControlError.ReleasedHandle));
            Object.Destroy(otherHost);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidInputAndInactiveController_DoNotAllocateVoice()
        {
            Assert.That(_controller.TryPlay(null, AudioPlayRequest.Default, out var nullHandle, out var clipError), Is.False);
            var invalid = new AudioPlayRequest(float.NaN, 1f, false, 0f, 128, true);
            Assert.That(_controller.TryPlay(_clip, invalid, out var invalidHandle, out var requestError), Is.False);
            _controller.enabled = false;
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var inactiveHandle, out var inactiveError), Is.False);

            Assert.That(nullHandle, Is.Null);
            Assert.That(invalidHandle, Is.Null);
            Assert.That(inactiveHandle, Is.Null);
            Assert.That(clipError, Is.EqualTo(AudioControlError.InvalidClip));
            Assert.That(requestError, Is.EqualTo(AudioControlError.InvalidRequest));
            Assert.That(inactiveError, Is.EqualTo(AudioControlError.ControllerUnavailable));
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveWorkerCall_RequiresMainThread()
        {
            AudioControlError observed = AudioControlError.None;
            var thread = new Thread(() => _controller.TryPlay(_clip, AudioPlayRequest.Default, out _, out observed));
            thread.Start();
            thread.Join();

            Assert.That(observed, Is.EqualTo(AudioControlError.MainThreadRequired));
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplicationQuitStopsVoices_AndEnableStartsFreshLifecycle()
        {
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var handle, out _), Is.True);
            _controller.SendMessage("OnApplicationQuit");
            Assert.That(handle.IsActive, Is.False);
            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out _, out var exiting), Is.False);
            Assert.That(exiting, Is.EqualTo(AudioControlError.ApplicationExiting));

            _controller.enabled = false;
            _controller.enabled = true;
            yield return null;

            Assert.That(_controller.TryPlay(_clip, AudioPlayRequest.Default, out var current, out var error), Is.True);
            Assert.That(error, Is.EqualTo(AudioControlError.None));
            current.Dispose();
        }

        private static AudioClip CreateClip(string name, float seconds)
        {
            const int frequency = 44100;
            var samples = Mathf.CeilToInt(frequency * seconds);
            var clip = AudioClip.Create(name, samples, 1, frequency, false);
            var data = new float[samples];
            for (var index = 0; index < samples; index++)
            {
                data[index] = Mathf.Sin(2f * Mathf.PI * 220f * index / frequency) * 0.05f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private static void SetVoiceLimit(AudioControlController controller, int value)
        {
            typeof(AudioControlController).GetField("_voiceLimit", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, value);
        }

        private static AudioSource FindActiveSource(GameObject host)
        {
            foreach (var source in host.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != null)
                {
                    return source;
                }
            }

            Assert.Fail("Active AudioSource was not found.");
            return null;
        }
    }
}
