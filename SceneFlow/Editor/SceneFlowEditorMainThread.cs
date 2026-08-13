using UnityEditor;

namespace SceneFlow.Editor
{
    /// <summary>Play前のEditorコードでもSceneFlowServiceを安全に生成できるようメインスレッドを確定する。</summary>
    internal static class SceneFlowEditorMainThread
    {
        [InitializeOnLoadMethod]
        internal static void Bind() => SceneFlowMainThread.BindFromUnityCallback();
    }
}
