using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TimeControl.Tests
{
    /// <summary>配布後に利用者へ公開する型とController契約を反射で固定する。</summary>
    public sealed class TimeControlPublicApiTests
    {
        /// <summary>runtime assemblyから公開される型を4種類だけに固定する。</summary>
        [Test]
        public void RuntimeAssembly_ExportsExactlyFourPublicTypes()
        {
            var exported = typeof(TimeControlController).Assembly.GetExportedTypes().OrderBy(type => type.FullName).ToArray();
            var expected = new[]
            {
                typeof(TimeControlController),
                typeof(TimeControlError),
                typeof(TimeControlStatus),
                typeof(TimeScaleLease),
            }.OrderBy(type => type.FullName).ToArray();

            Assert.That(exported, Is.EqualTo(expected));
        }

        /// <summary>Controllerの公開宣言面に取得、状態、通知、2つの上限だけが存在する。</summary>
        [Test]
        public void Controller_DeclaredPublicSurface_MatchesContract()
        {
            const BindingFlags declaredPublic = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var methods = typeof(TimeControlController).GetMethods(declaredPublic)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            var properties = typeof(TimeControlController).GetProperties(declaredPublic).Select(property => property.Name).OrderBy(name => name).ToArray();
            var events = typeof(TimeControlController).GetEvents(declaredPublic).Select(eventInfo => eventInfo.Name).ToArray();
            var fields = typeof(TimeControlController).GetFields(declaredPublic).Select(field => field.Name).OrderBy(name => name).ToArray();

            Assert.That(methods, Is.EqualTo(new[] { "TryAcquire" }));
            Assert.That(properties, Is.EqualTo(new[] { "IsControlling", "Status" }));
            Assert.That(events, Is.EqualTo(new[] { "StatusChanged" }));
            Assert.That(fields, Is.EqualTo(new[] { "MaximumEffectiveTimeScale", "MaximumMultiplier" }));
        }

        /// <summary>取得権が倍率、活動状態、Disposeだけを公開することを固定する。</summary>
        [Test]
        public void Lease_DeclaredPublicSurface_MatchesContract()
        {
            const BindingFlags declaredPublic = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var methods = typeof(TimeScaleLease).GetMethods(declaredPublic)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            var properties = typeof(TimeScaleLease).GetProperties(declaredPublic).Select(property => property.Name).OrderBy(name => name).ToArray();

            Assert.That(methods, Is.EqualTo(new[] { "Dispose" }));
            Assert.That(properties, Is.EqualTo(new[] { "IsActive", "Multiplier" }));
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(TimeScaleLease)), Is.True);
        }

        /// <summary>利用者が分岐に使う失敗理由の名前と順序を固定する。</summary>
        [Test]
        public void Error_ValuesMatchContract()
        {
            var names = Enum.GetNames(typeof(TimeControlError));

            Assert.That(names, Is.EqualTo(new[]
            {
                "None",
                "InvalidMultiplier",
                "EffectiveTimeScaleOutOfRange",
                "MainThreadRequired",
                "Busy",
                "OwnerAlreadyExists",
                "ControllerUnavailable",
                "ApplicationExiting",
                "ExternalTimeScaleChanged",
                "TimeScaleWriteFailed",
            }));
        }
    }
}
