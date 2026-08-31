using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>ビルド実行アシスタントの1回の実行について、Unityオブジェクトに依存しない変更不能な終了記録を保持します。</summary>
    public sealed class BuildAssistantHistoryEntry
    {
        private readonly ReadOnlyCollection<string> effectiveDefines;
        private readonly ReadOnlyCollection<BuildAssistantScene> scenes;
        private readonly ReadOnlyCollection<BuildAssistantAssetSize> assets;
        private readonly ReadOnlyCollection<BuildAssistantTypeSize> types;

        internal BuildAssistantHistoryEntry(string runId, DateTime createdAtUtc, DateTime startedAtUtc, DateTime completedAtUtc, BuildAssistantHistoryStatus status, BuildAssistantError error, string message, string outputRoot, string runDirectory, string artifactPath, BuildAssistantProfileKind profileKind, string profileGuid, string profileName, string profilePath, string profileDependencyHash, string profileStableId, BuildTarget target, BuildTargetGroup targetGroup, string namedBuildTarget, int subtarget, ScriptingImplementation scriptingBackend, BuildOptions options, IEnumerable<string> effectiveDefines, IEnumerable<BuildAssistantScene> scenes, int totalErrors, int totalWarnings, ulong totalOutputBytes, ulong packedContentBytes, ulong packedOverheadBytes, IEnumerable<BuildAssistantAssetSize> assets, IEnumerable<BuildAssistantTypeSize> types, string previousRunId, long totalOutputDeltaBytes, long packedContentDeltaBytes)
        {
            RunId = runId ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            Status = status;
            Error = error;
            Message = message ?? string.Empty;
            OutputRoot = outputRoot ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            ArtifactPath = artifactPath ?? string.Empty;
            ProfileKind = profileKind;
            ProfileGuid = profileGuid ?? string.Empty;
            ProfileName = profileName ?? string.Empty;
            ProfilePath = profilePath ?? string.Empty;
            ProfileDependencyHash = profileDependencyHash ?? string.Empty;
            ProfileStableId = profileStableId ?? string.Empty;
            Target = target;
            TargetGroup = targetGroup;
            NamedBuildTarget = namedBuildTarget ?? string.Empty;
            Subtarget = subtarget;
            ScriptingBackend = scriptingBackend;
            Options = options;
            this.effectiveDefines = Array.AsReadOnly((effectiveDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<BuildAssistantScene>()).ToArray());
            TotalErrors = totalErrors;
            TotalWarnings = totalWarnings;
            TotalOutputBytes = totalOutputBytes;
            PackedContentBytes = packedContentBytes;
            PackedOverheadBytes = packedOverheadBytes;
            this.assets = Array.AsReadOnly((assets ?? Enumerable.Empty<BuildAssistantAssetSize>()).ToArray());
            this.types = Array.AsReadOnly((types ?? Enumerable.Empty<BuildAssistantTypeSize>()).ToArray());
            PreviousRunId = previousRunId ?? string.Empty;
            TotalOutputDeltaBytes = totalOutputDeltaBytes;
            PackedContentDeltaBytes = packedContentDeltaBytes;
        }

        /// <summary>実行識別子を取得します。</summary>
        public string RunId { get; }

        /// <summary>計画を作成した協定世界時を取得します。</summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>ビルド呼び出しを開始した協定世界時を取得します。</summary>
        public DateTime StartedAtUtc { get; }

        /// <summary>ビルド実行アシスタントが終了を記録した協定世界時を取得します。</summary>
        public DateTime CompletedAtUtc { get; }

        /// <summary>記録した協定世界時から求めた、0以上の経過時間を取得します。</summary>
        public TimeSpan Duration => CompletedAtUtc >= StartedAtUtc ? CompletedAtUtc - StartedAtUtc : TimeSpan.Zero;

        /// <summary>実行の終了状態を取得します。</summary>
        public BuildAssistantHistoryStatus Status { get; }

        /// <summary>終了時の定義済みエラーを取得します。</summary>
        public BuildAssistantError Error { get; }

        /// <summary>Unityオブジェクトに依存しない終了時の診断文を取得します。</summary>
        public string Message { get; }

        /// <summary>実行に使った出力先基準フォルダーを取得します。</summary>
        public string OutputRoot { get; }

        /// <summary>この実行専用のフォルダーを取得します。</summary>
        public string RunDirectory { get; }

        /// <summary>対象機種に応じたプレイヤー成果物のパスを取得します。</summary>
        public string ArtifactPath { get; }

        /// <summary>実行に使ったプロファイルの種類を取得します。</summary>
        public BuildAssistantProfileKind ProfileKind { get; }

        /// <summary>独自プロファイルのGUIDを取得します。機種別プロファイルでは空文字列です。</summary>
        public string ProfileGuid { get; }

        /// <summary>プロファイルの表示名を取得します。</summary>
        public string ProfileName { get; }

        /// <summary>独自プロファイル素材のパスを取得します。機種別プロファイルでは空文字列です。</summary>
        public string ProfilePath { get; }

        /// <summary>設定、従来形式のプロファイル、取り込み済みの内容、パッケージ目録、ストリーミング用素材、独自プロファイルの依存関係をまとめた照合用要約値を取得します。</summary>
        public string ProfileDependencyHash { get; }

        /// <summary>比較対象を対応付けるための、安定したプロファイル識別値を取得します。</summary>
        public string ProfileStableId { get; }

        /// <summary>デスクトップ単体実行形式の対象機種を取得します。</summary>
        public BuildTarget Target { get; }

        /// <summary>対象機種群を取得します。</summary>
        public BuildTargetGroup TargetGroup { get; }

        /// <summary>Unityの名前付き対象機種名を取得します。</summary>
        public string NamedBuildTarget { get; }

        /// <summary>単体実行形式の対象種別値を取得します。</summary>
        public int Subtarget { get; }

        /// <summary>スクリプト処理方式を取得します。</summary>
        public ScriptingImplementation ScriptingBackend { get; }

        /// <summary>比較用に記録した、実際に使われた正規化済みのビルド選択肢を取得します。</summary>
        public BuildOptions Options { get; }

        /// <summary>実際に使われたスクリプト定義の、保護された読み取り専用一覧を取得します。</summary>
        public IReadOnlyList<string> EffectiveDefines => effectiveDefines;

        /// <summary>順序どおりに記録したシーンの、保護された読み取り専用一覧を取得します。</summary>
        public IReadOnlyList<BuildAssistantScene> Scenes => scenes;

        /// <summary>ビルド報告に含まれるエラー数を取得します。</summary>
        public int TotalErrors { get; }

        /// <summary>ビルド報告に含まれる警告数を取得します。</summary>
        public int TotalWarnings { get; }

        /// <summary>格納内容と付加情報の容量とは分けて記録した、Unityによる出力全体のバイト数を取得します。</summary>
        public ulong TotalOutputBytes { get; }

        /// <summary>全素材の各格納項目を検査しながら合計したバイト数を取得します。</summary>
        public ulong PackedContentBytes { get; }

        /// <summary>全格納ファイルの付加情報容量を検査しながら合計したバイト数を取得します。</summary>
        public ulong PackedOverheadBytes { get; }

        /// <summary>格納済み素材の行を、容量の降順、同容量では素材識別キーの文字順で取得します。</summary>
        public IReadOnlyList<BuildAssistantAssetSize> Assets => assets;

        /// <summary>格納済み型の行を、容量の降順、同容量では型識別キーの文字順で取得します。</summary>
        public IReadOnlyList<BuildAssistantTypeSize> Types => types;

        /// <summary>直前の互換性がある成功実行の識別子を取得します。該当しない場合は空文字列です。</summary>
        public string PreviousRunId { get; }

        /// <summary>出力全体のバイト数から、直前の比較可能な実行における出力全体のバイト数を引いた差分を取得します。</summary>
        public long TotalOutputDeltaBytes { get; }

        /// <summary>格納内容のバイト数から、直前の比較可能な実行における格納内容のバイト数を引いた差分を取得します。</summary>
        public long PackedContentDeltaBytes { get; }
    }
}
