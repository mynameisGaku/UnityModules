using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneFlow.Samples
{
    /// <summary>Target Sceneの名前と、現在有効なSceneかどうかをGame Viewへ表示する。</summary>
    [AddComponentMenu("StudioGaku/Scene Flow Basics Marker")]
    public sealed class SceneFlowBasicsMarker : MonoBehaviour
    {
        [SerializeField] private string _title = "Target";
        [SerializeField, TextArea] private string _description = "Scene Flowの対象Sceneです。";
        [SerializeField] private Color _backgroundColor = new Color(0.08f, 0.16f, 0.24f, 1f);
        [SerializeField] private Camera _sceneCamera;

        /// <summary>有効Sceneの変更を監視し、最初のCamera状態も同期する。</summary>
        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SynchronizeCamera();
        }

        /// <summary>有効Sceneの変更監視を解除する。</summary>
        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        /// <summary>背景色とSceneの役割を表示する。</summary>
        private void OnGUI()
        {
            var scene = gameObject.scene;
            if (scene != SceneManager.GetActiveScene()) return;

            var panelWidth = Mathf.Max(300f, Mathf.Min(520f, Screen.width - 48f));
            var panelX = Mathf.Max(24f, Screen.width - panelWidth - 24f);
            var previousBackgroundColor = GUI.backgroundColor;

            GUI.backgroundColor = _backgroundColor;
            GUILayout.BeginArea(new Rect(panelX, 24f, panelWidth, 170f), GUI.skin.box);
            GUILayout.Label(_title, GUI.skin.box);
            GUILayout.Label(_description);
            GUILayout.Space(8f);
            GUILayout.Label($"Scene: {scene.path}");
            GUILayout.Label("Active: Yes");
            GUILayout.EndArea();
            GUI.backgroundColor = previousBackgroundColor;
        }

        /// <summary>有効Sceneが変わった直後に自SceneのCamera状態を同期する。</summary>
        /// <param name="previous">変更前の有効Scene。</param>
        /// <param name="current">変更後の有効Scene。</param>
        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            SynchronizeCamera();
        }

        /// <summary>自Sceneが有効な間だけ、このSceneのCameraを有効にする。</summary>
        private void SynchronizeCamera()
        {
            if (_sceneCamera != null) _sceneCamera.enabled = gameObject.scene == SceneManager.GetActiveScene();
        }
    }
}
