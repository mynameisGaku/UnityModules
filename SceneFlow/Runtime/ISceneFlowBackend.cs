using System.Threading;
using UnityEngine;

namespace SceneFlow
{
    /// <summary>Unityの静的Scene APIを状態機械から分離する内部境界。</summary>
    internal interface ISceneFlowBackend
    {
        /// <summary>Play終了またはアプリ終了で発火するトークン。</summary>
        CancellationToken ExitToken { get; }

        /// <summary>現在の呼出スレッドがUnity callbackで確定済みのメインスレッドか調べる。</summary>
        bool IsMainThread { get; }

        /// <summary>SceneをPlayerまたは現在のBuild Profileから読み込めるか調べる。</summary>
        /// <param name="path">検査するSceneの完全パス。</param>
        /// <returns>現在の実行環境で読み込める場合はtrue。</returns>
        bool CanLoad(string path);

        /// <summary>完全パスが一致するSceneの数を返す。</summary>
        /// <param name="path">照合するSceneの完全パス。</param>
        /// <returns>現在読込済みで完全パスが一致するScene数。</returns>
        int CountLoaded(string path);

        /// <summary>読込済みSceneの総数を返す。</summary>
        int LoadedSceneCount { get; }

        /// <summary>現在読込済みのSceneをhandleと完全パスで固定した一覧を返す。</summary>
        /// <returns>操作前後の同一性確認に使うScene一覧。</returns>
        SceneFlowSceneIdentity[] SnapshotLoadedScenes();

        /// <summary>対象が現在の有効Sceneか調べる。</summary>
        /// <param name="path">検査するSceneの完全パス。</param>
        /// <returns>一意な対象が有効Sceneの場合はtrue。</returns>
        bool IsActive(string path);

        /// <summary>非同期Scene操作を開始する。</summary>
        /// <param name="path">読み込むSceneの完全パス。</param>
        /// <param name="additive">現在のSceneを保持して追加読込する場合はtrue。</param>
        /// <returns>開始した操作。開始できなければnull。</returns>
        ISceneFlowAsyncOperation Load(string path, bool additive);

        /// <summary>一意に特定したSceneの非同期アンロードを開始する。</summary>
        /// <param name="path">アンロードするSceneの完全パス。</param>
        /// <returns>開始した操作。開始できなければnull。</returns>
        ISceneFlowAsyncOperation Unload(string path);

        /// <summary>一意に特定したSceneを有効Sceneへ変更する。</summary>
        /// <param name="path">有効にするSceneの完全パス。</param>
        /// <returns>変更できた場合はtrue。</returns>
        bool SetActive(string path);

        /// <summary>次のフレームまで待つ。</summary>
        /// <param name="cancellationToken">Play終了またはアプリ終了を検出するトークン。</param>
        /// <returns>次のフレームで完了する待機。</returns>
        Awaitable NextFrame(CancellationToken cancellationToken);
    }

    /// <summary>UnityのAsyncOperationから状態機械が必要とする値だけを公開する。</summary>
    internal interface ISceneFlowAsyncOperation
    {
        /// <summary>操作が完了したかどうか。</summary>
        bool IsDone { get; }

        /// <summary>Unityが返す0以上1以下の生の進捗。</summary>
        float Progress { get; }
    }
}
