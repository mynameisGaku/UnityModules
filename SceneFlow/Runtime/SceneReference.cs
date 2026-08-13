using System;
using UnityEngine;

namespace SceneFlow
{
    /// <summary>短名の衝突を避けるため、プロジェクト相対の完全なSceneパスを保持する。</summary>
    [Serializable]
    public struct SceneReference : IEquatable<SceneReference>
    {
        [SerializeField]
        [Tooltip("Scene AssetのGUID。Editorで移動後のパスを追跡するために使う。")]
        private string _guid;

        [SerializeField]
        [Tooltip("AssetsまたはPackagesから始まり.unityで終わるSceneの完全パス。")]
        private string _path;

        /// <summary>SceneManagerへ渡すプロジェクト相対の完全パス。</summary>
        public string Path => _path ?? string.Empty;

        /// <summary>完全パスとして利用できる形式ならtrue。Build Profileへの登録は実行時に別途検査する。</summary>
        public bool IsValid => IsValidPath(_path);

        /// <summary>プロジェクト相対の完全パスから参照を作る。</summary>
        /// <param name="path">AssetsまたはPackagesから始まり.unityで終わるSceneパス。</param>
        public SceneReference(string path)
        {
            _guid = string.Empty;
            _path = NormalizePath(path);
        }

        /// <summary>EditorがAssetの識別子と完全パスを同時に保存するために使う。</summary>
        /// <param name="guid">Scene AssetのGUID。</param>
        /// <param name="path">Scene Assetの完全パス。</param>
        internal SceneReference(string guid, string path)
        {
            _guid = guid ?? string.Empty;
            _path = NormalizePath(path);
        }

        /// <summary>同じ完全パスを表すか調べる。UnityのSceneパスに合わせ大文字小文字を区別しない。</summary>
        /// <param name="other">比較するScene参照。</param>
        /// <returns>同じ完全パスならtrue。</returns>
        public bool Equals(SceneReference other) => string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

        /// <summary>同じScene参照か調べる。</summary>
        /// <param name="obj">比較する値。</param>
        /// <returns>同じScene参照ならtrue。</returns>
        public override bool Equals(object obj) => obj is SceneReference other && Equals(other);

        /// <summary>完全パスに基づくハッシュ値を返す。</summary>
        /// <returns>大文字小文字を区別しないパスのハッシュ値。</returns>
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Path);

        /// <summary>ログと画面表示に使える完全パスを返す。</summary>
        /// <returns>Sceneの完全パス。</returns>
        public override string ToString() => Path;

        /// <summary>2つの参照が同じ完全パスならtrue。</summary>
        /// <param name="left">左側の参照。</param>
        /// <param name="right">右側の参照。</param>
        /// <returns>同じ完全パスならtrue。</returns>
        public static bool operator ==(SceneReference left, SceneReference right) => left.Equals(right);

        /// <summary>2つの参照が異なる完全パスならtrue。</summary>
        /// <param name="left">左側の参照。</param>
        /// <param name="right">右側の参照。</param>
        /// <returns>完全パスが異なればtrue。</returns>
        public static bool operator !=(SceneReference left, SceneReference right) => !left.Equals(right);

        /// <summary>Sceneとして利用できる完全パスか調べる。</summary>
        /// <param name="path">検査するパス。</param>
        /// <returns>許可する完全パスならtrue。</returns>
        internal static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!string.Equals(path, path.Trim(), StringComparison.Ordinal)) return false;
            if (path.IndexOf('\\') >= 0 || path.IndexOf('?') >= 0 || path.IndexOf('#') >= 0) return false;
            var hasRoot = path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);
            if (!hasRoot || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || path.Length <= "Assets/.unity".Length) return false;
            var fileNameStart = path.LastIndexOf('/') + 1;
            if (path.Length - fileNameStart <= ".unity".Length) return false;

            var segmentStart = 0;
            while (segmentStart < path.Length)
            {
                var separator = path.IndexOf('/', segmentStart);
                var segmentLength = (separator < 0 ? path.Length : separator) - segmentStart;
                if (segmentLength == 0) return false;
                if (segmentLength == 1 && path[segmentStart] == '.') return false;
                if (segmentLength == 2 && path[segmentStart] == '.' && path[segmentStart + 1] == '.') return false;
                if (separator < 0) break;
                segmentStart = separator + 1;
            }

            return true;
        }

        private static string NormalizePath(string path) => path?.Replace('\\', '/') ?? string.Empty;
    }
}
