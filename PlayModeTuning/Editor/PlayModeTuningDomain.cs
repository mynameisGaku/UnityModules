using System;

namespace PlayModeTuning.Editor
{
    /// <summary>Provides one token whose Play entry behavior was verified for both supported reload modes.</summary>
    internal static class PlayModeTuningDomain
    {
        internal static readonly string Token = Guid.NewGuid().ToString("N");
    }
}
