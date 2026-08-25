// SPDX-License-Identifier: MIT

using UnityEngine;

namespace ObjectPool
{
    /// <summary>instanceの所属と状態を示す目印。prefab poolだけが操作する。手動追加、手動破壊はしない。</summary>
    [DisallowMultipleComponent]
    public sealed class PooledInstanceMarker : MonoBehaviour
    {
        /// <summary>発行元poolの一意id。返却時の照合に使う。</summary>
        public int PoolId { get; private set; }

        /// <summary>このinstanceが取り出された累積回数。新規生成直後は0、再利用ごとに増える。</summary>
        public long Generation { get; private set; }

        /// <summary>idleへ返却済みの場合はtrue。取り出されている間はfalse。</summary>
        public bool IsReleased { get; private set; }

        /// <summary>新規生成直後の状態へ初期化する。pool以外から呼ばない。</summary>
        /// <param name="poolId">発行元poolの一意id。</param>
        internal void Bind(int poolId)
        {
            PoolId = poolId;
            Generation = 0;
            IsReleased = false;
        }

        /// <summary>idleから再利用されたことを記録する。pool以外から呼ばない。</summary>
        internal void MarkReused()
        {
            Generation++;
            IsReleased = false;
        }

        /// <summary>idleへ返却されたことを記録する。pool以外から呼ばない。</summary>
        internal void MarkReleased()
        {
            IsReleased = true;
        }
    }
}
