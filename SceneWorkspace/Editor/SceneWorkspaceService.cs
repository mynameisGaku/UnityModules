namespace SceneWorkspace.Editor
{
    /// <summary>Provides the public editor-only entry points for capture, preview, and one confirmed workspace switch.</summary>
    public static class SceneWorkspaceService
    {
        /// <summary>Captures a clean, saved, valid current scene-manager setup without changing it.</summary>
        public static SceneWorkspaceCaptureResult CaptureCurrentSetup()
        {
            return CreateOperations().CaptureCurrentSetup();
        }

        /// <summary>Creates an immutable single-use plan without opening, closing, loading, or saving scenes.</summary>
        public static SceneWorkspacePlan Preview(SceneWorkspaceProfile profile)
        {
            return CreateOperations().Preview(profile);
        }

        /// <summary>Revalidates and applies exactly one previewed plan, then verifies it or reports recovery separately.</summary>
        public static SceneWorkspaceApplyResult Apply(SceneWorkspacePlan plan)
        {
            return CreateOperations().Apply(plan);
        }

        private static SceneWorkspaceOperations CreateOperations()
        {
            return new SceneWorkspaceOperations(new UnitySceneWorkspaceGateway());
        }
    }
}
