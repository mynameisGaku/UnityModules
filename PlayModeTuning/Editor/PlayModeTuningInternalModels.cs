using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor
{
    /// <summary>再読み込みを越えて保持する調整作業全体の保存形式です。</summary>
    [Serializable]
    internal sealed class PlayModeTuningPersistedSession
    {
        /// <summary>現在読み書きできる保存形式の版です。</summary>
        internal const int CurrentSchemaVersion = 1;

        /// <summary>保存形式の版です。</summary>
        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>調整作業を一意に識別する値です。</summary>
        public string sessionId = string.Empty;

        /// <summary>現在の作業段階を表す数値です。</summary>
        public int phase;

        /// <summary>現在の失敗理由を表す数値です。</summary>
        public int error;

        /// <summary>利用者へ表示する現在の説明です。</summary>
        public string message = string.Empty;

        /// <summary>開始時にスクリプト領域の再読み込みが無効だったかを示します。</summary>
        public bool domainReloadDisabled;

        /// <summary>開始時のスクリプト領域を識別する値です。</summary>
        public string startDomainToken = string.Empty;

        /// <summary>再生開始時のスクリプト領域を識別する値です。</summary>
        public string playDomainToken = string.Empty;

        /// <summary>確認済み反映予定を一意に識別する値です。</summary>
        public string planNonce = string.Empty;

        /// <summary>確認済み反映予定の内容を識別する版指紋です。</summary>
        public string planRevision = string.Empty;

        /// <summary>反映予定を作成したスクリプト領域の識別値です。</summary>
        public string planDomainToken = string.Empty;

        /// <summary>反映予定をすでに使用したかを示します。</summary>
        public bool planConsumed;

        /// <summary>選択した項目ごとの保存内容です。</summary>
        public List<PlayModeTuningPropertyRecord> properties = new List<PlayModeTuningPropertyRecord>();

        /// <summary>対象コンポーネントごとの保存内容です。</summary>
        public List<PlayModeTuningComponentRecord> components = new List<PlayModeTuningComponentRecord>();
    }

    /// <summary>選択した一項目の識別情報と変更前後の値を保持します。</summary>
    [Serializable]
    internal sealed class PlayModeTuningPropertyRecord
    {
        /// <summary>対象コンポーネントの識別情報をまとめた版指紋です。</summary>
        public string componentKey = string.Empty;

        /// <summary>対象コンポーネントの大域識別子です。</summary>
        public string globalObjectId = string.Empty;

        /// <summary>対象シーンの資産識別子です。</summary>
        public string sceneGuid = string.Empty;

        /// <summary>対象シーンのプロジェクト内パスです。</summary>
        public string scenePath = string.Empty;

        /// <summary>対象コンポーネントを定義するスクリプトの資産識別子です。</summary>
        public string scriptGuid = string.Empty;

        /// <summary>対象コンポーネントのアセンブリ修飾型名です。</summary>
        public string typeName = string.Empty;

        /// <summary>画面表示に使う対象名です。</summary>
        public string targetName = string.Empty;

        /// <summary>最上位のシリアル化項目を示す正確な識別名です。</summary>
        public string propertyPath = string.Empty;

        /// <summary>シリアル化項目の種類です。</summary>
        public string propertyType = string.Empty;

        /// <summary>数値項目を詳しく区別する種類です。</summary>
        public string numericType = string.Empty;

        /// <summary>変更前の値の種類を表す数値です。</summary>
        public int baselineKind;

        /// <summary>変更前の値を損失なく保存した文字列です。</summary>
        public string baselinePayload = string.Empty;

        /// <summary>変更前の値を利用者向けに表した文字列です。</summary>
        public string baselineDisplay = string.Empty;

        /// <summary>記録後の値の種類を表す数値です。</summary>
        public int capturedKind;

        /// <summary>記録後の値を損失なく保存した文字列です。</summary>
        public string capturedPayload = string.Empty;

        /// <summary>記録後の値を利用者向けに表した文字列です。</summary>
        public string capturedDisplay = string.Empty;

        internal string PropertyKey => PlayModeTuningFingerprint.Compute(new[] { componentKey, propertyPath, propertyType, numericType });

        internal PlayModeTuningEncodedValue Baseline => new PlayModeTuningEncodedValue((PlayModeTuningValueKind)baselineKind, baselinePayload, baselineDisplay);

        internal PlayModeTuningEncodedValue Captured => new PlayModeTuningEncodedValue((PlayModeTuningValueKind)capturedKind, capturedPayload, capturedDisplay);
    }

    /// <summary>対象コンポーネントと選択外項目の変更前状態を保持します。</summary>
    [Serializable]
    internal sealed class PlayModeTuningComponentRecord
    {
        /// <summary>対象コンポーネントの識別情報をまとめた版指紋です。</summary>
        public string componentKey = string.Empty;

        /// <summary>対象コンポーネントが属するシーンのパスです。</summary>
        public string scenePath = string.Empty;

        /// <summary>変更前の対象シーン階層と選択外項目をまとめた版指紋です。</summary>
        public string baselineUnselectedFingerprint = string.Empty;
    }

    internal sealed class PlayModeTuningEncodedValue
    {
        internal PlayModeTuningEncodedValue(PlayModeTuningValueKind kind, string payload, string display)
        {
            Kind = kind;
            Payload = payload ?? string.Empty;
            Display = display ?? string.Empty;
        }

        internal PlayModeTuningValueKind Kind { get; }
        internal string Payload { get; }
        internal string Display { get; }
        internal bool EqualsExact(PlayModeTuningEncodedValue other)
        {
            return other != null && Kind == other.Kind && StringComparer.Ordinal.Equals(Payload, other.Payload);
        }
    }

    internal sealed class PlayModeTuningEnvironment
    {
        internal PlayModeTuningEnvironment(bool playing, bool playingOrWillChange, bool compiling, bool updating, bool sceneReloadDisabled, bool domainReloadDisabled)
        {
            Playing = playing;
            PlayingOrWillChange = playingOrWillChange;
            Compiling = compiling;
            Updating = updating;
            SceneReloadDisabled = sceneReloadDisabled;
            DomainReloadDisabled = domainReloadDisabled;
        }

        internal bool Playing { get; }
        internal bool PlayingOrWillChange { get; }
        internal bool Compiling { get; }
        internal bool Updating { get; }
        internal bool SceneReloadDisabled { get; }
        internal bool DomainReloadDisabled { get; }
    }

    internal sealed class PlayModeTuningGatewayPropertySnapshot
    {
        internal PlayModeTuningGatewayPropertySnapshot(PlayModeTuningPropertyRecord record, PlayModeTuningEncodedValue value)
        {
            Record = record;
            Value = value;
        }

        internal PlayModeTuningPropertyRecord Record { get; }
        internal PlayModeTuningEncodedValue Value { get; }
    }

    internal sealed class PlayModeTuningGatewayComponentSnapshot
    {
        internal PlayModeTuningGatewayComponentSnapshot(string componentKey, string scenePath, string unselectedFingerprint)
        {
            ComponentKey = componentKey ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            UnselectedFingerprint = unselectedFingerprint ?? string.Empty;
        }

        internal string ComponentKey { get; }
        internal string ScenePath { get; }
        internal string UnselectedFingerprint { get; }
    }

    internal sealed class PlayModeTuningGatewaySnapshot
    {
        internal PlayModeTuningGatewaySnapshot(IEnumerable<PlayModeTuningGatewayPropertySnapshot> properties, IEnumerable<PlayModeTuningGatewayComponentSnapshot> components)
        {
            var orderedProperties = PlayModeTuningIdentityOrder.OrderProperties(properties, item => item.Record).ToArray();
            Properties = Array.AsReadOnly(orderedProperties);
            Components = Array.AsReadOnly(PlayModeTuningIdentityOrder.OrderComponents(components, item => item.ComponentKey, item => item.ScenePath, orderedProperties.Select(item => item.Record)).ToArray());
        }

        internal IReadOnlyList<PlayModeTuningGatewayPropertySnapshot> Properties { get; }
        internal IReadOnlyList<PlayModeTuningGatewayComponentSnapshot> Components { get; }
    }

    internal sealed class PlayModeTuningGatewayResult
    {
        private PlayModeTuningGatewayResult(PlayModeTuningError error, string message, PlayModeTuningGatewaySnapshot snapshot)
        {
            Error = error;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        internal PlayModeTuningError Error { get; }
        internal string Message { get; }
        internal PlayModeTuningGatewaySnapshot Snapshot { get; }
        internal bool Succeeded => Error == PlayModeTuningError.None && Snapshot != null;

        internal static PlayModeTuningGatewayResult Success(PlayModeTuningGatewaySnapshot snapshot)
        {
            return new PlayModeTuningGatewayResult(PlayModeTuningError.None, string.Empty, snapshot);
        }

        internal static PlayModeTuningGatewayResult Failure(PlayModeTuningError error, string message)
        {
            return new PlayModeTuningGatewayResult(error, message, null);
        }
    }

    internal sealed class PlayModeTuningMutationResult
    {
        private PlayModeTuningMutationResult(PlayModeTuningError error, string message)
        {
            Error = error;
            Message = message ?? string.Empty;
        }

        internal PlayModeTuningError Error { get; }
        internal string Message { get; }
        internal bool Succeeded => Error == PlayModeTuningError.None;

        internal static PlayModeTuningMutationResult Success()
        {
            return new PlayModeTuningMutationResult(PlayModeTuningError.None, string.Empty);
        }

        internal static PlayModeTuningMutationResult Failure(PlayModeTuningError error, string message)
        {
            return new PlayModeTuningMutationResult(error, message);
        }
    }

    internal sealed class PlayModeTuningWrite
    {
        internal PlayModeTuningWrite(PlayModeTuningPropertyRecord record, PlayModeTuningEncodedValue value)
        {
            Record = record;
            Value = value;
        }

        internal PlayModeTuningPropertyRecord Record { get; }
        internal PlayModeTuningEncodedValue Value { get; }
    }
}
