// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace ModuleInstaller.Editor
{
    internal sealed class UnityModulePackageClient : IModulePackageClient
    {
        public IModuleInstallRequest AddAndRemove(IReadOnlyList<string> packageUrls)
        {
            if (packageUrls == null)
            {
                throw new ArgumentNullException(nameof(packageUrls));
            }

            var urls = new string[packageUrls.Count];
            for (var index = 0; index < packageUrls.Count; index++)
            {
                urls[index] = packageUrls[index];
            }

            return new UnityModuleInstallRequest(Client.AddAndRemove(urls, Array.Empty<string>()));
        }

        private sealed class UnityModuleInstallRequest : IModuleInstallRequest
        {
            private readonly AddAndRemoveRequest _request;

            internal UnityModuleInstallRequest(AddAndRemoveRequest request)
            {
                _request = request ?? throw new ArgumentNullException(nameof(request));
            }

            public bool IsCompleted => _request.IsCompleted;
            public bool Succeeded => _request.Status == StatusCode.Success;
            public string ErrorMessage => _request.Error?.message ?? "Unknown Package Manager error.";
        }
    }
}
