// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Haptics.Editor.Tests
{
    /// <summary>振動要求を記録するだけのtest用driver。capabilityと戻り値はtestから設定する。</summary>
    internal sealed class FakeHapticsDriver : IHapticsDriver
    {
        /// <summary>testが設定する報告capability。</summary>
        public HapticsCapability Capability { get; set; }

        /// <summary>TryVibrateが返す値。</summary>
        public bool ResultToReturn { get; set; } = true;

        /// <summary>受け取った要求の記録。</summary>
        public List<HapticsPattern> Requests { get; } = new List<HapticsPattern>();

        /// <summary>これまでに受け取った要求の数。</summary>
        public int RequestCount => Requests.Count;

        /// <summary>要求を記録し、設定された値を返す。</summary>
        /// <param name="pattern">serviceから渡されたpattern。</param>
        /// <returns><see cref="ResultToReturn"/>の値。</returns>
        public bool TryVibrate(HapticsPattern pattern)
        {
            Requests.Add(pattern);
            return ResultToReturn;
        }
    }
}
