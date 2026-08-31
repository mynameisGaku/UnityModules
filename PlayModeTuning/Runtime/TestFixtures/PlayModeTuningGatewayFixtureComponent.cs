using UnityEngine;

namespace PlayModeTuning.Editor.Tests
{
    /// <summary>実際のシーン上で反映と取り消しを検証するためのコンポーネントです。</summary>
    public sealed class PlayModeTuningGatewayFixtureComponent : MonoBehaviour
    {
        /// <summary>反映対象として扱う整数値です。</summary>
        [InspectorName("反映対象値")]
        public int selectedValue = 10;

        /// <summary>反映対象外の副作用を確認する整数値です。</summary>
        [InspectorName("反映対象外値")]
        public int unselectedValue = 20;

        /// <summary>反映対象値の検証時に変更する別コンポーネントです。</summary>
        [InspectorName("副作用の変更先")]
        public PlayModeTuningGatewayFixtureComponent sideEffectTarget;

        private void OnValidate()
        {
            if (selectedValue == 71 && sideEffectTarget != null)
                sideEffectTarget.unselectedValue = 92;
        }
    }
}
