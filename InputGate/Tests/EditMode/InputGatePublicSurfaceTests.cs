using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace InputGate.Tests
{
    /// <summary>Runtime assemblyの公開型と公開memberが意図した小さい契約に留まることを検証する。</summary>
    public sealed class InputGatePublicSurfaceTests
    {
        /// <summary>Runtime assemblyが公開する型を4種類だけに固定する。</summary>
        [Test]
        public void RuntimeAssembly_ExportsExactlyFourTypes()
        {
            var exported = typeof(InputGateController).Assembly.GetExportedTypes()
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                "InputGate.InputGateController",
                "InputGate.InputGateError",
                "InputGate.InputGateLease",
                "InputGate.InputGateStatus",
            }));
        }

        /// <summary>Controllerの公開操作を状態参照、通知、取得だけに固定する。</summary>
        [Test]
        public void Controller_ExposesOnlyBoundedDeclaredMembers()
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var members = typeof(InputGateController).GetMembers(Flags)
                .Where(member => member.MemberType == MemberTypes.Method ||
                                 member.MemberType == MemberTypes.Property ||
                                 member.MemberType == MemberTypes.Event)
                .Select(member => member.MemberType + ":" + member.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(members, Is.EqualTo(new[]
            {
                "Event:StatusChanged",
                "Method:add_StatusChanged",
                "Method:get_IsBlocking",
                "Method:get_Status",
                "Method:remove_StatusChanged",
                "Method:TryAcquire",
                "Property:IsBlocking",
                "Property:Status",
            }.OrderBy(name => name, StringComparer.Ordinal).ToArray()));
        }
    }
}
