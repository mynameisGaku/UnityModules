using System;

namespace AssetImportAudit.Editor
{
    internal static class AssetImportAuditTextureSize
    {
        internal static int[] CreateValues()
        {
            return new[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
        }

        internal static string[] CreateLabels()
        {
            return new[] { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192", "16384" };
        }

        internal static void Validate(int value, string parameterName)
        {
            switch (value)
            {
                case 32:
                case 64:
                case 128:
                case 256:
                case 512:
                case 1024:
                case 2048:
                case 4096:
                case 8192:
                case 16384:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(parameterName, value, "Max texture size must be a Unity importer preset from 32 through 16384.");
            }
        }
    }
}
