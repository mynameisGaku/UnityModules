using System.Linq;
using NUnit.Framework;

namespace AudioControl.Tests
{
    public sealed class AudioControlPublicSurfaceTests
    {
        [Test]
        public void RuntimeAssembly_ExportsExactlyFourContractTypes()
        {
            var names = typeof(AudioControlController).Assembly.GetExportedTypes()
                .Select(type => type.FullName)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(names, Is.EqualTo(new[]
            {
                "AudioControl.AudioControlController",
                "AudioControl.AudioControlError",
                "AudioControl.AudioControlHandle",
                "AudioControl.AudioPlayRequest"
            }));
        }
    }
}
