using System;

namespace BuildAssistant.Editor
{
    /// <summary>Unityオブジェクトを保持せず、順序付きビルドシーン入力を1件記録します。</summary>
    public sealed class BuildAssistantScene
    {
        /// <summary>変更不能なシーンの記録を作成します。</summary>
        /// <param name="order">実際に使われるプロファイルのシーン一覧における、0から始まる順番。</param>
        /// <param name="guid">Unity素材のGUID。パスを解決できない場合は空文字列。</param>
        /// <param name="assetPath">プロジェクトからの相対シーン素材パス。</param>
        /// <param name="enabled">プレイヤービルドへシーンを含めるかどうか。</param>
        /// <param name="dependencyHash">計画作成時に記録したUnity依存関係のハッシュ値。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/>が0未満の場合に発生します。</exception>
        public BuildAssistantScene(int order, string guid, string assetPath, bool enabled, string dependencyHash)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order));

            Order = order;
            Guid = guid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Enabled = enabled;
            DependencyHash = dependencyHash ?? string.Empty;
        }

        /// <summary>実際に使われるプロファイルのシーン一覧における、0から始まる順番を取得します。</summary>
        public int Order { get; }

        /// <summary>Unity素材のGUIDを取得します。</summary>
        public string Guid { get; }

        /// <summary>プロジェクトからの相対シーン素材パスを取得します。</summary>
        public string AssetPath { get; }

        /// <summary>プレイヤービルドへシーンを含めるかどうかを取得します。</summary>
        public bool Enabled { get; }

        /// <summary>計画作成時に記録した依存関係のハッシュ値を取得します。</summary>
        public string DependencyHash { get; }
    }
}
