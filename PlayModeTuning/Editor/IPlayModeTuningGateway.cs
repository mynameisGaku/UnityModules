using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>Separates deterministic session rules from Unity object resolution and mutation.</summary>
    internal interface IPlayModeTuningGateway
    {
        PlayModeTuningEnvironment GetEnvironment();
        PlayModeTuningGatewayResult ResolveSelections(IReadOnlyList<PlayModeTuningPropertySelection> selections);
        PlayModeTuningGatewayResult Capture(IReadOnlyList<PlayModeTuningPropertyRecord> properties);
        PlayModeTuningMutationResult Apply(IReadOnlyList<PlayModeTuningWrite> writes);
        PlayModeTuningMutationResult MarkScenesDirty(IReadOnlyList<string> scenePaths);
    }
}
