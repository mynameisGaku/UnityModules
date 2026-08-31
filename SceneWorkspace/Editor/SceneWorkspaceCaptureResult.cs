using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>独立して保持した現在のシーン構成、または取得失敗の理由を返します。</summary>
    public sealed class SceneWorkspaceCaptureResult
    {
        /// <summary>失敗理由、案内、指紋値、シーン構成から取得結果を作成します。</summary>
        internal SceneWorkspaceCaptureResult(SceneWorkspaceError error, string message, string fingerprint, IEnumerable<SceneWorkspaceSceneState> scenes)
        {
            Error = error;
            Message = message ?? string.Empty;
            Fingerprint = fingerprint ?? string.Empty;
            Scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
        }

        /// <summary>失敗していない場合は<see cref="SceneWorkspaceError.None"/>を返します。</summary>
        public SceneWorkspaceError Error { get; }

        /// <summary>失敗理由または処理結果の日本語案内を返します。</summary>
        public string Message { get; }

        /// <summary>順番と状態を含む現在構成のSHA-256指紋値を返します。取得失敗時は空です。</summary>
        public string Fingerprint { get; }

        /// <summary>取得したシーンを現在の順番で返します。呼び出し側から一覧を変更できません。</summary>
        public IReadOnlyList<SceneWorkspaceSceneState> Scenes { get; }

        /// <summary>現在構成の取得と検証に成功したかを返します。</summary>
        public bool Succeeded => Error == SceneWorkspaceError.None;
    }
}
