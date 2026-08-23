using System;
using Microsoft.Win32.SafeHandles;

namespace BuildAssistant.Editor
{
    internal sealed class DirectoryIdentityLease : IDisposable
    {
        private SafeFileHandle handle;

        internal DirectoryIdentityLease(string canonicalPath, SafeFileHandle handle = null)
        {
            CanonicalPath = canonicalPath ?? string.Empty;
            this.handle = handle;
        }

        internal string CanonicalPath { get; }

        public void Dispose()
        {
            var ownedHandle = handle;
            handle = null;
            if (ownedHandle == null)
                return;
            try
            {
                ownedHandle.Dispose();
            }
            catch
            {
            }
        }
    }
}
