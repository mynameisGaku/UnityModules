using DebugMenu;
using UnityEngine;
using UnityEngine.Scripting;

namespace DebugMenu.Samples
{
    /// <summary>値変更、監視、最上位ページ切り替え、子ページをまとめて確認するサンプル。</summary>
    public static class DebugMenuBasicsSample
    {
        private static bool _invincible;
        private static float _moveSpeed = 6f;
        private static Color _accentColor = new Color(0.20f, 0.65f, 1f, 1f);
        private static int _healCount;

        /// <summary>プレイヤー向けの値変更と子ページを登録する。</summary>
        /// <param name="menu">登録先のデバッグメニュー。</param>
        [Preserve]
        [DebugMenuRegister(Order = 0)]
        private static void RegisterPlayerPage(DebugMenuRoot menu)
        {
            var player = menu.AddPage("Player");
            player.Description = "値を変更し、Details の子ページを開けます。";

            player.Bool("Invincible", () => _invincible, value => _invincible = value)
                .WithShortcut(KeyCode.F2)
                .WithSaveKey("sample.player.invincible")
                .Describe("F2 のショートカットでも切り替えられます。");

            player.Float("Move Speed", () => _moveSpeed, value => _moveSpeed = value)
                .WithRange(0f, 20f)
                .WithStep(0.5f)
                .WithUnit("m/s")
                .WithSaveKey("sample.player.move-speed");

            var color = player.Color("Accent Color", () => _accentColor, value => _accentColor = value)
                .WithSaveKey("sample.player.accent-color");
            color.ShowAlpha = true;

            player.Watch("Mode", () => _invincible ? "Invincible" : "Normal");

            var details = new DebugPage("Player Details")
            {
                Description = "Esc で Player ページへ戻ります。",
            };
            details.Watch("Heal Count", () => _healCount, 0);
            details.Action("Heal", () => _healCount++).Describe("実行回数を Heal Count へ反映します。");
            player.AddChildPage(details, DebugAttachMode.Page, "Open Details");
        }

        /// <summary>読み取り専用の監視値と折れ線グラフを別の最上位ページへ登録する。</summary>
        /// <param name="menu">登録先のデバッグメニュー。</param>
        [Preserve]
        [DebugMenuRegister(Order = 10)]
        private static void RegisterDiagnosticsPage(DebugMenuRoot menu)
        {
            var diagnostics = menu.AddPage("Diagnostics");
            diagnostics.Description = "[ と ]、またはヘッダーの左右ボタンで Player と切り替えます。";

            diagnostics.Watch("Elapsed", () => Time.unscaledTime, 1).WithUnit("s");
            diagnostics.Watch("FPS", CurrentFps, 1).WarnOutside(30f, 240f);

            var graph = diagnostics.Graph("Frame Time", () => Time.unscaledDeltaTime * 1000f, 180)
                .WithUnit("ms")
                .Describe("表示中の unscaledDeltaTime を直近 180 件まで描きます。");
            graph.SampleInterval = 0f;
            graph.AutoScale = true;
        }

        private static float CurrentFps()
        {
            var delta = Time.unscaledDeltaTime;
            return delta > 0f ? 1f / delta : 0f;
        }
    }
}
