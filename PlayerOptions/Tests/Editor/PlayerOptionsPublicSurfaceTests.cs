// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>公開API最小面、readonly値、assembly依存、main-thread初期化順を固定する。</summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class PlayerOptionsPublicSurfaceTests
    {
        [Test]
        public void RuntimeAssembly_ExportsOnlyDocumentedPublicTypes()
        {
            var exported = typeof(PlayerOptionsService).Assembly
                .GetExportedTypes()
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expected = new[]
            {
                "PlayerOptions.IPlayerOptionsStorage",
                "PlayerOptions.PlayerDisplayOptions",
                "PlayerOptions.PlayerOptionsError",
                "PlayerOptions.PlayerOptionsField",
                "PlayerOptions.PlayerOptionsResult",
                "PlayerOptions.PlayerOptionsService",
                "PlayerOptions.PlayerOptionsState",
                "PlayerOptions.PlayerOptionsWarning",
                "PlayerOptions.PlayerPrefsPlayerOptionsStorage",
                "PlayerOptions.PlayerQualityOptions",
            };

            Assert.That(exported, Is.EqualTo(expected));
            Assert.That(typeof(PlayerOptionsDocument).IsPublic, Is.False);
            Assert.That(typeof(PlayerOptionsValidator).IsPublic, Is.False);
            Assert.That(typeof(IPlayerOptionsRuntime).IsPublic, Is.False);
        }

        [Test]
        public void Service_PublicMembersMatchFrozenVersionOneContract()
        {
            var type = typeof(PlayerOptionsService);
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            var methods = type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var stateChanged = type.GetEvent("StateChanged");
            var createDefault = type.GetMethod(
                "CreateDefault",
                BindingFlags.Public | BindingFlags.Static);
            var fieldType = type.Assembly.GetType("PlayerOptions.PlayerOptionsField");

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(PlayerOptionsState), typeof(IPlayerOptionsStorage) }));
            Assert.That(methods, Is.EqualTo(new[]
            {
                "Apply",
                "CreateDefault",
                "Load",
                "Save",
                "SetState",
            }));
            Assert.That(type.GetProperty("Defaults")?.CanWrite, Is.False);
            Assert.That(type.GetProperty("State")?.CanWrite, Is.False);
            Assert.That(stateChanged?.EventHandlerType, Is.EqualTo(typeof(Action<PlayerOptionsState>)));
            Assert.That(createDefault, Is.Not.Null);
            Assert.That(createDefault.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(createDefault.GetParameters()[0].HasDefaultValue, Is.True);
            Assert.That(
                createDefault.GetParameters()[0].DefaultValue,
                Is.EqualTo(PlayerOptionsService.DefaultStorageKey));
            Assert.That(
                PlayerOptionsService.DefaultStorageKey,
                Is.EqualTo("com.studiogaku.player-options.document"));
            Assert.That(fieldType, Is.Not.Null);
            Assert.That(type.Assembly.GetType("PlayerOptions.PlayerOptionsResult")
                ?.GetProperty("AffectedFields")?.PropertyType, Is.EqualTo(fieldType));
            Assert.That(type.Assembly.GetType("PlayerOptions.PlayerOptionsResult")
                ?.GetProperty("RollbackFailedFields")?.PropertyType, Is.EqualTo(fieldType));
            Assert.That(type.Assembly.GetType("PlayerOptions.PlayerOptionsResult")
                ?.GetProperty("OutcomeUnknownFields")?.PropertyType, Is.EqualTo(fieldType));
        }

        [Test]
        public void PublicValueTypesHaveNoMutableInstanceFields()
        {
            var valueTypes = new[]
            {
                typeof(PlayerDisplayOptions),
                typeof(PlayerQualityOptions),
                typeof(PlayerOptionsState),
                typeof(PlayerOptionsResult),
            };

            for (var typeIndex = 0; typeIndex < valueTypes.Length; typeIndex++)
            {
                var type = valueTypes[typeIndex];
                var fields = type.GetFields(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);
                Assert.That(type.IsValueType, Is.True, type.FullName);
                Assert.That(
                    fields.All(field => field.IsInitOnly),
                    Is.True,
                    $"{type.FullName} contains a mutable instance field.");
                Assert.That(
                    type.GetFields(BindingFlags.Public | BindingFlags.Instance),
                    Is.Empty,
                    $"{type.FullName} exposes an instance field.");
            }
        }

        [Test]
        public void PublicEnumsContainFrozenErrorsAndResolutionOutcomeWarning()
        {
            Assert.That(
                Enum.GetNames(typeof(PlayerOptionsError)),
                Is.EqualTo(new[]
                {
                    "None",
                    "InvalidOptions",
                    "CorruptData",
                    "UnsupportedSchemaVersion",
                    "StorageReadFailed",
                    "StorageWriteFailed",
                    "SerializationFailed",
                    "ApplyFailed",
                    "RollbackFailed",
                    "RuntimeUnavailable",
                    "MainThreadRequired",
                    "Busy",
                    "MigrationFailed",
                }));
            Assert.That(
                Enum.GetNames(typeof(PlayerOptionsWarning)),
                Is.EqualTo(new[]
                {
                    "None",
                    "DisplayFallbackUsed",
                    "QualityIndexAdjusted",
                    "QualityFallbackUsed",
                    "RefreshRateNormalized",
                    "TargetFrameRateMayBeOverridden",
                    "ResolutionChangeDeferred",
                    "ResolutionOutcomeUnknown",
                }),
                "Screen.SetResolution throw後の診断warningを公開してください。");

            var fieldType = typeof(PlayerOptionsService).Assembly.GetType(
                "PlayerOptions.PlayerOptionsField");
            Assert.That(fieldType, Is.Not.Null);
            Assert.That(fieldType.IsDefined(typeof(FlagsAttribute), inherit: false), Is.True);
            Assert.That(
                typeof(PlayerOptionsWarning).IsDefined(typeof(FlagsAttribute), inherit: false),
                Is.True);
            Assert.That(
                Enum.GetNames(fieldType),
                Is.EqualTo(new[]
                {
                    "None",
                    "Display",
                    "TargetFrameRate",
                    "MasterVolume",
                    "Quality",
                }));
            Assert.That(Convert.ToInt32(Enum.Parse(fieldType, "None")), Is.EqualTo(0));
            Assert.That(Convert.ToInt32(Enum.Parse(fieldType, "Display")), Is.EqualTo(1 << 0));
            Assert.That(Convert.ToInt32(Enum.Parse(fieldType, "TargetFrameRate")), Is.EqualTo(1 << 1));
            Assert.That(Convert.ToInt32(Enum.Parse(fieldType, "MasterVolume")), Is.EqualTo(1 << 2));
            Assert.That(Convert.ToInt32(Enum.Parse(fieldType, "Quality")), Is.EqualTo(1 << 3));
            Assert.That(
                Convert.ToInt32(PlayerOptionsWarning.ResolutionChangeDeferred),
                Is.EqualTo(1 << 5));
            Assert.That(
                Convert.ToInt32(Enum.Parse(typeof(PlayerOptionsWarning), "ResolutionOutcomeUnknown")),
                Is.EqualTo(1 << 6));
            Assert.That(
                Convert.ToInt32(Enum.Parse(typeof(PlayerOptionsError), "MigrationFailed")),
                Is.EqualTo(12));
        }

        [Test]
        public void RuntimeAssembly_HasNoEditorOrSiblingModuleDependency()
        {
            var references = typeof(PlayerOptionsService).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("UnityEditor"));
            Assert.That(references, Does.Not.Contain("SaveSystem.Runtime"));
            Assert.That(references, Does.Not.Contain("InputDeviceDisplay.Runtime"));
            Assert.That(references, Does.Not.Contain("InputGate.Runtime"));
            Assert.That(references, Does.Not.Contain("ModuleInstaller.Editor"));
            Assert.That(references, Does.Not.Contain("BuildGuard.Editor"));
        }

        [Test]
        public void SubsystemRegistrationCallback_WhenInvoked_BindsCurrentThreadForConstruction()
        {
            var subsystemRegistration = typeof(PlayerOptionsMainThread)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method =>
                {
                    var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    return attribute != null &&
                           attribute.loadType == RuntimeInitializeLoadType.SubsystemRegistration;
                });

            try
            {
                subsystemRegistration.Invoke(null, null);

                Assert.That(
                    PlayerOptionsMainThread.IsCurrent,
                    Is.True,
                    "SubsystemRegistration callback itself must bind the main thread.");
                var service = PlayerOptionsService.CreateDefault(
                    $"com.studiogaku.player-options.tests.surface.{Guid.NewGuid():N}");
                Assert.That(service, Is.Not.Null);
                Assert.That(
                    () => new PlayerOptionsService(
                        service.Defaults,
                        new FakePlayerOptionsStorage()),
                    Throws.Nothing);
            }
            finally
            {
                PlayerOptionsMainThread.BindForTests();
            }
        }
    }
}
