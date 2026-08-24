using System;
using UnityEngine;

namespace InputDeviceDisplay
{
    [Serializable]
    internal sealed class InputDeviceDisplayLayoutOverride
    {
        [SerializeField]
        private string _layoutName = string.Empty;

        [SerializeField]
        private InputDeviceDisplayStyle _style = InputDeviceDisplayStyle.GenericGamepad;

        /// <summary>完全一致するlayout名と使用する表記体系を保持する。</summary>
        /// <param name="layoutName">完全一致で照合するInput System layout名。</param>
        /// <param name="style">一致時に使用する表記体系。Unknownは指定できない。</param>
        internal InputDeviceDisplayLayoutOverride(string layoutName, InputDeviceDisplayStyle style)
        {
            _layoutName = layoutName;
            _style = style;
        }

        /// <summary>完全一致で照合するInput System layout名。</summary>
        internal string LayoutName => _layoutName;

        /// <summary>layout一致時に使用する表記体系。</summary>
        internal InputDeviceDisplayStyle Style => _style;
    }
}
