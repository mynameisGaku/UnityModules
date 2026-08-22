// SPDX-License-Identifier: MIT

namespace ProjectSetup.Editor
{
    internal interface IProjectSetupEnvironment
    {
        bool IsAvailable { get; }
        ProjectSetupSnapshot Capture();
        void Apply(ProjectSetupProfile profile);
        void Apply(ProjectSetupSnapshot snapshot);
    }

    internal interface IProjectSetupBackupStore
    {
        bool Exists { get; }
        void Save(ProjectSetupSnapshot snapshot);
        bool TryLoad(out ProjectSetupSnapshot snapshot, out string error);
    }
}
