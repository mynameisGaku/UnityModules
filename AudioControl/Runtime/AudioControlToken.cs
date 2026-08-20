using System.Threading;

namespace AudioControl
{
    internal sealed class AudioControlToken
    {
        private readonly AudioControlGeneration _generation;
        private int _active = 1;

        internal AudioControlToken(AudioControlGeneration generation, long voiceId, int priority)
        {
            _generation = generation;
            VoiceId = voiceId;
            Priority = priority;
        }

        internal long VoiceId { get; }

        internal int Priority { get; }

        internal AudioControlGeneration Generation => _generation;

        internal bool IsActive => Volatile.Read(ref _active) != 0;

        internal void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0)
            {
                return;
            }

            _generation.ReleaseFromHandle(VoiceId);
        }

        internal void DeactivateFromOwner()
        {
            Interlocked.Exchange(ref _active, 0);
        }
    }
}
