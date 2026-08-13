using System;

namespace SceneFlow
{
    /// <summary>読込済みSceneを操作前後で照合するためのhandleと完全パス。</summary>
    internal readonly struct SceneFlowSceneIdentity : IEquatable<SceneFlowSceneIdentity>
    {
        /// <summary>Unityが割り当てたhandleと完全パスから同一性情報を作る。</summary>
        /// <param name="handle">Scene instanceを識別するUnityの生handle。</param>
        /// <param name="path">Sceneの完全パス。</param>
        public SceneFlowSceneIdentity(ulong handle, string path)
        {
            Handle = handle;
            Path = path ?? string.Empty;
        }

        /// <summary>Scene instanceを識別するUnityの生handle。</summary>
        public ulong Handle { get; }

        /// <summary>Sceneの完全パス。</summary>
        public string Path { get; }

        /// <summary>同じScene instanceと完全パスを表すか調べる。</summary>
        /// <param name="other">比較するScene同一性情報。</param>
        /// <returns>handleと完全パスが一致する場合はtrue。</returns>
        public bool Equals(SceneFlowSceneIdentity other) =>
            Handle == other.Handle && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

        /// <summary>同じScene同一性情報か調べる。</summary>
        /// <param name="obj">比較する値。</param>
        /// <returns>同じScene同一性情報の場合はtrue。</returns>
        public override bool Equals(object obj) => obj is SceneFlowSceneIdentity other && Equals(other);

        /// <summary>handleと完全パスに基づくハッシュ値を返す。</summary>
        /// <returns>Scene同一性情報のハッシュ値。</returns>
        public override int GetHashCode() => (Handle.GetHashCode() * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
    }
}
