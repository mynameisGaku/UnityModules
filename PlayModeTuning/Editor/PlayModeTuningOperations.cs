using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayModeTuning.Editor
{
    /// <summary>決定論的な作業状態を管理し、エンジン上の読み書きだけを接続先へ委ねます。</summary>
    internal sealed class PlayModeTuningOperations
    {
        internal const int MaximumComponents = 32;
        internal const int MaximumProperties = 256;
        internal const int MaximumPayloadBytes = 256 * 1024;
        private const string StorageFailureMessage = "調整データを保存できませんでした。詳しくはコンソールを確認してください。";
        private const string CompletionGuardMessage = "反映結果を調整データへ確定できませんでした。対象シーンと取り消し履歴を確認し、詳しくはコンソールを確認してください。";

        private readonly IPlayModeTuningGateway gateway;
        private readonly IPlayModeTuningSessionStore store;
        private readonly PlayModeTuningPlanRegistry registry;
        private readonly string domainToken;
        private bool applyInProgress;
        private string transientStorageFailureMessage = string.Empty;

        // 読み込みの復旧だけで消してよい一時エラーかを区別します。
        private bool transientStorageReadFailure;

        internal PlayModeTuningOperations(IPlayModeTuningGateway gateway, IPlayModeTuningSessionStore store, PlayModeTuningPlanRegistry registry, string domainToken)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.domainToken = string.IsNullOrEmpty(domainToken) ? throw new ArgumentException("スクリプト領域の識別子が必要です。", nameof(domainToken)) : domainToken;
        }

        internal PlayModeTuningStartResult Start(IReadOnlyList<PlayModeTuningPropertySelection> selections)
        {
            var current = LoadCurrent(out var storedError, out var storedMessage);
            if (storedError != PlayModeTuningError.None)
                return StartFailure(storedError, storedMessage);
            if (current != null && !IsTerminal((PlayModeTuningPhase)current.phase))
                return StartFailure(PlayModeTuningError.WrongPhase, "現在の調整を破棄または完了してから、新しい調整を開始してください。");
            var environmentError = ValidateEditEnvironment(gateway.GetEnvironment());
            if (environmentError.Error != PlayModeTuningError.None)
                return StartFailure(environmentError.Error, environmentError.Message);
            if (selections == null || selections.Count == 0)
                return StartFailure(PlayModeTuningError.InvalidSelection, "コンポーネントの項目を一つ以上選んでください。");
            if (selections.Count > MaximumProperties)
                return StartFailure(PlayModeTuningError.TooManyProperties, "一回の調整で選べる項目は256件までです。");

            var copiedSelections = selections.ToArray();
            var resolved = gateway.ResolveSelections(copiedSelections);
            if (!resolved.Succeeded)
                return StartFailure(resolved.Error, resolved.Message);
            if (resolved.Snapshot.Properties.Count == 0)
                return StartFailure(PlayModeTuningError.InvalidSelection, "対応する対象項目を解決できませんでした。");
            if (resolved.Snapshot.Properties.Count > MaximumProperties)
                return StartFailure(PlayModeTuningError.TooManyProperties, "一回の調整で選べる項目は256件までです。");
            if (resolved.Snapshot.Components.Count > MaximumComponents)
                return StartFailure(PlayModeTuningError.TooManyComponents, "一回の調整で選べるコンポーネントは32件までです。");
            var baselineBytes = ComputePayloadBytes(resolved.Snapshot.Properties, null);
            if (baselineBytes > MaximumPayloadBytes)
                return StartFailure(PlayModeTuningError.PayloadTooLarge, "変更前データが256 KiBの上限を超えています。");

            var environment = gateway.GetEnvironment();
            var session = new PlayModeTuningPersistedSession
            {
                sessionId = Guid.NewGuid().ToString("N"),
                phase = (int)PlayModeTuningPhase.Armed,
                error = (int)PlayModeTuningError.None,
                message = "調整を開始しました。再生を開始した後、明示的に値を記録してください。",
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
            if (!TrySave(session, out var saveMessage))
                return StartFailure(PlayModeTuningError.SessionStorageFailed, saveMessage);
            return new PlayModeTuningStartResult(PlayModeTuningError.None, string.Empty, ToPublicSession(session));
        }

        internal PlayModeTuningSession GetCurrentSession()
        {
            var session = LoadCurrent(out _, out _);
            var current = session == null ? IdleSession() : ToPublicSession(session);
            return string.IsNullOrEmpty(transientStorageFailureMessage)
                ? current
                : WithError(current, PlayModeTuningError.SessionStorageFailed, transientStorageFailureMessage);
        }

        internal PlayModeTuningCaptureResult CaptureDuringPlay(Guid sessionId)
        {
            var session = LoadExact(sessionId, out var loadError, out var loadMessage);
            if (session == null)
                return CaptureFailure(loadError, loadMessage);
            if ((PlayModeTuningPhase)session.phase != PlayModeTuningPhase.Capturable)
                return CaptureFailure(PlayModeTuningError.WrongPhase, "開始済みの調整で再生を始めた後にだけ値を記録できます。", session);
            var environment = gateway.GetEnvironment();
            if (!environment.Playing)
                return CaptureFailure(PlayModeTuningError.PlayModeRequired, "値の記録は再生中に実行してください。", session);
            if (environment.SceneReloadDisabled)
                return CaptureFailure(MarkStale(session, PlayModeTuningError.DisableSceneReloadUnsupported, "シーン再読み込みを無効にした設定には対応していません。"));

            var captured = gateway.Capture(session.properties);
            if (!captured.Succeeded)
                return CaptureFailure(MarkStale(session, captured.Error, captured.Message));
            if (!RefreshTargetNames(session, captured.Snapshot))
                return CaptureFailure(MarkStale(session, PlayModeTuningError.IdentityMismatch, "再生中に選択項目の識別情報が変わりました。"));
            if (!HasSameIdentity(session, captured.Snapshot))
                return CaptureFailure(MarkStale(session, PlayModeTuningError.IdentityMismatch, "再生中に対象または項目の識別情報が変わりました。"));
            var payloadBytes = ComputePayloadBytes(captured.Snapshot.Properties, session.properties);
            if (payloadBytes > MaximumPayloadBytes)
                return CaptureFailure(MarkStale(session, PlayModeTuningError.PayloadTooLarge, "変更前と記録後のデータが合計256 KiBの上限を超えています。"));

            var previousSession = ToPublicSession(session);
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
            session.message = "選んだ値を記録しました。再生を終了してから差分を表示してください。";
            if (!TrySave(session, out var saveMessage))
                return CaptureFailure(WithError(previousSession, PlayModeTuningError.SessionStorageFailed, saveMessage));
            return new PlayModeTuningCaptureResult(PlayModeTuningError.None, string.Empty, ToPublicSession(session), session.properties.Count, payloadBytes);
        }

        internal PlayModeTuningPlan PreviewAfterPlay(Guid sessionId)
        {
            var session = LoadExact(sessionId, out var loadError, out var loadMessage);
            if (session == null)
                return PlayModeTuningPlanner.Failure(loadError, loadMessage, sessionId);
            if ((PlayModeTuningPhase)session.phase != PlayModeTuningPhase.ReadyToPreview)
                return PlayModeTuningPlanner.Failure(PlayModeTuningError.WrongPhase, "値を明示的に記録し、再生を終了した後にだけ差分を表示できます。", sessionId);
            var environmentError = ValidateEditEnvironment(gateway.GetEnvironment());
            if (environmentError.Error != PlayModeTuningError.None)
                return PlayModeTuningPlanner.Failure(environmentError.Error, environmentError.Message, sessionId);

            var current = gateway.Capture(session.properties);
            if (!current.Succeeded)
            {
                var stale = MarkStale(session, current.Error, current.Message);
                return PlayModeTuningPlanner.Failure(stale.Error, stale.Message, sessionId);
            }
            if (!RefreshTargetNames(session, current.Snapshot))
            {
                var stale = MarkStale(session, PlayModeTuningError.IdentityMismatch, "差分表示前に選択項目の識別情報が変わりました。");
                return PlayModeTuningPlanner.Failure(stale.Error, stale.Message, sessionId);
            }
            if (!MatchesBaseline(session, current.Snapshot))
            {
                var stale = MarkStale(session, PlayModeTuningError.StaleSession, "調整開始後に、編集状態の値、対象シーンの階層構造、または選択外の最上位項目が変わりました。");
                return PlayModeTuningPlanner.Failure(stale.Error, stale.Message, sessionId);
            }

            var nonce = Guid.NewGuid();
            var plan = PlayModeTuningPlanner.Create(session, nonce);
            if (!plan.IsReady)
            {
                var previousSession = ToPublicSession(session);
                session.phase = (int)PlayModeTuningPhase.Completed;
                session.error = (int)plan.Error;
                session.message = plan.Message;
                if (!TrySave(session, out var saveMessage))
                    return PlayModeTuningPlanner.Failure(PlayModeTuningError.SessionStorageFailed, saveMessage, previousSession.SessionId);
                return plan;
            }
            session.phase = (int)PlayModeTuningPhase.Previewed;
            session.error = (int)PlayModeTuningError.None;
            session.message = "差分を正確に確認し、同意した後、この反映予定を一度だけ使用してください。";
            session.planNonce = nonce.ToString("N");
            session.planRevision = plan.Revision;
            session.planDomainToken = domainToken;
            session.planConsumed = false;
            if (!TrySave(session, out var previewSaveMessage))
                return PlayModeTuningPlanner.Failure(PlayModeTuningError.SessionStorageFailed, previewSaveMessage, sessionId);
            registry.Register(plan);
            return plan;
        }

        internal PlayModeTuningApplyResult Apply(PlayModeTuningPlan plan)
        {
            if (applyInProgress)
                return ApplyFailure(false, PlayModeTuningError.ApplyInProgress, "別の反映処理が進行中です。");
            var consumeError = registry.TryConsume(plan);
            if (consumeError != PlayModeTuningError.None)
                return ApplyFailure(false, consumeError, consumeError == PlayModeTuningError.PlanAlreadyConsumed ? "反映予定はすでに使用済みです。" : "反映予定が古いか、現在のスクリプト領域で作成されていません。");

            var session = LoadExact(plan.SessionId, out var loadError, out var loadMessage);
            if (session == null)
            {
                if (loadError == PlayModeTuningError.SessionStorageFailed)
                    registry.RestoreBeforeMutation(plan);
                return ApplyFailure(false, loadError, loadMessage);
            }
            session.planConsumed = true;
            if (!TrySave(session, out var consumeSaveMessage))
            {
                session.planConsumed = false;
                registry.RestoreBeforeMutation(plan);
                return ApplyFailure(false, PlayModeTuningError.SessionStorageFailed, consumeSaveMessage);
            }
            if ((PlayModeTuningPhase)session.phase != PlayModeTuningPhase.Previewed ||
                !StringComparer.Ordinal.Equals(session.planNonce, plan.Nonce.ToString("N")) ||
                !StringComparer.Ordinal.Equals(session.planRevision, plan.Revision) ||
                !StringComparer.Ordinal.Equals(session.planDomainToken, domainToken))
                return ApplyStale(session, PlayModeTuningError.StalePlan, "保存された調整内容が、確認済みの反映予定と一致しません。");
            var recomputedPlan = PlayModeTuningPlanner.Create(session, plan.Nonce);
            if (!recomputedPlan.IsReady || !StringComparer.Ordinal.Equals(recomputedPlan.Revision, plan.Revision))
                return ApplyStale(session, PlayModeTuningError.StalePlan, "保存された値または識別情報が、確認済みの反映予定と一致しません。");

            var environmentError = ValidateEditEnvironment(gateway.GetEnvironment());
            if (environmentError.Error != PlayModeTuningError.None)
                return ApplyStale(session, environmentError.Error, environmentError.Message);
            var before = gateway.Capture(session.properties);
            if (!before.Succeeded)
                return ApplyStale(session, before.Error, before.Message);
            if (!MatchesBaseline(session, before.Snapshot))
                return ApplyStale(session, PlayModeTuningError.StaleSession, "差分表示後に編集状態の対象が変わりました。");

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
                    return RollbackAfterFailure(session, before.Snapshot, PlayModeTuningError.VerificationFailed, "反映中に、選択した値、対象シーンの階層構造、または選択外の最上位項目が意図せず変わりました。");
                var scenePaths = before.Snapshot.Components.Select(item => item.ScenePath).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var dirtyResult = gateway.MarkScenesDirty(scenePaths);
                if (!dirtyResult.Succeeded)
                    return RollbackAfterFailure(session, before.Snapshot, dirtyResult.Error, dirtyResult.Message);

                session.phase = (int)PlayModeTuningPhase.Stale;
                session.error = (int)PlayModeTuningError.SessionStorageFailed;
                session.message = CompletionGuardMessage;
                session.planConsumed = true;
                if (!TrySave(session, out var guardSaveMessage))
                    return RollbackAfterFailure(session, before.Snapshot, PlayModeTuningError.SessionStorageFailed, guardSaveMessage);
                var complete = gateway.CompleteApply();
                if (!complete.Succeeded)
                    return RollbackAfterFailure(session, before.Snapshot, complete.Error, complete.Message);
                session.phase = (int)PlayModeTuningPhase.Completed;
                session.error = (int)PlayModeTuningError.None;
                session.message = "確認済みの値を反映し、対象シーンを変更済みにしました。";
                if (!TrySave(session, out var completionSaveMessage))
                    return RollbackAfterFailure(session, before.Snapshot, PlayModeTuningError.SessionStorageFailed, completionSaveMessage);
                gateway.ReleaseApply();
                return new PlayModeTuningApplyResult(true, true, PlayModeTuningError.None, session.message, false, false, PlayModeTuningError.None, string.Empty, ToPublicSession(session));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                return RollbackAfterFailure(session, before.Snapshot, PlayModeTuningError.ApplyFailed, "反映処理を完了できませんでした。詳しくはコンソールを確認してください。");
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
            var previousSession = ToPublicSession(session);
            session.phase = (int)PlayModeTuningPhase.Completed;
            session.error = (int)PlayModeTuningError.None;
            session.message = "値を反映せずに調整を破棄しました。";
            session.planConsumed = true;
            if (!TrySave(session, out var saveMessage))
                return WithError(previousSession, PlayModeTuningError.SessionStorageFailed, saveMessage);
            registry.RemoveSession(sessionId);
            return ToPublicSession(session);
        }

        internal bool OnEnteredPlayMode()
        {
            var session = LoadCurrent(out var storedError, out _);
            if (storedError != PlayModeTuningError.None)
                return false;
            ClearRecoveredReadFailure();
            if (session == null || (PlayModeTuningPhase)session.phase != PlayModeTuningPhase.Armed)
                return true;
            var environment = gateway.GetEnvironment();
            if (!environment.Playing)
                return MarkStale(session, PlayModeTuningError.StaleSession, "再生開始時の状態更新を完了する前に再生が終了しました。").Error != PlayModeTuningError.SessionStorageFailed;
            if (environment.SceneReloadDisabled)
            {
                return MarkStale(session, PlayModeTuningError.DisableSceneReloadUnsupported, "シーン再読み込みを無効にした設定には対応していません。").Error != PlayModeTuningError.SessionStorageFailed;
            }
            if (environment.DomainReloadDisabled != session.domainReloadDisabled)
            {
                return MarkStale(session, PlayModeTuningError.DomainReloadMismatch, "調整開始後にスクリプト領域の再読み込み設定が変わりました。").Error != PlayModeTuningError.SessionStorageFailed;
            }
            var tokenChanged = !StringComparer.Ordinal.Equals(session.startDomainToken, domainToken);
            if (tokenChanged == session.domainReloadDisabled)
            {
                return MarkStale(session, PlayModeTuningError.DomainReloadMismatch, session.domainReloadDisabled ? "スクリプト領域の再読み込みを無効にした場合、再生開始時も同じ領域識別子が必要です。" : "通常の再生開始では新しい領域識別子が必要です。").Error != PlayModeTuningError.SessionStorageFailed;
            }
            session.playDomainToken = domainToken;
            session.phase = (int)PlayModeTuningPhase.Capturable;
            session.error = (int)PlayModeTuningError.None;
            session.message = "再生中です。調整が終わったら選んだ値を明示的に記録してください。";
            return TrySave(session, out _);
        }

        internal bool OnEnteredEditMode()
        {
            var session = LoadCurrent(out var storedError, out _);
            if (storedError != PlayModeTuningError.None)
                return false;
            ClearRecoveredReadFailure();
            if (session == null)
                return true;
            var environment = gateway.GetEnvironment();
            if (environment.Playing || environment.PlayingOrWillChange)
                return MarkStale(session, PlayModeTuningError.StaleSession, "編集状態への復帰処理を完了する前に、次の再生が始まりました。").Error != PlayModeTuningError.SessionStorageFailed;
            var phase = (PlayModeTuningPhase)session.phase;
            if (phase == PlayModeTuningPhase.Captured)
            {
                session.phase = (int)PlayModeTuningPhase.ReadyToPreview;
                session.error = (int)PlayModeTuningError.None;
                session.message = "再生を終了しました。反映する前に記録した差分を確認してください。";
                return TrySave(session, out _);
            }
            if (phase == PlayModeTuningPhase.Capturable)
                return MarkStale(session, PlayModeTuningError.StaleSession, "値を明示的に記録する前に再生が終了しました。").Error != PlayModeTuningError.SessionStorageFailed;
            return true;
        }

        internal bool ResumeLifecycle(EPlayModeTuningObservedTransition observedTransition = EPlayModeTuningObservedTransition.None)
        {
            var session = LoadCurrent(out var storedError, out _);
            if (storedError != PlayModeTuningError.None)
                return false;
            ClearRecoveredReadFailure();
            if (session == null)
                return true;
            var environment = gateway.GetEnvironment();
            var phase = (PlayModeTuningPhase)session.phase;
            if (phase == PlayModeTuningPhase.Previewed && !StringComparer.Ordinal.Equals(session.planDomainToken, domainToken))
                return MarkStale(session, PlayModeTuningError.StalePlan, "スクリプト領域の再読み込みにより、確認済みの反映予定が無効になりました。").Error != PlayModeTuningError.SessionStorageFailed;
            if (phase == PlayModeTuningPhase.Armed && environment.Playing)
                return OnEnteredPlayMode();
            if (phase == PlayModeTuningPhase.Armed && observedTransition == EPlayModeTuningObservedTransition.EnteredPlayMode)
                return MarkStale(session, PlayModeTuningError.StaleSession, "再生開始時の状態更新を完了する前に再生が終了しました。").Error != PlayModeTuningError.SessionStorageFailed;
            if (phase == PlayModeTuningPhase.Captured && !environment.Playing && !environment.PlayingOrWillChange)
                return OnEnteredEditMode();
            if (phase == PlayModeTuningPhase.Capturable && !environment.Playing && !environment.PlayingOrWillChange)
                return OnEnteredEditMode();
            if ((phase == PlayModeTuningPhase.Captured || phase == PlayModeTuningPhase.Capturable) && observedTransition == EPlayModeTuningObservedTransition.EnteredEditMode)
                return OnEnteredEditMode();
            return true;
        }

        private PlayModeTuningApplyResult RollbackAfterFailure(PlayModeTuningPersistedSession session, PlayModeTuningGatewaySnapshot before, PlayModeTuningError applyError, string applyMessage)
        {
            var rollback = gateway.RevertApply();
            var rollbackSucceeded = false;
            var rollbackMessage = rollback.Message;
            if (rollback.Succeeded)
            {
                var restored = gateway.Capture(session.properties);
                rollbackSucceeded = restored.Succeeded && SnapshotsMatchExactly(before, restored.Snapshot);
                rollbackMessage = rollbackSucceeded ? "対象シーン階層の検査対象となるシリアル化値を反映前へ戻しました。" : "復元後も、選択項目、対象シーンの階層構造、または選択外の最上位項目に変更が残っています。";
            }
            session.phase = (int)PlayModeTuningPhase.Completed;
            session.error = (int)applyError;
            session.message = applyMessage ?? string.Empty;
            if (!TrySave(session, out var saveMessage))
            {
                applyError = PlayModeTuningError.SessionStorageFailed;
                session.phase = (int)PlayModeTuningPhase.Stale;
                session.error = (int)applyError;
                session.planConsumed = true;
                if (TryClear(out _))
                {
                    applyMessage = saveMessage + " 安全のため、保存されていた調整データを消去しました。";
                }
                else
                {
                    var invalidStateMessage = saveMessage + " 保存されていた調整データを無効状態へ更新しました。";
                    session.message = invalidStateMessage;
                    applyMessage = TrySave(session, out _)
                        ? invalidStateMessage
                        : saveMessage + " 保存されていた調整データの消去も無効化もできませんでした。Unityエディターを再起動する前に対象シーンを確認してください。";
                }
                session.message = applyMessage;
            }
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
                message = "有効な調整作業が保存されていません。";
                return null;
            }
            if (storedId != sessionId)
            {
                error = PlayModeTuningError.InvalidSession;
                message = "指定された作業識別子が現在の調整と一致しません。";
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
                UnityEngine.Debug.LogException(exception);
                message = "保存された調整データを読み取れませんでした。詳しくはコンソールを確認してください。";
                error = PlayModeTuningError.SessionStorageFailed;
                transientStorageFailureMessage = message;
                transientStorageReadFailure = true;
                return new PlayModeTuningPersistedSession
                {
                    sessionId = Guid.Empty.ToString("N"),
                    phase = (int)PlayModeTuningPhase.Stale,
                    error = (int)error,
                    message = message,
                    planConsumed = true
                };
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
                message = message ?? "保存された調整データが無効です。",
                planConsumed = true
            };
            TrySave(invalid, out _);
            return invalid;
        }

        private PlayModeTuningSession MarkStale(PlayModeTuningPersistedSession session, PlayModeTuningError error, string message)
        {
            var previousSession = ToPublicSession(session);
            session.phase = (int)PlayModeTuningPhase.Stale;
            session.error = (int)error;
            session.message = message ?? string.Empty;
            session.planConsumed = true;
            if (!TrySave(session, out var saveMessage))
                return WithError(previousSession, PlayModeTuningError.SessionStorageFailed, saveMessage);
            if (Guid.TryParseExact(session.sessionId, "N", out var sessionId))
                registry.RemoveSession(sessionId);
            return ToPublicSession(session);
        }

        private PlayModeTuningApplyResult ApplyStale(PlayModeTuningPersistedSession session, PlayModeTuningError error, string message)
        {
            var publicSession = MarkStale(session, error, message);
            return new PlayModeTuningApplyResult(false, false, publicSession.Error, publicSession.Message, false, false, PlayModeTuningError.None, string.Empty, publicSession);
        }

        private bool TrySave(PlayModeTuningPersistedSession session, out string message)
        {
            try
            {
                store.Save(session);
                transientStorageFailureMessage = string.Empty;
                transientStorageReadFailure = false;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                message = StorageFailureMessage;
                transientStorageFailureMessage = message;
                transientStorageReadFailure = false;
                return false;
            }
        }

        private bool TryClear(out string message)
        {
            try
            {
                store.Clear();
                transientStorageFailureMessage = string.Empty;
                transientStorageReadFailure = false;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                message = "保存された調整データを消去できませんでした。詳しくはコンソールを確認してください。";
                transientStorageFailureMessage = message;
                transientStorageReadFailure = false;
                return false;
            }
        }

        private void ClearRecoveredReadFailure()
        {
            if (!transientStorageReadFailure)
                return;
            transientStorageFailureMessage = string.Empty;
            transientStorageReadFailure = false;
        }

        private static EnvironmentValidation ValidateEditEnvironment(PlayModeTuningEnvironment environment)
        {
            if (environment == null)
                return new EnvironmentValidation(PlayModeTuningError.SessionDataInvalid, "エディターの状態を取得できません。");
            if (environment.SceneReloadDisabled)
                return new EnvironmentValidation(PlayModeTuningError.DisableSceneReloadUnsupported, "シーン再読み込みを無効にした設定には対応していません。");
            if (environment.Playing || environment.PlayingOrWillChange)
                return new EnvironmentValidation(PlayModeTuningError.EditModeRequired, "この処理は安定した編集状態で実行してください。");
            if (environment.Compiling || environment.Updating)
                return new EnvironmentValidation(PlayModeTuningError.EditorBusy, "コンパイルとアセット更新が終わるまで待ってください。");
            return new EnvironmentValidation(PlayModeTuningError.None, string.Empty);
        }

        private static PlayModeTuningSession ToPublicSession(PlayModeTuningPersistedSession session)
        {
            return new PlayModeTuningSession(ParseSessionId(session), (PlayModeTuningPhase)session.phase, (PlayModeTuningError)session.error, session.message, session.components?.Count ?? 0, session.properties?.Count ?? 0);
        }

        private static PlayModeTuningSession WithError(PlayModeTuningSession session, PlayModeTuningError error, string message)
        {
            return new PlayModeTuningSession(session.SessionId, session.Phase, error, message, session.ComponentCount, session.PropertyCount);
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
