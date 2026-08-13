using UnityEngine;

namespace TimeControl
{
    /// <summary>UnityのTime.timeScaleだけを読み書きする実行時保管先。</summary>
    internal sealed class UnityTimeScaleBackend : ITimeScaleBackend
    {
        /// <summary>共有できる無状態の保管先。</summary>
        internal static readonly UnityTimeScaleBackend Instance = new UnityTimeScaleBackend();

        private UnityTimeScaleBackend()
        {
        }

        /// <summary>Unityの現在の時間倍率を読み取る。</summary>
        /// <returns>現在のTime.timeScale。</returns>
        public float Read() => Time.timeScale;

        /// <summary>Unityの時間倍率を書き換える。</summary>
        /// <param name="value">書き込む時間倍率。</param>
        public void Write(float value) => Time.timeScale = value;
    }
}
