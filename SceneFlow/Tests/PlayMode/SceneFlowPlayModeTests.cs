using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SceneFlow.Tests.PlayMode
{
    /// <summary>実際のSceneManagerを使い、公開操作、進捗、通知順、直列化を確かめる。</summary>
    public sealed class SceneFlowPlayModeTests
    {
        private string _harnessPath;
        private string _targetAPath;
        private string _targetBPath;

        /// <summary>実Play Modeで登録済みの配置を解決し、Harnessだけを読み込む。</summary>
        [UnitySetUp]
        public IEnumerator LoadHarnessScene()
        {
            Assert.That(Application.isPlaying, Is.True, "PlayMode test runnerから実行されていません");
            Assert.That(SceneFlowPlayModeScenePaths.TryResolve(out var scenePaths, out var resolveError), Is.True, resolveError);
            _harnessPath = scenePaths[0];
            _targetAPath = scenePaths[1];
            _targetBPath = scenePaths[2];
            AssertProjectRelativeScenePath(_harnessPath);
            AssertProjectRelativeScenePath(_targetAPath);
            AssertProjectRelativeScenePath(_targetBPath);
            AssertLoadable(_harnessPath);
            AssertLoadable(_targetAPath);
            AssertLoadable(_targetBPath);

            var operation = SceneManager.LoadSceneAsync(_harnessPath, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, $"Harness Sceneを開始できません: {_harnessPath}");
            while (!operation.isDone) yield return null;

            AssertLoadedRoot(_harnessPath, "Scene Flow Test Harness");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(_harnessPath).IgnoreCase);
        }

        /// <summary>4種類の公開操作を完全パスで実行し、実Scene状態と通知契約をまとめて検証する。</summary>
        [UnityTest]
        public IEnumerator Operations_UseFullPathsAndPreserveProgressAndEventContracts()
        {
            var service = new SceneFlowService();
            var trace = new List<FlowTraceEntry>();
            service.StatusChanged += status => trace.Add(FlowTraceEntry.FromStatus(status));
            service.Finished += result => trace.Add(FlowTraceEntry.FromFinished(result));

            var result = default(SceneFlowResult);
            yield return WaitForResult(service.LoadSingleAsync(new SceneReference(_targetAPath)), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);
            AssertSuccessfulTrace(trace, SceneFlowOperation.LoadSingle, _targetAPath, SceneFlowPhase.Loading, true);
            AssertLoadedRoot(_targetAPath, "Scene Flow Test Target A");
            AssertNotLoaded(_harnessPath);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(_targetAPath).IgnoreCase);

            trace.Clear();
            yield return WaitForResult(service.LoadAdditiveAsync(new SceneReference(_targetBPath)), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);
            AssertSuccessfulTrace(trace, SceneFlowOperation.LoadAdditive, _targetBPath, SceneFlowPhase.Loading, true);
            AssertLoadedRoot(_targetAPath, "Scene Flow Test Target A");
            AssertLoadedRoot(_targetBPath, "Scene Flow Test Target B");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(_targetAPath).IgnoreCase);

            trace.Clear();
            yield return WaitForResult(service.SetActiveAsync(new SceneReference(_targetBPath)), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);
            AssertSuccessfulTrace(trace, SceneFlowOperation.SetActive, _targetBPath, SceneFlowPhase.SettingActive, false);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(_targetBPath).IgnoreCase);

            trace.Clear();
            yield return WaitForResult(service.UnloadAsync(new SceneReference(_targetAPath)), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);
            AssertSuccessfulTrace(trace, SceneFlowOperation.Unload, _targetAPath, SceneFlowPhase.Unloading, false);
            AssertNotLoaded(_targetAPath);
            AssertLoadedRoot(_targetBPath, "Scene Flow Test Target B");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(_targetBPath).IgnoreCase);
            Assert.That(service.IsBusy, Is.False);
            Assert.That(service.Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
        }

        /// <summary>Awaitableの完了をフレーム単位で待ち、結果を呼出元へ返す。</summary>
        /// <param name="operation">完了を待つScene Flow操作。</param>
        /// <param name="receiveResult">完了結果の受取先。</param>
        private static IEnumerator WaitForResult(Awaitable<SceneFlowResult> operation, Action<SceneFlowResult> receiveResult)
        {
            var awaiter = operation.GetAwaiter();
            while (!awaiter.IsCompleted) yield return null;
            receiveResult(awaiter.GetResult());
        }

        /// <summary>成功操作の状態通知、完了通知、Idle復帰が規定順であることを確かめる。</summary>
        private static void AssertSuccessfulTrace(IReadOnlyList<FlowTraceEntry> trace, SceneFlowOperation operation, string path, SceneFlowPhase workPhase, bool expectsVerifying)
        {
            Assert.That(trace.Count, Is.GreaterThanOrEqualTo(5), "必要な状態通知が不足しています");

            var index = 0;
            AssertStatus(trace[index++], SceneFlowPhase.Validating, operation, path);

            var workStart = index;
            while (index < trace.Count && IsStatusPhase(trace[index], workPhase))
            {
                AssertStatus(trace[index], workPhase, operation, path);
                index++;
            }

            Assert.That(index, Is.GreaterThan(workStart), $"{workPhase}が通知されていません");
            var lastWorkStatus = trace[index - 1].Status;
            if (workPhase == SceneFlowPhase.Loading || workPhase == SceneFlowPhase.Unloading)
            {
                Assert.That(lastWorkStatus.Progress, Is.EqualTo(1f).Within(0.000001f), $"{workPhase}の最終進捗が1ではありません");
            }

            if (expectsVerifying) AssertStatus(trace[index++], SceneFlowPhase.Verifying, operation, path);
            AssertStatus(trace[index++], SceneFlowPhase.Completed, operation, path);

            Assert.That(index, Is.LessThan(trace.Count), "Finished通知がありません");
            Assert.That(trace[index].Kind, Is.EqualTo(FlowTraceKind.Finished));
            Assert.That(trace[index].Result.IsSuccess, Is.True, trace[index].Result.Message);
            Assert.That(trace[index].Result.Request.Operation, Is.EqualTo(operation));
            Assert.That(trace[index].Result.Request.Scene.Path, Is.EqualTo(path));
            index++;

            Assert.That(index, Is.LessThan(trace.Count), "Idle通知がありません");
            Assert.That(trace[index].Kind, Is.EqualTo(FlowTraceKind.Status));
            Assert.That(trace[index].Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
            Assert.That(trace[index].Status.Progress, Is.EqualTo(0f));
            index++;
            Assert.That(index, Is.EqualTo(trace.Count), "Idle復帰後に余分な通知があります");

            AssertProgressContract(trace);
        }

        /// <summary>状態通知の段階、操作、完全パスが期待値と一致することを確かめる。</summary>
        private static void AssertStatus(FlowTraceEntry entry, SceneFlowPhase phase, SceneFlowOperation operation, string path)
        {
            Assert.That(entry.Kind, Is.EqualTo(FlowTraceKind.Status));
            Assert.That(entry.Status.Phase, Is.EqualTo(phase));
            Assert.That(entry.Status.Request.Operation, Is.EqualTo(operation));
            Assert.That(entry.Status.Request.Scene.Path, Is.EqualTo(path));
            AssertProjectRelativeScenePath(entry.Status.Request.Scene.Path);
        }

        /// <summary>Idle以外の進捗が有限、0以上1以下、単調非減少、完了時1であることを確かめる。</summary>
        private static void AssertProgressContract(IReadOnlyList<FlowTraceEntry> trace)
        {
            var previous = 0f;
            var completedProgress = -1f;
            for (var i = 0; i < trace.Count; i++)
            {
                if (trace[i].Kind != FlowTraceKind.Status || trace[i].Status.Phase == SceneFlowPhase.Idle) continue;

                var progress = trace[i].Status.Progress;
                Assert.That(float.IsNaN(progress), Is.False, "進捗がNaNです");
                Assert.That(float.IsInfinity(progress), Is.False, "進捗が無限値です");
                Assert.That(progress, Is.InRange(0f, 1f));
                Assert.That(progress, Is.GreaterThanOrEqualTo(previous), "進捗が後退しました");
                previous = progress;
                if (trace[i].Status.Phase == SceneFlowPhase.Completed) completedProgress = progress;
            }

            Assert.That(completedProgress, Is.EqualTo(1f).Within(0.000001f), "Completedの進捗が1ではありません");
        }

        /// <summary>指定した要素が状態通知かつ指定段階かを返す。</summary>
        private static bool IsStatusPhase(FlowTraceEntry entry, SceneFlowPhase phase) =>
            entry.Kind == FlowTraceKind.Status && entry.Status.Phase == phase;

        /// <summary>現在のBuild Profileから完全パスでSceneを読めることを確かめる。</summary>
        private static void AssertLoadable(string path)
        {
            Assert.That(Application.CanStreamedLevelBeLoaded(path), Is.True, $"PlayMode回帰を実行する前に有効なBuild ProfileへSceneを登録してください: {path}");
        }

        /// <summary>GUID解決後のSceneがAssetsまたはPackagesから始まる完全パスであることを確かめる。</summary>
        private static void AssertProjectRelativeScenePath(string path)
        {
            Assert.That(path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal), Is.True, $"Sceneのプロジェクト相対完全パスではありません: {path}");
            Assert.That(path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase), Is.True, $"Scene Assetではありません: {path}");
        }

        /// <summary>指定Sceneが読込済みで、期待する固有名のroot GameObjectを含むことを確かめる。</summary>
        private static void AssertLoadedRoot(string path, string expectedRootName)
        {
            var scene = SceneManager.GetSceneByPath(path);
            Assert.That(scene.IsValid(), Is.True, $"Sceneが見つかりません: {path}");
            Assert.That(scene.isLoaded, Is.True, $"Sceneが読込済みではありません: {path}");

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, expectedRootName, StringComparison.Ordinal)) return;
            }

            Assert.Fail($"固有root GameObjectがありません: {path} ({expectedRootName})");
        }

        /// <summary>指定Sceneが読込済み一覧に残っていないことを確かめる。</summary>
        private static void AssertNotLoaded(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            Assert.That(!scene.IsValid() || !scene.isLoaded, Is.True, $"Sceneが読込済みのままです: {path}");
        }

        /// <summary>状態変更と完了を1本の順序列へ記録する要素。</summary>
        private readonly struct FlowTraceEntry
        {
            private FlowTraceEntry(FlowTraceKind kind, SceneFlowStatus status, SceneFlowResult result)
            {
                Kind = kind;
                Status = status;
                Result = result;
            }

            /// <summary>記録した通知の種類。</summary>
            public FlowTraceKind Kind { get; }

            /// <summary>状態通知の値。完了通知では既定値。</summary>
            public SceneFlowStatus Status { get; }

            /// <summary>完了通知の値。状態通知では既定値。</summary>
            public SceneFlowResult Result { get; }

            /// <summary>状態通知から順序記録を作る。</summary>
            public static FlowTraceEntry FromStatus(SceneFlowStatus status) =>
                new FlowTraceEntry(FlowTraceKind.Status, status, default);

            /// <summary>完了通知から順序記録を作る。</summary>
            public static FlowTraceEntry FromFinished(SceneFlowResult result) =>
                new FlowTraceEntry(FlowTraceKind.Finished, default, result);
        }

        /// <summary>同じ順序列へ記録する通知の種類。</summary>
        private enum FlowTraceKind
        {
            Status,
            Finished,
        }
    }
}
