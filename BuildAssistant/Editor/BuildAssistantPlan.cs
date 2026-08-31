using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>作成後の入力変更を検出できる、変更不能なデスクトップ単体実行形式のビルド計画を表します。</summary>
    public sealed class BuildAssistantPlan
    {
        private readonly ReadOnlyCollection<string> extraScriptingDefines;
        private readonly ReadOnlyCollection<string> effectiveDefines;
        private readonly ReadOnlyCollection<BuildAssistantScene> scenes;

        internal BuildAssistantPlan(BuildAssistantError error, string message, string runId, DateTime createdAtUtc, string outputRoot, string runDirectory, string artifactPath, OutputRootMode outputRootMode, ProfileSnapshot profile, BuildTarget target, BuildTargetGroup targetGroup, string namedBuildTarget, int subtarget, ScriptingImplementation scriptingBackend, BuildOptions options, BuildOptions invocationOptions, string assetBundleManifestPath, IEnumerable<string> extraScriptingDefines, IEnumerable<string> effectiveDefines, IEnumerable<BuildAssistantScene> scenes, BuildAssistantHistoryEntry previousComparableSuccess)
        {
            Error = error;
            Message = message ?? string.Empty;
            RunId = runId ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            OutputRoot = outputRoot ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            ArtifactPath = artifactPath ?? string.Empty;
            OutputRootMode = outputRootMode;
            ProfileKind = profile?.Kind ?? BuildAssistantProfileKind.Platform;
            ProfileGuid = profile?.Guid ?? string.Empty;
            ProfileName = profile?.Name ?? string.Empty;
            ProfilePath = profile?.AssetPath ?? string.Empty;
            ProfileDependencyHash = profile?.DependencyHash ?? string.Empty;
            ProfileStableId = profile?.StableId ?? string.Empty;
            Target = target;
            TargetGroup = targetGroup;
            NamedBuildTarget = namedBuildTarget ?? string.Empty;
            Subtarget = subtarget;
            ScriptingBackend = scriptingBackend;
            Options = options;
            InvocationOptions = invocationOptions;
            AssetBundleManifestPath = assetBundleManifestPath ?? string.Empty;
            this.extraScriptingDefines = Array.AsReadOnly((extraScriptingDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.effectiveDefines = Array.AsReadOnly((effectiveDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<BuildAssistantScene>()).ToArray());
            PreviousComparableSuccess = previousComparableSuccess;
        }

        /// <summary>計画作成時の定義済みエラーを取得します。実行可能な計画ではエラーなしを示します。</summary>
        public BuildAssistantError Error { get; }

        /// <summary>エディター画面へ表示できる診断文を取得します。</summary>
        public string Message { get; }

        /// <summary>計画作成時の検査に合格し、実行可能かどうかを取得します。</summary>
        public bool IsReady => Error == BuildAssistantError.None;

        /// <summary>計画作成時に生成された安定した実行識別子を取得します。</summary>
        public string RunId { get; }

        /// <summary>再現可能な計画作成へ渡された協定世界時を取得します。</summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>正規化された出力先基準フォルダーの絶対パスを取得します。</summary>
        public string OutputRoot { get; }

        /// <summary>この実行専用に予約された、正規化済みフォルダーの絶対パスを取得します。</summary>
        public string RunDirectory { get; }

        /// <summary>対象機種に応じたプレイヤー成果物のパスを取得します。</summary>
        public string ArtifactPath { get; }

        /// <summary>計画が機種別プロファイルと独自ビルドプロファイル素材のどちらを使うかを取得します。</summary>
        public BuildAssistantProfileKind ProfileKind { get; }

        /// <summary>独自ビルドプロファイル素材のGUIDを取得します。機種別プロファイルでは空文字列です。</summary>
        public string ProfileGuid { get; }

        /// <summary>記録したプロファイル表示名を取得します。</summary>
        public string ProfileName { get; }

        /// <summary>独自ビルドプロファイル素材のパスを取得します。機種別プロファイルでは空文字列です。</summary>
        public string ProfilePath { get; }

        /// <summary>設定、従来形式のプロファイル、取り込み済みの内容、パッケージ目録、ストリーミング用素材、独自プロファイルの依存関係をまとめた照合用要約値を取得します。</summary>
        public string ProfileDependencyHash { get; }

        /// <summary>互換性のある履歴を比較するための、安定したプロファイル識別値を取得します。</summary>
        public string ProfileStableId { get; }

        /// <summary>記録したデスクトップ単体実行形式の対象機種を取得します。</summary>
        public BuildTarget Target { get; }

        /// <summary>記録した対象機種群を取得します。</summary>
        public BuildTargetGroup TargetGroup { get; }

        /// <summary>記録したUnityの名前付き対象機種名を取得します。</summary>
        public string NamedBuildTarget { get; }

        /// <summary>記録した単体実行形式の対象種別値を取得します。</summary>
        public int Subtarget { get; }

        /// <summary>記録したスクリプト処理方式を取得します。</summary>
        public ScriptingImplementation ScriptingBackend { get; }

        /// <summary>独自プロファイルのビルド方式と圧縮指定を含む、実際に使われる正規化済みのビルド選択肢を取得します。</summary>
        public BuildOptions Options { get; }

        /// <summary>指定されている場合、記録したアセットバンドル目録のパスを取得します。</summary>
        public string AssetBundleManifestPath { get; }

        /// <summary>ビルド時だけ追加するスクリプト定義の、保護された読み取り専用一覧を取得します。</summary>
        public IReadOnlyList<string> ExtraScriptingDefines => extraScriptingDefines;

        /// <summary>全体設定、プロファイル、ビルド時限定の定義を統合した、保護された読み取り専用一覧を取得します。</summary>
        public IReadOnlyList<string> EffectiveDefines => effectiveDefines;

        /// <summary>無効なシーンも含め、順序どおりに記録した全シーンの保護された読み取り専用一覧を取得します。</summary>
        public IReadOnlyList<BuildAssistantScene> Scenes => scenes;

        /// <summary>計画作成時に互換性のある成功履歴が存在した場合、その最新項目を取得します。</summary>
        public BuildAssistantHistoryEntry PreviousComparableSuccess { get; }

        internal OutputRootMode OutputRootMode { get; }
        internal BuildOptions InvocationOptions { get; }
    }
}
