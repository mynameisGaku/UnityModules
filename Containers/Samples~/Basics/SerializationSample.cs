using System;
using System.Collections.Generic;
using Containers;
using UnityEngine;

namespace Containers.Samples
{
    /// <summary>サンプル用の敵の種別。</summary>
    public enum EnemyKind
    {
        /// <summary>雑魚。</summary>
        Grunt,

        /// <summary>射手。</summary>
        Archer,

        /// <summary>ボス。</summary>
        Boss
    }

    /// <summary>サンプル用のダメージ種別。</summary>
    public enum DamageType
    {
        /// <summary>物理。</summary>
        Physical,

        /// <summary>炎。</summary>
        Fire,

        /// <summary>氷。</summary>
        Ice
    }

    /// <summary>敵の種別ごとの出現重み。</summary>
    [Serializable]
    public sealed class SpawnWeights : SerializableDictionary<EnemyKind, float> { }

    /// <summary>ダメージ種別ごとの倍率。</summary>
    [Serializable]
    public sealed class DamageMultipliers : EnumMap<DamageType, float> { }

    /// <summary>
    /// シリアライズ対応コンテナが Inspector でどう見えるかのサンプル。
    /// <para>
    /// このコンポーネントを空の GameObject に付けて Inspector を見ると、
    /// 辞書がキーと値の並んだ行として編集でき、重複キーが赤く出て、
    /// <see cref="Optional{T}"/> がチェックボックス付きの 1 行になり、
    /// <see cref="EnumMap{TEnum,TValue}"/> が列挙名をラベルにして並ぶのが確認できる。
    /// </para>
    /// </summary>
    public sealed class SerializationSample : MonoBehaviour
    {
        [Header("辞書 — キー重複はここで警告される")]
        [SerializeField] private SpawnWeights _spawnWeights = new SpawnWeights();

        [Header("enum 別の値 — 設定漏れが目で見える")]
        [SerializeField] private DamageMultipliers _damageMultipliers = new DamageMultipliers();

        [Header("上書き設定 — チェックを外すと既定値が使われる")]
        [SerializeField] private Optional<float> _moveSpeedOverride;

        [SerializeField] private float _defaultMoveSpeed = 5f;

        [Header("範囲 — スライダーで編集する")]
        [SerializeField, MinMaxSlider(0f, 100f)] private FloatRange _damageRange = new FloatRange(10f, 25f);

        [Header("安定 ID — セーブデータからの参照に使う")]
        [SerializeField] private SerializableGuid _instanceId;

        [Header("型の選択 — 派生クラスをドロップダウンで選ぶ")]
        [SerializeField, TypeFilter(typeof(ScriptableObject))] private SerializableType _configType;

        /// <summary>上書きがあればそれを、無ければ既定値を返す。</summary>
        public float MoveSpeed => _moveSpeedOverride.GetValueOrDefault(_defaultMoveSpeed);

        private void Start()
        {
            if (_instanceId.IsEmpty) _instanceId = SerializableGuid.NewGuid();

            Debug.Log($"移動速度: {MoveSpeed}（上書き: {_moveSpeedOverride.HasValue}）");
            Debug.Log($"ダメージ: {_damageRange.Random():F1}");
            Debug.Log($"炎の倍率: {_damageMultipliers[DamageType.Fire]}");
            Debug.Log($"設定の型: {_configType}");

            foreach (var pair in _spawnWeights)
            {
                Debug.Log($"出現重み {pair.Key} = {pair.Value}");
            }
        }

        /// <summary>出現重みに従って敵の種別を 1 つ引く。</summary>
        public EnemyKind DrawSpawn()
        {
            var total = 0f;
            foreach (var pair in _spawnWeights) total += pair.Value;

            var roll = UnityEngine.Random.Range(0f, total);
            foreach (var pair in _spawnWeights)
            {
                roll -= pair.Value;
                if (roll <= 0f) return pair.Key;
            }

            return EnemyKind.Grunt;
        }
    }
}
