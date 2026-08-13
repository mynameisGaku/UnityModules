using UnityEditor;
using UnityEngine;

namespace Inspector.Editor
{
    /// <summary>
    /// 描画で使い回す <see cref="GUIStyle"/> と小さな部品。
    /// <para>
    /// <see cref="GUIStyle"/> を毎フレーム作ると、そのぶんだけ確保が走る。
    /// エディタ拡張で一番よくある無駄なので、まとめて 1 回だけ作る。
    /// </para>
    /// </summary>
    public static class InspectorStyles
    {
        private static GUIStyle _title;
        private static GUIStyle _subtitle;
        private static GUIStyle _boxHeader;
        private static GUIStyle _suffix;
        private static GUIStyle _inlineButton;
        private static GUIStyle _centered;
        private static Texture2D _lineTexture;

        /// <summary>見出し。</summary>
        public static GUIStyle Title => _title ?? (_title = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = true,
        });

        /// <summary>見出しに添える小さな説明。</summary>
        public static GUIStyle Subtitle => _subtitle ?? (_subtitle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
        });

        /// <summary>枠囲みグループの見出し。</summary>
        public static GUIStyle BoxHeader => _boxHeader ?? (_boxHeader = new GUIStyle(EditorStyles.boldLabel)
        {
            margin = new RectOffset(0, 0, 0, 2),
        });

        /// <summary>値の右に添える単位。</summary>
        public static GUIStyle Suffix => _suffix ?? (_suffix = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(2, 0, 0, 0),
        });

        /// <summary>バーの中央に重ねる文字。</summary>
        public static GUIStyle CenteredLabel => _centered ?? (_centered = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        });

        /// <summary>フィールドの横に置く小さなボタン。</summary>
        public static GUIStyle InlineButton => _inlineButton ?? (_inlineButton = new GUIStyle(EditorStyles.miniButton)
        {
            padding = new RectOffset(4, 4, 0, 0),
        });

        /// <summary>区切り線を引く。</summary>
        public static void HorizontalLine(float height, Color color, float spaceBefore, float spaceAfter)
        {
            if (spaceBefore > 0f) GUILayout.Space(spaceBefore);

            var rect = EditorGUILayout.GetControlRect(false, Mathf.Max(1f, height));
            EditorGUI.DrawRect(rect, color);

            if (spaceAfter > 0f) GUILayout.Space(spaceAfter);
        }

        /// <summary>塗り潰した矩形を描くための 1 ピクセルのテクスチャ。</summary>
        public static Texture2D WhitePixel
        {
            get
            {
                if (_lineTexture != null) return _lineTexture;

                _lineTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _lineTexture.SetPixel(0, 0, Color.white);
                _lineTexture.Apply();
                return _lineTexture;
            }
        }

        /// <summary><see cref="InfoBoxKind"/> を Unity 側の種類に直す。</summary>
        public static MessageType ToMessageType(InfoBoxKind kind)
        {
            switch (kind)
            {
                case InfoBoxKind.Info: return MessageType.Info;
                case InfoBoxKind.Warning: return MessageType.Warning;
                case InfoBoxKind.Error: return MessageType.Error;
                default: return MessageType.None;
            }
        }
    }
}
