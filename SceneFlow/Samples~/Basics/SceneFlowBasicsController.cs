using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneFlow.Samples
{
    /// <summary>
    /// Single読込、Additive読込、有効Scene変更、アンロードを順番に試す操作画面。
    /// Bootstrapの置き換え後も操作を続けるため、このコンポーネントを持つ物体だけを維持する。
    /// </summary>
    [AddComponentMenu("StudioGaku/Scene Flow Basics Controller")]
    public sealed class SceneFlowBasicsController : MonoBehaviour
    {
        private const string TargetAFileName = "SceneFlowBasicsTargetA.unity";
        private const string TargetBFileName = "SceneFlowBasicsTargetB.unity";

        [SerializeField, TextArea] private string _lastResult = "上から順にボタンを押してください。";

        private SceneFlowService _sceneFlow;
        private SceneReference _targetA;
        private SceneReference _targetB;
        private SceneFlowStatus _status;

        /// <summary>現在のScene配置から対象パスを作り、Scene操作サービスを所有する。</summary>
        private void Awake()
        {
            var bootstrapPath = gameObject.scene.path.Replace('\\', '/');
            var sampleDirectory = Path.GetDirectoryName(bootstrapPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sampleDirectory))
            {
                _lastResult = "Bootstrap Sceneの保存先を解決できません。Setupをやり直してください。";
                enabled = false;
                return;
            }

            _targetA = new SceneReference(sampleDirectory + "/" + TargetAFileName);
            _targetB = new SceneReference(sampleDirectory + "/" + TargetBFileName);
            _sceneFlow = new SceneFlowService();
            _sceneFlow.StatusChanged += HandleStatusChanged;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>通知購読を解除する。</summary>
        private void OnDestroy()
        {
            if (_sceneFlow != null) _sceneFlow.StatusChanged -= HandleStatusChanged;
        }

        /// <summary>操作、進捗、読込済みSceneをGame Viewへ表示する。</summary>
        private void OnGUI()
        {
            var width = Mathf.Max(320f, Mathf.Min(620f, Screen.width - 48f));
            var height = Mathf.Max(440f, Screen.height - 48f);
            var previousEnabled = GUI.enabled;

            GUILayout.BeginArea(new Rect(24f, 24f, width, height), GUI.skin.box);
            GUILayout.Label("Scene Flow Basics", GUI.skin.box);
            GUILayout.Space(8f);
            GUILayout.Label("1 → 4 の順番で、Scene状態の変化を確認します。");
            GUILayout.Label($"段階: {_status.Phase} / 進捗: {_status.Progress:P0}");
            GUILayout.Label($"有効Scene: {SceneManager.GetActiveScene().path}");
            GUILayout.Space(8f);

            GUI.enabled = previousEnabled && _sceneFlow != null && !_sceneFlow.IsBusy;
            if (GUILayout.Button("1. Target AをSingle読込")) LoadTargetASingle();
            if (GUILayout.Button("2. Target BをAdditive読込")) LoadTargetBAdditive();
            if (GUILayout.Button("3. Target Bを有効Sceneにする")) SetTargetBActive();
            if (GUILayout.Button("4. Target Aをアンロード")) UnloadTargetA();
            GUI.enabled = previousEnabled;

            GUILayout.Space(12f);
            GUILayout.Label("最後の結果:");
            GUILayout.Label(_lastResult);
            GUILayout.Space(12f);
            GUILayout.Label("読込済みScene:");
            DrawLoadedScenes();
            GUILayout.EndArea();
        }

        /// <summary>Target AをSingleで読み込み、Bootstrap Sceneを置き換える。</summary>
        [ContextMenu("Scene Flow Basics/1. Target AをSingle読込")]
        private void LoadTargetASingle()
        {
            Execute(SceneFlowRequest.LoadSingle(_targetA));
        }

        /// <summary>Target BをAdditiveで追加し、Target Aを残す。</summary>
        [ContextMenu("Scene Flow Basics/2. Target BをAdditive読込")]
        private void LoadTargetBAdditive()
        {
            Execute(SceneFlowRequest.LoadAdditive(_targetB));
        }

        /// <summary>読込済みのTarget Bを有効Sceneへ変更する。</summary>
        [ContextMenu("Scene Flow Basics/3. Target Bを有効Sceneにする")]
        private void SetTargetBActive()
        {
            Execute(SceneFlowRequest.SetActive(_targetB));
        }

        /// <summary>有効SceneではなくなったTarget Aをアンロードする。</summary>
        [ContextMenu("Scene Flow Basics/4. Target Aをアンロード")]
        private void UnloadTargetA()
        {
            Execute(SceneFlowRequest.Unload(_targetA));
        }

        /// <summary>1件の要求を実行し、例外をGame Viewへ残す。</summary>
        /// <param name="request">操作種類と対象Scene。</param>
        private async void Execute(SceneFlowRequest request)
        {
            if (!Application.isPlaying || _sceneFlow == null)
            {
                _lastResult = "Play Modeで操作してください。";
                return;
            }

            try
            {
                var result = await _sceneFlow.ExecuteAsync(request);
                var outcome = result.IsSuccess ? "成功" : $"失敗: {result.Error}";
                _lastResult = $"{result.Request.Operation} / {outcome}\n{result.Message}";
                Debug.Log($"[Scene Flow Basics] {_lastResult}", this);
            }
            catch (Exception exception)
            {
                _lastResult = $"予期しない失敗: {exception.Message}";
                Debug.LogException(exception, this);
            }
        }

        /// <summary>進捗通知を画面表示用の状態へ反映する。</summary>
        /// <param name="status">現在の処理段階と進捗。</param>
        private void HandleStatusChanged(SceneFlowStatus status)
        {
            _status = status;
        }

        /// <summary>完全なSceneパスと有効Sceneの印を一覧表示する。</summary>
        private static void DrawLoadedScenes()
        {
            var activeScene = SceneManager.GetActiveScene();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var activeMark = scene == activeScene ? " [Active]" : string.Empty;
                GUILayout.Label($"- {scene.path}{activeMark}");
            }
        }
    }
}
