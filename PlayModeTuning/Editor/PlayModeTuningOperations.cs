using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayModeTuning.Editor
{
    /// <summary>Owns the deterministic session state machine and delegates only engine reads and writes.</summary>
    internal sealed class PlayModeTuningOperations
    {
        internal const int MaximumComponents = 32;
        internal const int MaximumProperties = 256;
        internal const int MaximumPayloadBytes = 256 * 1024;

        private readonly IPlayModeTuningGateway gateway;
        private readonly IPlayModeTuningSessionStore store;
        private readonly PlayModeTuningPlanRegistry registry;
        private readonly string domainToken;
        private bool applyInProgress;

        internal PlayModeTuningOperations(IPlayModeTuningGateway gateway, IPlayModeTuningSessionStore store, PlayModeTuningPlanRegistry registry, string domainToken)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.domainToken = string.IsNullOrEmpty(domainToken) ? throw new ArgumentException("A domain token is required.", nameof(domainToken)) : domainToken;
        }

        internal PlayModeTuningStartResult Start(IReadOnlyList<PlayModeTuningPropertySelection> selections)
        {
            var current = LoadCurrent(out var storedError, out var storedMessage);
            if (storedError != PlayModeTuningError.None)
                return StartFailure(storedError, storedMessage);
            if (current != null && !IsTerminal((PlayModeTuningPhase)current.phase))
                return StartFailure(PlayModeTuningError.WrongPhase, "Discard or finish the current session before starting another one.");
            var environmentError = ValidateEditEnvironment(gateway.GetEnvironment());
            if (environmentError.Error != PlayModeTuningError.None)
                return StartFailure(environmentError.Error, environmentError.Message);
            if (selections == null || selections.Count == 0)
                return StartFailure(PlayModeTuningError.InvalidSelection, "Select at least one component property.");
            if (selections.Count > MaximumProperties)
                return StartFailure(PlayModeTuningError.TooManyProperties, "A session supports at most 256 selected properties.");

            var copiedSelections = selections.ToArray();
            var resolved = gateway.ResolveSelections(copiedSelections);
            if (!resolved.Succeeded)
                return StartFailure(resolved.Error, resolved.Message);
            if (resolved.Snapshot.Properties.Count == 0)
                return StartFailure(PlayModeTuningError.InvalidSelection, "No supported properties were resolved.");
            if (resolved.Snapshot.Properties.Count > MaximumProperties)
                return StartFailure(PlayModeTuningError.TooManyProperties, "A session supports at most 256 selected properties.");
            if (resolved.Snapshot.Components.Count > MaximumComponents)
                return StartFailure(PlayModeTuningError.TooManyComponents, "A session supports at most 32 components.");
            var baselineBytes = ComputePayloadBytes(resolved.Snapshot.Properties, null);
            if (baselineBytes > MaximumPayloadBytes)
                return StartFailure(PlayModeTuningError.PayloadTooLarge, "The baseline exceeds the 256 KiB session payload limit.");

            var environment = gateway.GetEnvironment();
            var session = new PlayModeTuningPersistedSession
            {
                sessionId = Guid.NewGuid().ToString("N"),
                phase = (int)PlayModeTuningPhase.Armed,
                error = (int)PlayModeTuningError.None,
                message = "The session is armed. Enter Play Mode, then capture manually.",
                domainReloadDisabled = environment.DomainReloadDisabled,
                startDomainToken = domainToken,
                properties = resolved.Snapshot.Properties.Select(CreateBaselineRecord).ToList(),
                components = resolved.Snapshot.Components.Select(item => new PlayModeTuningComponentRecord
                {
                    componentKey = item.ComponentKey,
                    scenePath = item.ScenePath,
                    baselineUnselectedFingerprint = item.UnselectedFingerprint
                }).ToList()
            };
            store.Save(session);
            return new PlayModeTuningStartResult(PlayModeTuningError.None, string.Empty, ToPublicSession(session));
        }

        internal PlayModeTuningSession GetCurrentSession()
        {
            var session = LoadCurrent(out _, out _);
            return session == null ? IdleSession() : ToPublicSession(session);
        }

        internal PlayModeTuningCaptureResult CaptureDuringPlay(Guid sessionId)
        {
            var session = LoadExact(sessionId, out var loadError, out var loadMessage);
            if (session == null)
                return CaptureFailure(loadError, loadMessage);
            if ((PlayModeTuningPhase)session.phase != PlayModeTuningPhase.Capturable)
                return CaptureFailure(PlayModeTuningError.WrongPhase, "Capture is available only after the armed session enters Play Mode.", session);
            var environment = gateway.GetEnvironment();
            if (!environment.Playing)
                return CaptureFailure(PlayModeTuningError.PlayModeRequired, "Capture During Play requires active Play Mode.", session);
            if (environment.SceneReloadDisabled)
                return CaptureFailure(MarkStale(session, PlayModeTuningError.DisableSceneReloadUnsupported, "Disable Scene Reload is not supported."));

            var captured = gateway.Capture(session.properties);
            if (!captured.Succeeded)
                return CaptureFailure(MarkStale(session, captured.Error, captured.Message));
            if (!RefreshTargetNames(session, captured.Snapshot))
                return CaptureFailure(MarkStale(session, PlayModeTuningError.IdentityMismatch, "A selected property identity changed during Play Mode."));
            if (!HasSameIdentity(session, captured.Snapshot))
                return CaptureFailure(MarkStale(session, PlayModeTuningError.IdentityMismatch, "A selected target or property identity changed during Play Mode."));
            var payloadBytes = ComputePayloadBytes(captured.Snapshot.Properties, session.properties);
            if (payloadBytes > MaximumPayloadBytes)
                return CaptureFailure(MarkStale(session, PlayModeTuningError.PayloadTooLarge, "The baseline and captured values exceed the 256 KiB session payload limit."));

            var capturedByKey = captured.Snapshot.Properties.ToDictionary(item => item.Record.PropertyKey, StringComparer.Ordinal);
            foreach (var property in session.properties)
            {
                var value = capturedByKey[property.PropertyKey].Value;
                property.capturedKind = (int)value.Kind;
                property.capturedPayload = value.Payload;
                property.capturedDisplay = value.Display;
            }
            session.phase = (int)PlayModeTuningPhase.Captured;
            session.error = (int)PlayModeTuningError.None;
            session.message = "The selected values were captured. Exit Play Mode before previewing.";
            store.Save(session);
            return new PlayModeTuningCaptureResult(PlayModeTuningError.None, string.Empty, ToPublicSession(session), session.properties.Count, payloadBytes);
        }

        internal PlayModeTuningPlan PreviewAfterPlay(Guid sessionId)
        {
            var session = LoadExact(sessionId, out var loadError, out var loadMessage);
            if (session == null)
                return PlayModeTuningPlanner.Failure(loadError, loadMessage, sessionId);
            if ((PlayModeTuningPhase)session.phase != PlayModeTuningPhase.ReadyToPreview)
                return PlayModeTuningPlanner.Failure(PlayModeTuningError.WrongPhase, "Preview is available only after a manual capture and Play Mode exit.", sessionId);
            var environmentError = ValidateEditEnvironment(gateway.GetEnvironment());
            if (environmentError.Error != PlayModeTuningError.None)
                return PlayModeTuningPlanner.Failure(environmentError.Error, environmentError.Message, sessionId);

            var current = gateway.Capture(session.properties);
            if (!current.Succeeded)
            {
                MarkStale(session, current.Error, current.Message);
                return PlayModeTuningPlanner.Failure(current.Error, current.Message, sessionId);
            }
            if (!RefreshTargetNames(session, current.Snapshot))
            {
                MarkStale(session, PlayModeTuningError.IdentityMismatch, "A selected property identity changed before preview.");
                return PlayModeTuningPlanner.Failure(PlayModeTuningError.IdentityMismatch, session.message, sessionId);
            }
            if (!MatchesBaseline(session, current.Snapshot))
            {
                MarkStale(session, PlayModeTuningError.StaleSession, "Edit Mode values or unselected top-level properties changed after the session was armed.");
                return PlayModeTuningPlanner.Failure(PlayModeTuningError.StaleSession, session.message, sessionId);
            }

            var nonce = Guid.NewGuid();
            var plan = PlayModeTuningPlanner.Create(session, nonce);
            if (!plan.IsReady)
            {
                session.phase = (int)PlayModeTuningPhase.Completed;
                session.error = (int)plan.Error;
                session.message = plan.Message;
                store.Save(session);
                return plan;
            }
            registry.Register(plan);
            session.phase = (int)PlayModeTuningPhase.Previewed;
            session.error = (int)PlayModeTuningError.None;
            session.message = "Review the exact preview, confirm it, then apply this plan once.";
            session.planNonce = nonce.ToString("N");
            session.planRevision = plan.Revision;
            session.planDomainToken = domainToken;
            session.planConsumed = false;
            store.Save(session);
            return plan;
        }

        internal PlayModeTuningApplyResult Apply(PlayModeTuningPlan plan)
        {
            var consumeError = registry.TryConsume(plan);
            if (consumeError != PlayModeTuningError.None)
                return ApplyFailure(false, consumeError, consumeError == PlayModeTuningError.PlanAlreadyConsumed ? "The plan was already consumed." : "The plan object is stale or was not created by this domain.");
            if (applyInProgress)
                return ApplyFailure(false, PlayModeTuningError.ApplyInProgress, "Another apply operation is already running.");

            var session = LoadExact(plan.SessionId, out var loadError, out var loadMessage);
            if (session == null)
                return ApplyFailure(false, loadError, loadMessage);
            session.planConsumed = true;
            store.Save(session);
            if ((PlayModeTuningPhase)session.phase != PlayModeTuningPhase.Previewed ||
                !StringComparer.Ordinal.Equals(session.planNonce, plan.Nonce.ToString("N")) ||
                !StringComparer.Ordinal.Equals(session.planRevision, plan.Revision) ||
                !StringComparer.Ordinal.Equals(session.planDomainToken, domainToken))
                return ApplyStale(session, PlayModeTuningError.StalePlan, "The stored session no longer matches the exact previewed plan.");
            var recomputedPlan = PlayModeTuningPlanner.Create(session, plan.Nonce);
            if (!recomputedPlan.IsReady || !StringComparer.Ordinal.Equals(recomputedPlan.Revision, plan.Revision))
                return ApplyStale(session, PlayModeTuningError.StalePlan, "The stored values or identity fields no longer match the confirmed plan.");

            var environmentError = ValidateEditEnvironment(gateway.GetEnvironment());
            if (environmentError.Error != PlayModeTuningError.None)
                return ApplyStale(session, environmentError.Error, environmentError.Message);
            var before = gateway.Capture(session.properties);
            if (!before.Succeeded)
                return ApplyStale(session, before.Error, before.Message);
            if (!MatchesBaseline(session, before.Snapshot))
                return ApplyStale(session, PlayModeTuningError.StaleSession, "The Edit Mode target state changed after preview.");

            applyInProgress = true;
            try
            {
                var writes = CreateCapturedWrites(session);
                var mutation = gateway.Apply(writes);
                if (!mutation.Succeeded)
                    return RollbackAfterFailure(session, before.Snapshot, mutation.Error, mutation.Message);
                var after = gateway.Capture(session.properties);
                if (!after.Succeeded)
                    return RollbackAfterFailure(session, before.Snapshot, PlayModeTuningError.VerificationFailed, after.Message);
                if (!MatchesCaptured(session, after.Snapshot, before.Snapshot))
                    return RollbackAfterFailure(session, before.Snapshot, PlayModeTuningError.VerificationFailed, "Selected values or unselected top-level content changed during apply.");
                var scenePaths = before.Snapshot.Components.Select(item => item.ScenePath).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var dirtyResult = gateway.MarkScenesDirty(scenePaths);
                if (!dirtyResult.Succeeded)
                    return RollbackAfterFailure(session, before.Snapshot, dirtyResult.Error, dirtyResult.Message);

                session.phase = (int)PlayModeTuningPhase.Completed;
                session.error = (int)PlayModeTuningError.None;
                session.message = "The confirmed values were applied and the target scenes were marked dirty.";
                store.Save(session);
                return new PlayModeTuningApplyResult(true, true, PlayModeTuningError.None, session.message, false, false, PlayModeTuningError.None, string.Empty, ToPublicSession(session));
            }
            finally
            {
                applyInProgress = false;
            }
        }

        internal PlayModeTuningSession Discard(Guid sessionId)
        {
            var session = LoadExact(sessionId, out var loadError, out var loadMessage);
            if (session == null)
            {
                var current = LoadCurrent(out _, out _);
                return current == null ? new PlayModeTuningSession(Guid.Empty, PlayModeTuningPhase.Idle, loadError, loadMessage, 0, 0) : new PlayModeTuningSession(ParseSessionId(current), (PlayModeTuningPhase)current.phase, loadError, loadMessage, current.components.Count, current.properties.Count);
            }
            registry.RemoveSession(sessionId);
            session.phase = (int)PlayModeTuningPhase.Completed;
            session.error = (int)PlayModeTuningError.None;
            session.message = "The session was discarded without applying values.";
            session.planConsumed = true;
            store.Save(session);
            return ToPublicSession(session);
        }

        internal void OnEnteredPlayMode()
        {
            var session = LoadCurrent(out var storedError, out _);
            if (storedError != PlayModeTuningError.None)
                return;
            if (session == null || (PlayModeTuningPhase)session.phase != PlayModeTuningPhase.Armed)
                return;
            var environment = gateway.GetEnvironment();
            if (environment.SceneReloadDisabled)
            {
                MarkStale(session, PlayModeTuningError.DisableSceneReloadUnsupported, "Disable Scene Reload is not supported.");
                return;
            }
            if (environment.DomainReloadDisabled != session.domainReloadDisabled)
            {
                MarkStale(session, PlayModeTuningError.DomainReloadMismatch, "The Domain Reload setting changed after the session was armed.");
                return;
            }
            var tokenChanged = !StringComparer.Ordinal.Equals(session.startDomainToken, domainToken);
            if (tokenChanged == session.domainReloadDisabled)
            {
                MarkStale(session, PlayModeTuningError.DomainReloadMismatch, session.domainReloadDisabled ? "Disable Domain Reload requires the same domain token on Play entry." : "Default Play Mode requires a new domain token on Play entry.");
                return;
            }
            session.playDomainToken = domainToken;
            session.phase = (int)PlayModeTuningPhase.Capturable;
            session.error = (int)PlayModeTuningError.None;
            session.message = "Play Mode is active. Capture the selected values manually when tuning is complete.";
            store.Save(session);
        }

        internal void OnEnteredEditMode()
        {
            var session = LoadCurrent(out var storedError, out _);
            if (storedError != PlayModeTuningError.None)
                return;
            if (session == null)
                return;
            var phase = (PlayModeTuningPhase)session.phase;
            if (phase == PlayModeTuningPhase.Captured)
            {
                session.phase = (int)PlayModeTuningPhase.ReadyToPreview;
                session.error = (int)PlayModeTuningError.None;
                session.message = "Play Mode ended. Preview the captured differences before applying anything.";
                store.Save(session);
            }
            else if (phase == PlayModeTuningPhase.Capturable)
            {
                MarkStale(session, PlayModeTuningError.StaleSession, "Play Mode ended before an explicit capture was made.");
            }
        }

        internal void ResumeLifecycle()
        {
            var session = LoadCurrent(out var storedError, out _);
            if (storedError != PlayModeTuningError.None)
                return;
            if (session == null)
                return;
            var environment = gateway.GetEnvironment();
            var phase = (PlayModeTuningPhase)session.phase;
            if (phase == PlayModeTuningPhase.Previewed && !StringComparer.Ordinal.Equals(session.planDomainToken, domainToken))
            {
                MarkStale(session, PlayModeTuningError.StalePlan, "A Domain Reload invalidated the previewed plan object.");
                return;
            }
            if (phase == PlayModeTuningPhase.Armed && environment.Playing)
                OnEnteredPlayMode();
            else if (phase == PlayModeTuningPhase.Captured && !environment.Playing && !environment.PlayingOrWillChange)
                OnEnteredEditMode();
            else if (phase == PlayModeTuningPhase.Capturable && !environment.Playing && !environment.PlayingOrWillChange)
                OnEnteredEditMode();
        }

        private PlayModeTuningApplyResult RollbackAfterFailure(PlayModeTuningPersistedSession session, PlayModeTuningGatewaySnapshot before, PlayModeTuningError applyError, string applyMessage)
        {
            var rollbackWrites = PlayModeTuningIdentityOrder.OrderProperties(before.Properties, item => item.Record)
                .Select(item => new PlayModeTuningWrite(item.Record, item.Value))
                .ToArray();
            var rollback = gateway.Apply(rollbackWrites);
            var rollbackSucceeded = false;
            var rollbackMessage = rollback.Message;
            if (rollback.Succeeded)
            {
                var restored = gateway.Capture(session.properties);
                rollbackSucceeded = restored.Succeeded && SnapshotsMatchExactly(before, restored.Snapshot);
                rollbackMessage = rollbackSucceeded ? "The selected values and unselected top-level content were restored." : "Rollback verification found selected or unselected residual changes.";
            }
            session.phase = (int)PlayModeTuningPhase.Completed;
            session.error = (int)applyError;
            session.message = applyMessage ?? string.Empty;
            store.Save(session);
            return new PlayModeTuningApplyResult(true, false, applyError, applyMessage, true, rollbackSucceeded, rollbackSucceeded ? PlayModeTuningError.None : PlayModeTuningError.RollbackFailed, rollbackMessage, ToPublicSession(session));
        }

        private static bool SnapshotsMatchExactly(PlayModeTuningGatewaySnapshot expected, PlayModeTuningGatewaySnapshot actual)
        {
            if (expected == null || actual == null || expected.Properties.Count != actual.Properties.Count || expected.Components.Count != actual.Components.Count)
                return false;
            var actualProperties = actual.Properties.ToDictionary(item => item.Record.PropertyKey, StringComparer.Ordinal);
            foreach (var property in expected.Properties)
            {
                if (!actualProperties.TryGetValue(property.Record.PropertyKey, out var actualProperty) || !property.Value.EqualsExact(actualProperty.Value))
                    return false;
            }
            var actualComponents = actual.Components.ToDictionary(item => item.ComponentKey, StringComparer.Ordinal);
            foreach (var component in expected.Components)
            {
                if (!actualComponents.TryGetValue(component.ComponentKey, out var actualComponent) || !StringComparer.Ordinal.Equals(component.ScenePath, actualComponent.ScenePath) || !StringComparer.Ordinal.Equals(component.UnselectedFingerprint, actualComponent.UnselectedFingerprint))
                    return false;
            }
            return true;
        }

        private static bool MatchesCaptured(PlayModeTuningPersistedSession session, PlayModeTuningGatewaySnapshot actual, PlayModeTuningGatewaySnapshot before)
        {
            if (!HasSameIdentity(session, actual) || before.Components.Count != actual.Components.Count)
                return false;
            var actualProperties = actual.Properties.ToDictionary(item => item.Record.PropertyKey, StringComparer.Ordinal);
            foreach (var property in session.properties)
            {
                if (!actualProperties.TryGetValue(property.PropertyKey, out var actualProperty) || !property.Captured.EqualsExact(actualProperty.Value))
                    return false;
            }
            var beforeComponents = before.Components.ToDictionary(item => item.ComponentKey, StringComparer.Ordinal);
            foreach (var component in actual.Components)
            {
                if (!beforeComponents.TryGetValue(component.ComponentKey, out var beforeComponent) || !StringComparer.Ordinal.Equals(component.ScenePath, beforeComponent.ScenePath) || !StringComparer.Ordinal.Equals(component.UnselectedFingerprint, beforeComponent.UnselectedFingerprint))
                    return false;
            }
            return true;
        }

        private static bool MatchesBaseline(PlayModeTuningPersistedSession session, PlayModeTuningGatewaySnapshot actual)
        {
            if (!HasSameIdentity(session, actual))
                return false;
            var actualProperties = actual.Properties.ToDictionary(item => item.Record.PropertyKey, StringComparer.Ordinal);
            foreach (var property in session.properties)
            {
                if (!actualProperties.TryGetValue(property.PropertyKey, out var current) || !property.Baseline.EqualsExact(current.Value))
                    return false;
            }
            var actualComponents = actual.Components.ToDictionary(item => item.ComponentKey, StringComparer.Ordinal);
            foreach (var component in session.components)
            {
                if (!actualComponents.TryGetValue(component.componentKey, out var current) || !StringComparer.Ordinal.Equals(component.scenePath, current.ScenePath) || !StringComparer.Ordinal.Equals(component.baselineUnselectedFingerprint, current.UnselectedFingerprint))
                    return false;
            }
            return true;
        }

        private static bool HasSameIdentity(PlayModeTuningPersistedSession session, PlayModeTuningGatewaySnapshot actual)
        {
            if (session == null || actual == null || session.properties.Count != actual.Properties.Count || session.components.Count != actual.Components.Count)
                return false;
            var actualProperties = actual.Properties.ToDictionary(item => item.Record.PropertyKey, StringComparer.Ordinal);
            foreach (var property in session.properties)
            {
                if (!actualProperties.TryGetValue(property.PropertyKey, out var current) || !StringComparer.Ordinal.Equals(property.targetName, current.Record.targetName))
                    return false;
            }
            var actualComponents = actual.Components.ToDictionary(item => item.ComponentKey, StringComparer.Ordinal);
            foreach (var component in session.components)
            {
                if (!actualComponents.TryGetValue(component.componentKey, out var current) || !StringComparer.Ordinal.Equals(component.scenePath, current.ScenePath))
                    return false;
            }
            return true;
        }

        private static bool RefreshTargetNames(PlayModeTuningPersistedSession session, PlayModeTuningGatewaySnapshot snapshot)
        {
            if (session == null || snapshot == null || session.properties.Count != snapshot.Properties.Count)
                return false;
            var snapshotByKey = snapshot.Properties.ToDictionary(item => item.Record.PropertyKey, StringComparer.Ordinal);
            foreach (var property in session.properties)
            {
                if (!snapshotByKey.TryGetValue(property.PropertyKey, out var current))
                    return false;
                property.targetName = current.Record.targetName;
            }
            return true;
        }

        private static PlayModeTuningPropertyRecord CreateBaselineRecord(PlayModeTuningGatewayPropertySnapshot snapshot)
        {
            var source = snapshot.Record;
            return new PlayModeTuningPropertyRecord
            {
                componentKey = source.componentKey,
                globalObjectId = source.globalObjectId,
                sceneGuid = source.sceneGuid,
                scenePath = source.scenePath,
                scriptGuid = source.scriptGuid,
                typeName = source.typeName,
                targetName = source.targetName,
                propertyPath = source.propertyPath,
                propertyType = source.propertyType,
                numericType = source.numericType,
                baselineKind = (int)snapshot.Value.Kind,
                baselinePayload = snapshot.Value.Payload,
                baselineDisplay = snapshot.Value.Display
            };
        }

        private static IReadOnlyList<PlayModeTuningWrite> CreateCapturedWrites(PlayModeTuningPersistedSession session)
        {
            return PlayModeTuningIdentityOrder.OrderProperties(session.properties.Where(item => !item.Baseline.EqualsExact(item.Captured)), item => item)
                .Select(item => new PlayModeTuningWrite(item, item.Captured))
                .ToArray();
        }

        private static int ComputePayloadBytes(IReadOnlyList<PlayModeTuningGatewayPropertySnapshot> current, IReadOnlyList<PlayModeTuningPropertyRecord> baseline)
        {
            long total = 0;
            var baselineByKey = baseline == null ? null : baseline.ToDictionary(item => item.PropertyKey, StringComparer.Ordinal);
            foreach (var item in current)
            {
                total += Encoding.UTF8.GetByteCount(item.Record.PropertyKey);
                total += Encoding.UTF8.GetByteCount(item.Value.Payload);
                if (baselineByKey != null && baselineByKey.TryGetValue(item.Record.PropertyKey, out var stored))
                    total += Encoding.UTF8.GetByteCount(stored.baselinePayload);
                if (total > int.MaxValue)
                    return int.MaxValue;
            }
            return (int)total;
        }

        private PlayModeTuningPersistedSession LoadExact(Guid sessionId, out PlayModeTuningError error, out string message)
        {
            var session = LoadCurrent(out error, out message);
            if (error != PlayModeTuningError.None)
                return null;
            if (session == null || !Guid.TryParseExact(session.sessionId, "N", out var storedId))
            {
                error = PlayModeTuningError.InvalidSession;
                message = "No valid tuning session is stored.";
                return null;
            }
            if (storedId != sessionId)
            {
                error = PlayModeTuningError.InvalidSession;
                message = "The supplied session identity does not match the current session.";
                return null;
            }
            error = PlayModeTuningError.None;
            message = string.Empty;
            return session;
        }

        private PlayModeTuningPersistedSession LoadCurrent(out PlayModeTuningError error, out string message)
        {
            PlayModeTuningPersistedSession session;
            try
            {
                session = store.Load();
            }
            catch (Exception exception)
            {
                session = null;
                message = "The stored session could not be read: " + exception.Message;
                error = PlayModeTuningError.SessionDataInvalid;
                return NormalizeInvalidSession(message);
            }
            if (session == null)
            {
                error = PlayModeTuningError.None;
                message = string.Empty;
                return null;
            }
            if (PlayModeTuningSessionValidator.TryValidate(session, out message))
            {
                error = PlayModeTuningError.None;
                return session;
            }
            error = PlayModeTuningError.SessionDataInvalid;
            return NormalizeInvalidSession(message);
        }

        private PlayModeTuningPersistedSession NormalizeInvalidSession(string message)
        {
            var invalid = new PlayModeTuningPersistedSession
            {
                sessionId = Guid.Empty.ToString("N"),
                phase = (int)PlayModeTuningPhase.Stale,
                error = (int)PlayModeTuningError.SessionDataInvalid,
                message = message ?? "The stored session is invalid.",
                planConsumed = true
            };
            try
            {
                store.Save(invalid);
            }
            catch (Exception)
            {
            }
            return invalid;
        }

        private PlayModeTuningSession MarkStale(PlayModeTuningPersistedSession session, PlayModeTuningError error, string message)
        {
            session.phase = (int)PlayModeTuningPhase.Stale;
            session.error = (int)error;
            session.message = message ?? string.Empty;
            session.planConsumed = true;
            if (Guid.TryParseExact(session.sessionId, "N", out var sessionId))
                registry.RemoveSession(sessionId);
            store.Save(session);
            return ToPublicSession(session);
        }

        private PlayModeTuningApplyResult ApplyStale(PlayModeTuningPersistedSession session, PlayModeTuningError error, string message)
        {
            var publicSession = MarkStale(session, error, message);
            return new PlayModeTuningApplyResult(false, false, error, message, false, false, PlayModeTuningError.None, string.Empty, publicSession);
        }

        private static EnvironmentValidation ValidateEditEnvironment(PlayModeTuningEnvironment environment)
        {
            if (environment == null)
                return new EnvironmentValidation(PlayModeTuningError.SessionDataInvalid, "The editor environment is unavailable.");
            if (environment.SceneReloadDisabled)
                return new EnvironmentValidation(PlayModeTuningError.DisableSceneReloadUnsupported, "Disable Scene Reload is not supported.");
            if (environment.Playing || environment.PlayingOrWillChange)
                return new EnvironmentValidation(PlayModeTuningError.EditModeRequired, "This operation requires stable Edit Mode.");
            if (environment.Compiling || environment.Updating)
                return new EnvironmentValidation(PlayModeTuningError.EditorBusy, "Wait for compilation and Asset updates to finish.");
            return new EnvironmentValidation(PlayModeTuningError.None, string.Empty);
        }

        private static PlayModeTuningSession ToPublicSession(PlayModeTuningPersistedSession session)
        {
            return new PlayModeTuningSession(ParseSessionId(session), (PlayModeTuningPhase)session.phase, (PlayModeTuningError)session.error, session.message, session.components?.Count ?? 0, session.properties?.Count ?? 0);
        }

        private static Guid ParseSessionId(PlayModeTuningPersistedSession session)
        {
            return session != null && Guid.TryParseExact(session.sessionId, "N", out var parsed) ? parsed : Guid.Empty;
        }

        private static PlayModeTuningSession IdleSession()
        {
            return new PlayModeTuningSession(Guid.Empty, PlayModeTuningPhase.Idle, PlayModeTuningError.None, string.Empty, 0, 0);
        }

        private static bool IsTerminal(PlayModeTuningPhase phase)
        {
            return phase == PlayModeTuningPhase.Completed || phase == PlayModeTuningPhase.Stale;
        }

        private static PlayModeTuningStartResult StartFailure(PlayModeTuningError error, string message)
        {
            return new PlayModeTuningStartResult(error, message, new PlayModeTuningSession(Guid.Empty, PlayModeTuningPhase.Idle, error, message, 0, 0));
        }

        private static PlayModeTuningCaptureResult CaptureFailure(PlayModeTuningError error, string message, PlayModeTuningPersistedSession session = null)
        {
            var publicSession = session == null ? new PlayModeTuningSession(Guid.Empty, PlayModeTuningPhase.Idle, error, message, 0, 0) : ToPublicSession(session);
            return new PlayModeTuningCaptureResult(error, message, publicSession, 0, 0);
        }

        private static PlayModeTuningCaptureResult CaptureFailure(PlayModeTuningSession session)
        {
            return new PlayModeTuningCaptureResult(session.Error, session.Message, session, 0, 0);
        }

        private static PlayModeTuningApplyResult ApplyFailure(bool attempted, PlayModeTuningError error, string message)
        {
            return new PlayModeTuningApplyResult(attempted, false, error, message, false, false, PlayModeTuningError.None, string.Empty, new PlayModeTuningSession(Guid.Empty, PlayModeTuningPhase.Idle, error, message, 0, 0));
        }

        private sealed class EnvironmentValidation
        {
            internal EnvironmentValidation(PlayModeTuningError error, string message)
            {
                Error = error;
                Message = message;
            }

            internal PlayModeTuningError Error { get; }
            internal string Message { get; }
        }
    }
}
