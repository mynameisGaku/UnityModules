using System;
using System.IO;
using UnityEngine;

namespace SaveSystem.Samples
{
    /// <summary>
    /// 保存したコイン数と Play 開始回数を画面に出し、再起動を跨ぐ保存を確認できるサンプル。
    /// Game View のボタンとコンポーネントの Context Menu は同じ操作を行う。
    /// </summary>
    [AddComponentMenu("StudioGaku/Save System Basics Sample")]
    public sealed class SaveSystemSample : MonoBehaviour
    {
        private const string FolderName = "SaveSystemBasics";
        private const string SlotName = "basic";
        private const string DataVersion = "1";

        [SerializeField] private int _coins = 100;
        [SerializeField] private int _playCount;
        [SerializeField, TextArea] private string _lastResult = "Scene を開いて Play してください。";

        private SaveService _saves;

        /// <summary>保存対象にする、ゲーム固有の最小データ。</summary>
        [Serializable]
        private sealed class SampleSaveData
        {
            public int Coins;
            public int PlayCount;
        }

        /// <summary>前回の状態を読み、今回の Play 開始を保存する。</summary>
        private void Awake()
        {
            if (!TryEnsureService()) return;

            var loaded = _saves.Load<SampleSaveData>(SlotName, DataVersion);
            if (loaded.IsSuccess)
            {
                Apply(loaded.Value);
                _playCount++;
                SaveState($"前回の保存を読み込みました。Play 開始回数を {_playCount} に更新して保存しました。");
                return;
            }

            if (loaded.Error == SaveError.NotFound)
            {
                _playCount = 1;
                SaveState("初回データを作成しました。Play を止めて再開すると開始回数が増えます。");
                return;
            }

            SetStatus($"起動時の読み込みに失敗しました: {loaded.Error} / {loaded.Message} 元データは変更していません。");
        }

        /// <summary>保存状態と操作ボタンを Game View に表示する。</summary>
        private void OnGUI()
        {
            var width = Mathf.Max(280f, Mathf.Min(520f, Screen.width - 48f));
            var height = Mathf.Max(300f, Screen.height - 48f);

            GUILayout.BeginArea(new Rect(24f, 24f, width, height), GUI.skin.box);
            GUILayout.Label("Save System Basics", GUI.skin.box);
            GUILayout.Space(8f);
            GUILayout.Label($"現在のコイン: {_coins:N0}");
            GUILayout.Label($"保存を読み込んだ Play 開始回数: {_playCount:N0}");
            GUILayout.Space(8f);

            if (GUILayout.Button("コインを 100 増やす（未保存）")) AddCoins();
            if (GUILayout.Button("現在の状態を保存")) SaveCurrentState();
            if (GUILayout.Button("保存した状態を読み込む")) LoadState();
            if (GUILayout.Button("サンプルの保存を削除")) DeleteState();

            GUILayout.Space(12f);
            GUILayout.Label("最後の結果:");
            GUILayout.Label(_lastResult);
            GUILayout.Space(8f);
            GUILayout.Label("保存先:");
            GUILayout.Label(Path.Combine(Application.persistentDataPath, FolderName, SlotName + ".save"));
            GUILayout.EndArea();
        }

        /// <summary>保存前の変化が分かるように、コインだけを増やす。</summary>
        [ContextMenu("Save System/コインを 100 増やす（未保存）")]
        private void AddCoins()
        {
            if (_coins <= int.MaxValue - 100) _coins += 100;
            SetStatus("コインを増やしました。まだ保存していません。");
        }

        /// <summary>Inspector と Game View にある現在値を保存する。</summary>
        [ContextMenu("Save System/現在の状態を保存")]
        private void SaveCurrentState()
        {
            if (!TryEnsureService()) return;
            SaveState("現在の状態を保存しました。");
        }

        /// <summary>保存済みの値を読み込み、表示中の値へ反映する。</summary>
        [ContextMenu("Save System/保存した状態を読み込む")]
        private void LoadState()
        {
            if (!TryEnsureService()) return;

            var loaded = _saves.Load<SampleSaveData>(SlotName, DataVersion);
            if (!loaded.IsSuccess)
            {
                SetStatus($"読み込みに失敗しました: {loaded.Error} / {loaded.Message} 表示中の値と元データは変更していません。");
                return;
            }

            Apply(loaded.Value);
            var recovery = loaded.Metadata.RecoveredFromBackup ? " バックアップから復旧しました。" : string.Empty;
            SetStatus($"保存した状態を読み込みました。{recovery}");
        }

        /// <summary>このサンプル専用スロットを削除し、画面上の値を初期値へ戻す。</summary>
        [ContextMenu("Save System/サンプルの保存を削除")]
        private void DeleteState()
        {
            if (!TryEnsureService()) return;

            var deleted = _saves.Delete(SlotName);
            if (!deleted.IsSuccess)
            {
                SetStatus($"削除に失敗しました: {deleted.Error} / {deleted.Message}");
                return;
            }

            _coins = 100;
            _playCount = 0;
            SetStatus("サンプルの保存を削除しました。次の Play 開始時に初回データを作ります。");
        }

        /// <summary>現在値を保存し、成功理由を画面へ出す。</summary>
        /// <param name="successMessage">保存成功時の説明。</param>
        private void SaveState(string successMessage)
        {
            var data = new SampleSaveData
            {
                Coins = _coins,
                PlayCount = _playCount,
            };

            var saved = _saves.Save(SlotName, data, DataVersion);
            if (!saved.IsSuccess)
            {
                SetStatus($"保存に失敗しました: {saved.Error} / {saved.Message}");
                return;
            }

            SetStatus(successMessage);
        }

        /// <summary>読み込んだ値を画面表示用の状態へ反映する。</summary>
        /// <param name="data">検証を通った保存データ。</param>
        private void Apply(SampleSaveData data)
        {
            _coins = data.Coins;
            _playCount = data.PlayCount;
        }

        /// <summary>標準構成を必要な時だけ作り、利用できない環境では理由を表示する。</summary>
        private bool TryEnsureService()
        {
            if (_saves != null) return true;

            try
            {
                _saves = SaveService.CreateDefault(FolderName);
                return true;
            }
            catch (Exception exception)
            {
                SetStatus($"保存先を作れませんでした: {exception.Message}");
                return false;
            }
        }

        /// <summary>画面と Console に同じ操作結果を残す。</summary>
        /// <param name="message">利用者へ見せる結果。</param>
        private void SetStatus(string message)
        {
            _lastResult = message;
            Debug.Log($"[Save System Basics] {message}", this);
        }
    }
}
