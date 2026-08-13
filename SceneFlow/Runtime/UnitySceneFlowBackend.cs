using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneFlow
{
    /// <summary>SceneManagerとApplicationの実状態へ接続する標準backend。</summary>
    internal sealed class UnitySceneFlowBackend : ISceneFlowBackend
    {
        /// <summary>Unity callbackで確定済みのメインスレッドからの生成だけを許可する。</summary>
        /// <exception cref="InvalidOperationException">Unityのメインスレッド以外から生成した場合。</exception>
        public UnitySceneFlowBackend()
        {
            SceneFlowMainThread.RequireCurrent();
        }

        /// <inheritdoc />
        public CancellationToken ExitToken => Application.exitCancellationToken;

        /// <inheritdoc />
        public bool IsMainThread => SceneFlowMainThread.IsCurrent;

        /// <inheritdoc />
        public int LoadedSceneCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.IsValid() && scene.isLoaded) count++;
                }

                return count;
            }
        }

        /// <inheritdoc />
        public bool CanLoad(string path) => Application.CanStreamedLevelBeLoaded(path);

        /// <inheritdoc />
        public int CountLoaded(string path)
        {
            var count = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && PathEquals(scene.path, path)) count++;
            }

            return count;
        }

        /// <inheritdoc />
        public SceneFlowSceneIdentity[] SnapshotLoadedScenes()
        {
            var result = new SceneFlowSceneIdentity[LoadedSceneCount];
            var writeIndex = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                result[writeIndex++] = new SceneFlowSceneIdentity(scene.handle.GetRawData(), scene.path);
            }

            return result;
        }

        /// <inheritdoc />
        public bool IsActive(string path)
        {
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.isLoaded && PathEquals(scene.path, path);
        }

        /// <inheritdoc />
        public ISceneFlowAsyncOperation Load(string path, bool additive)
        {
            var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            var operation = SceneManager.LoadSceneAsync(path, mode);
            return operation == null ? null : new UnitySceneFlowAsyncOperation(operation);
        }

        /// <inheritdoc />
        public ISceneFlowAsyncOperation Unload(string path)
        {
            var scene = FindUniqueLoadedScene(path);
            if (!scene.IsValid()) return null;

            var operation = SceneManager.UnloadSceneAsync(scene);
            return operation == null ? null : new UnitySceneFlowAsyncOperation(operation);
        }

        /// <inheritdoc />
        public bool SetActive(string path)
        {
            var scene = FindUniqueLoadedScene(path);
            return scene.IsValid() && scene.isLoaded && SceneManager.SetActiveScene(scene);
        }

        /// <inheritdoc />
        public Awaitable NextFrame(CancellationToken cancellationToken) => Awaitable.NextFrameAsync(cancellationToken);

        private static Scene FindUniqueLoadedScene(string path)
        {
            var match = default(Scene);
            var found = false;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (!candidate.IsValid() || !candidate.isLoaded || !PathEquals(candidate.path, path)) continue;
                if (found) return default;

                match = candidate;
                found = true;
            }

            return match;
        }

        private static bool PathEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        /// <summary>UnityのAsyncOperationを内部契約へ合わせる。</summary>
        private sealed class UnitySceneFlowAsyncOperation : ISceneFlowAsyncOperation
        {
            private readonly AsyncOperation _operation;

            public UnitySceneFlowAsyncOperation(AsyncOperation operation)
            {
                _operation = operation;
            }

            /// <inheritdoc />
            public bool IsDone => _operation.isDone;

            /// <inheritdoc />
            public float Progress => _operation.progress;
        }
    }
}
