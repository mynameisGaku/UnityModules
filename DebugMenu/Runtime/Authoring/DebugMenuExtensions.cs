using System;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// メニューを短く組み立てるための拡張。
    /// <para>
    /// 生成した行をそのまま返すので、<c>WithRange</c> や <c>Unit</c> を続けて書ける。
    /// 親を差し替えられるよう <see cref="DebugElement"/> 側に生やしてあり、
    /// ページ直下でもグループの中でも同じ書き方になる。
    /// </para>
    /// <code>
    /// page.Group("プレイヤー", g =>
    /// {
    ///     g.Bool("無敵", () => p.Invincible, v => p.Invincible = v);
    ///     g.Float("移動速度", () => p.Speed, v => p.Speed = v).WithRange(0f, 20f).Unit = "m/s";
    ///     g.Action("HP 全回復", () => p.Heal());
    /// });
    /// </code>
    /// </summary>
    public static class DebugMenuExtensions
    {
        // ── ページ直下へ足す（Root への転送） ────────────────────────────────

        /// <summary>ページ直下に見出しを足し、その中身をその場で組み立てる。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">見出し名。</param>
        /// <param name="build">中身を組み立てる処理。</param>
        public static DebugGroup Group(this DebugPage page, string label, Action<DebugElement> build = null)
        {
            var group = page.Root.Group(label, build);
            page.Invalidate();
            return group;
        }

        /// <summary>ページ直下に行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="element">足す行。</param>
        public static T Add<T>(this DebugPage page, T element) where T : DebugElement
        {
            var added = page.Root.Add(element);
            page.Invalidate();
            return added;
        }

        // 以下はページ直下へ足すための転送。
        // 拡張メソッドの解決は暗黙変換をたどらないので、DebugPage を受け取る形を
        // 別に用意しないと page.Bool(...) と書けない（page.Root.Bool(...) を強いることになる）。

        /// <summary>ページ直下に区切りを足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">区切りに添える文字。</param>
        public static DebugSeparator Separator(this DebugPage page, string label = null) =>
            page.Add(new DebugSeparator(label));

        /// <summary>ページ直下に、実行するだけの行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="action">決定で走らせる処理。</param>
        public static DebugAction Action(this DebugPage page, string label, Action action) =>
            page.Add(new DebugAction(label, action));

        /// <summary>ページ直下に真偽値の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugBool Bool(this DebugPage page, string label, Func<bool> getter, Action<bool> setter) =>
            page.Add(new DebugBool(label, getter, setter));

        /// <summary>ページ直下に、行自身が値を抱える真偽値の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public static DebugBool Bool(this DebugPage page, string label, bool initialValue = false) =>
            page.Add(new DebugBool(label, initialValue));

        /// <summary>ページ直下に整数の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugInt Int(this DebugPage page, string label, Func<int> getter, Action<int> setter) =>
            page.Add(new DebugInt(label, getter, setter));

        /// <summary>ページ直下に、行自身が値を抱える整数の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public static DebugInt Int(this DebugPage page, string label, int initialValue = 0) =>
            page.Add(new DebugInt(label, initialValue));

        /// <summary>ページ直下に小数の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugFloat Float(this DebugPage page, string label, Func<float> getter, Action<float> setter) =>
            page.Add(new DebugFloat(label, getter, setter));

        /// <summary>ページ直下に、行自身が値を抱える小数の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public static DebugFloat Float(this DebugPage page, string label, float initialValue = 0f) =>
            page.Add(new DebugFloat(label, initialValue));

        /// <summary>ページ直下に enum の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugEnum Enum<TEnum>(this DebugPage page, string label, Func<TEnum> getter, Action<TEnum> setter)
            where TEnum : struct, Enum =>
            page.Add(DebugEnum.OfEnum(label, getter, setter));

        /// <summary>ページ直下に、候補から選ぶ行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="options">候補の表示名。</param>
        /// <param name="getter">現在の選択位置を返す関数。</param>
        /// <param name="setter">選択位置を書き込む関数。</param>
        public static DebugEnum Choice(this DebugPage page, string label, string[] options, Func<int> getter, Action<int> setter) =>
            page.Add(new DebugEnum(label, options, getter, setter));

        /// <summary>ページ直下に文字列の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugText Text(this DebugPage page, string label, Func<string> getter, Action<string> setter) =>
            page.Add(new DebugText(label, getter, setter));

        /// <summary>ページ直下に色の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugColor Color(this DebugPage page, string label, Func<Color> getter, Action<Color> setter) =>
            page.Add(new DebugColor(label, getter, setter));

        /// <summary>ページ直下に Vector3 の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugVector Vector(this DebugPage page, string label, Func<Vector3> getter, Action<Vector3> setter) =>
            page.Add(DebugVector.Of(label, getter, setter));

        /// <summary>ページ直下に、文字列を眺める行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="textProvider">右カラムへ出す文字列を返す関数。</param>
        public static DebugWatch Watch(this DebugPage page, string label, Func<string> textProvider) =>
            page.Add(new DebugWatch(label, textProvider));

        /// <summary>ページ直下に、数値を眺める行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="valueProvider">監視する値を返す関数。</param>
        /// <param name="digits">小数点以下の桁数。</param>
        public static DebugWatch Watch(this DebugPage page, string label, Func<float> valueProvider, int digits = 2) =>
            page.Add(new DebugWatch(label, valueProvider, digits));

        /// <summary>ページ直下に折れ線の行を足す。</summary>
        /// <param name="page">対象のページ。</param>
        /// <param name="label">表示名。</param>
        /// <param name="provider">標本にする値を返す関数。</param>
        /// <param name="sampleCount">保持する標本の数。</param>
        public static DebugGraph Graph(this DebugPage page, string label, Func<float> provider, int sampleCount = 120) =>
            page.Add(new DebugGraph(label, provider, sampleCount));

        // ── 見出しと区切り ──────────────────────────────────────────────────

        /// <summary>見出しを足し、その中身をその場で組み立てる。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">見出し名。</param>
        /// <param name="build">中身を組み立てる処理。</param>
        public static DebugGroup Group(this DebugElement parent, string label, Action<DebugElement> build = null)
        {
            var group = parent.Add(new DebugGroup(label));
            build?.Invoke(group);
            return group;
        }

        /// <summary>区切りを足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">区切りに添える文字。</param>
        public static DebugSeparator Separator(this DebugElement parent, string label = null) =>
            parent.Add(new DebugSeparator(label));

        // ── 値の行 ──────────────────────────────────────────────────────────

        /// <summary>実行するだけの行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="action">決定で走らせる処理。</param>
        public static DebugAction Action(this DebugElement parent, string label, Action action) =>
            parent.Add(new DebugAction(label, action));

        /// <summary>真偽値の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugBool Bool(this DebugElement parent, string label, Func<bool> getter, Action<bool> setter) =>
            parent.Add(new DebugBool(label, getter, setter));

        /// <summary>行自身が値を抱える真偽値の行を足す。メニューでしか使わないフラグ向け。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public static DebugBool Bool(this DebugElement parent, string label, bool initialValue = false) =>
            parent.Add(new DebugBool(label, initialValue));

        /// <summary>整数の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugInt Int(this DebugElement parent, string label, Func<int> getter, Action<int> setter) =>
            parent.Add(new DebugInt(label, getter, setter));

        /// <summary>行自身が値を抱える整数の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public static DebugInt Int(this DebugElement parent, string label, int initialValue = 0) =>
            parent.Add(new DebugInt(label, initialValue));

        /// <summary>小数の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugFloat Float(this DebugElement parent, string label, Func<float> getter, Action<float> setter) =>
            parent.Add(new DebugFloat(label, getter, setter));

        /// <summary>行自身が値を抱える小数の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public static DebugFloat Float(this DebugElement parent, string label, float initialValue = 0f) =>
            parent.Add(new DebugFloat(label, initialValue));

        /// <summary>enum の行を足す。候補は宣言順に並ぶ。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugEnum Enum<TEnum>(this DebugElement parent, string label, Func<TEnum> getter, Action<TEnum> setter)
            where TEnum : struct, Enum =>
            parent.Add(DebugEnum.OfEnum(label, getter, setter));

        /// <summary>候補から選ぶ行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="options">候補の表示名。</param>
        /// <param name="getter">現在の選択位置を返す関数。</param>
        /// <param name="setter">選択位置を書き込む関数。</param>
        public static DebugEnum Choice(this DebugElement parent, string label, string[] options, Func<int> getter, Action<int> setter) =>
            parent.Add(new DebugEnum(label, options, getter, setter));

        /// <summary>文字列の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugText Text(this DebugElement parent, string label, Func<string> getter, Action<string> setter) =>
            parent.Add(new DebugText(label, getter, setter));

        /// <summary>色の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugColor Color(this DebugElement parent, string label, Func<Color> getter, Action<Color> setter) =>
            parent.Add(new DebugColor(label, getter, setter));

        /// <summary>Vector3 の行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugVector Vector(this DebugElement parent, string label, Func<Vector3> getter, Action<Vector3> setter) =>
            parent.Add(DebugVector.Of(label, getter, setter));

        // ── 眺めるだけの行 ──────────────────────────────────────────────────

        /// <summary>文字列を眺める行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="textProvider">右カラムへ出す文字列を返す関数。</param>
        public static DebugWatch Watch(this DebugElement parent, string label, Func<string> textProvider) =>
            parent.Add(new DebugWatch(label, textProvider));

        /// <summary>数値を眺める行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="valueProvider">監視する値を返す関数。</param>
        /// <param name="digits">小数点以下の桁数。</param>
        public static DebugWatch Watch(this DebugElement parent, string label, Func<float> valueProvider, int digits = 2) =>
            parent.Add(new DebugWatch(label, valueProvider, digits));

        /// <summary>値の推移を折れ線で見る行を足す。</summary>
        /// <param name="parent">足す先。</param>
        /// <param name="label">表示名。</param>
        /// <param name="provider">標本にする値を返す関数。</param>
        /// <param name="sampleCount">保持する標本の数。</param>
        public static DebugGraph Graph(this DebugElement parent, string label, Func<float> provider, int sampleCount = 120) =>
            parent.Add(new DebugGraph(label, provider, sampleCount));

        // ── 共通の飾り付け ──────────────────────────────────────────────────

        /// <summary>説明文を設定して、そのまま行を返す。</summary>
        /// <param name="element">対象の行。</param>
        /// <param name="description">画面下へ出す説明文。</param>
        public static T Describe<T>(this T element, string description) where T : DebugElement
        {
            element.Description = description;
            return element;
        }

        /// <summary>単位を設定して、そのまま行を返す。</summary>
        /// <param name="element">対象の行。</param>
        /// <param name="unit">値へ添える単位。</param>
        public static T WithUnit<T>(this T element, string unit) where T : DebugElement
        {
            element.Unit = unit;
            return element;
        }

        /// <summary>注意色になる範囲を設定して、そのまま行を返す。</summary>
        /// <param name="element">対象の行。</param>
        /// <param name="min">この値を下回ると注意色。</param>
        /// <param name="max">この値を上回ると注意色。</param>
        public static T WarnOutside<T>(this T element, float min, float max) where T : DebugElement
        {
            element.SetWarnRange(min, max);
            return element;
        }

        /// <summary>ショートカットキーを割り当てて、そのまま行を返す。</summary>
        /// <param name="element">対象の行。</param>
        /// <param name="key">割り当てるキー。</param>
        public static T WithShortcut<T>(this T element, KeyCode key) where T : DebugElement
        {
            element.Shortcut = key;
            return element;
        }

        /// <summary>保存キーを明示して、そのまま行を返す。</summary>
        /// <param name="element">対象の行。</param>
        /// <param name="saveKey">保存・復元に使う絶対キー。</param>
        public static T WithSaveKey<T>(this T element, string saveKey) where T : DebugElement
        {
            element.SaveKey = saveKey;
            return element;
        }
    }
}
