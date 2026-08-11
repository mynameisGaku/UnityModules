using UnityEngine;

namespace DebugMenu
{
    /// <summary>メニューが解釈できる操作の種類。デバイスには依存しない。</summary>
    public enum DebugMenuCommand
    {
        /// <summary>何もしない。</summary>
        None,

        /// <summary>カーソルを 1 つ上へ。</summary>
        Up,

        /// <summary>カーソルを 1 つ下へ。</summary>
        Down,

        /// <summary>値を減らす、または左へ。</summary>
        Left,

        /// <summary>値を増やす、または右へ。</summary>
        Right,

        /// <summary>決定。</summary>
        Decide,

        /// <summary>取り消し。1 つ前のページへ戻る。</summary>
        Cancel,

        /// <summary>1 画面ぶん上へ。</summary>
        PageUp,

        /// <summary>1 画面ぶん下へ。</summary>
        PageDown,

        /// <summary>いまの行のピン留めを切り替える。</summary>
        ToggleFavorite,

        /// <summary>いまの行を既定値へ戻す。</summary>
        ResetValue,

        /// <summary>前の最上位ページへ切り替える。</summary>
        PreviousPage,

        /// <summary>次の最上位ページへ切り替える。</summary>
        NextPage,

        /// <summary>全体検索を開く。</summary>
        Search,

        /// <summary>直前の値変更を取り消す。</summary>
        Undo,

        /// <summary>取り消した値変更をやり直す。</summary>
        Redo,

        /// <summary>メニューの表示を切り替える。</summary>
        ToggleMenu,
    }

    /// <summary>
    /// 1 フレーム分の入力状態。押しっぱなしかどうかだけを持ち、
    /// 「押した瞬間」や「押しっぱなしの繰り返し」の判定は
    /// <see cref="DebugMenuInputRepeater"/> が行う。
    /// <para>
    /// デバイスから直接読まず、この形で受け取るようにしてあるのは 2 つ理由がある。
    /// 入力パッケージへの依存をこのモジュールから外せること、そして
    /// <b>入力デバイス無しでメニューの操作をテストできる</b>こと。
    /// </para>
    /// </summary>
    public struct DebugMenuInputState
    {
        /// <summary>メニューの表示切り替えが押されたか。</summary>
        public bool ToggleMenu;

        /// <summary>上方向が押されているか。</summary>
        public bool Up;

        /// <summary>下方向が押されているか。</summary>
        public bool Down;

        /// <summary>左方向が押されているか。</summary>
        public bool Left;

        /// <summary>右方向が押されているか。</summary>
        public bool Right;

        /// <summary>決定が押されているか。</summary>
        public bool Decide;

        /// <summary>取り消しが押されているか。</summary>
        public bool Cancel;

        /// <summary>1 画面ぶん上へ、が押されているか。</summary>
        public bool PageUp;

        /// <summary>1 画面ぶん下へ、が押されているか。</summary>
        public bool PageDown;

        /// <summary>前の最上位ページへの切り替えが押されているか。</summary>
        public bool PreviousPage;

        /// <summary>次の最上位ページへの切り替えが押されているか。</summary>
        public bool NextPage;

        /// <summary>ピン留めの切り替えが押されているか。</summary>
        public bool ToggleFavorite;

        /// <summary>既定値へ戻すが押されているか。</summary>
        public bool ResetValue;

        /// <summary>全体検索を開く操作が押されているか。</summary>
        public bool Search;

        /// <summary>取り消し操作が押されているか。</summary>
        public bool Undo;

        /// <summary>やり直し操作が押されているか。</summary>
        public bool Redo;

        /// <summary>指定の操作が押されているかを読む。</summary>
        /// <param name="command">読む操作。</param>
        public bool IsHeld(DebugMenuCommand command) => command switch
        {
            DebugMenuCommand.ToggleMenu => ToggleMenu,
            DebugMenuCommand.Up => Up,
            DebugMenuCommand.Down => Down,
            DebugMenuCommand.Left => Left,
            DebugMenuCommand.Right => Right,
            DebugMenuCommand.Decide => Decide,
            DebugMenuCommand.Cancel => Cancel,
            DebugMenuCommand.PageUp => PageUp,
            DebugMenuCommand.PageDown => PageDown,
            DebugMenuCommand.PreviousPage => PreviousPage,
            DebugMenuCommand.NextPage => NextPage,
            DebugMenuCommand.ToggleFavorite => ToggleFavorite,
            DebugMenuCommand.ResetValue => ResetValue,
            DebugMenuCommand.Search => Search,
            DebugMenuCommand.Undo => Undo,
            DebugMenuCommand.Redo => Redo,
            _ => false,
        };

        /// <summary>2つの入力元を論理和で合成する。</summary>
        /// <param name="first">1つ目の入力状態。</param>
        /// <param name="second">2つ目の入力状態。</param>
        public static DebugMenuInputState Combine(in DebugMenuInputState first, in DebugMenuInputState second) =>
            new DebugMenuInputState
            {
                ToggleMenu = first.ToggleMenu || second.ToggleMenu,
                Up = first.Up || second.Up,
                Down = first.Down || second.Down,
                Left = first.Left || second.Left,
                Right = first.Right || second.Right,
                Decide = first.Decide || second.Decide,
                Cancel = first.Cancel || second.Cancel,
                PageUp = first.PageUp || second.PageUp,
                PageDown = first.PageDown || second.PageDown,
                PreviousPage = first.PreviousPage || second.PreviousPage,
                NextPage = first.NextPage || second.NextPage,
                ToggleFavorite = first.ToggleFavorite || second.ToggleFavorite,
                ResetValue = first.ResetValue || second.ResetValue,
                Search = first.Search || second.Search,
                Undo = first.Undo || second.Undo,
                Redo = first.Redo || second.Redo,
            };
    }

    /// <summary>
    /// 押しっぱなしを「押した瞬間 → 少し待つ → 一定間隔で繰り返し」に変換する。
    /// <para>
    /// 値を大きく動かしたいときに連打させないための仕組み。押した瞬間だけを見る実装だと
    /// 100 まで上げるのに 100 回叩くことになる。
    /// </para>
    /// <para>
    /// 押しっぱなしが続くほど間隔を詰めていくので、長く押すほど速く進む。
    /// </para>
    /// </summary>
    public sealed class DebugMenuInputRepeater
    {
        private DebugMenuCommand _active = DebugMenuCommand.None;
        private float _heldSeconds;
        private float _nextFireAt;

        /// <summary>押してから繰り返しが始まるまでの待ち時間（秒）。</summary>
        public float InitialDelay { get; set; } = 0.35f;

        /// <summary>繰り返しの間隔（秒）。</summary>
        public float RepeatInterval { get; set; } = 0.08f;

        /// <summary>この秒数だけ押し続けると、間隔が <see cref="FastRepeatInterval"/> へ縮む。</summary>
        public float AccelerateAfter { get; set; } = 1.2f;

        /// <summary>加速したあとの繰り返し間隔（秒）。</summary>
        public float FastRepeatInterval { get; set; } = 0.02f;

        /// <summary>
        /// 1 フレーム分の入力を受け取り、実行すべき操作を返す。
        /// <para>
        /// 同時に複数押されている場合は、上から順に 1 つだけ拾う。
        /// 斜め入力でカーソルと値が同時に動くのを避けるため。
        /// </para>
        /// </summary>
        /// <param name="state">今フレームの押下状態。</param>
        /// <param name="deltaSeconds">前フレームからの経過秒。</param>
        /// <returns>実行すべき操作。無ければ <see cref="DebugMenuCommand.None"/>。</returns>
        public DebugMenuCommand Poll(in DebugMenuInputState state, float deltaSeconds)
        {
            var pressed = FirstHeld(state);

            if (pressed == DebugMenuCommand.None)
            {
                _active = DebugMenuCommand.None;
                _heldSeconds = 0f;
                return DebugMenuCommand.None;
            }

            // 別の操作へ移ったら押し始めからやり直す。
            if (pressed != _active)
            {
                _active = pressed;
                _heldSeconds = 0f;
                _nextFireAt = InitialDelay;
                return pressed;
            }

            _heldSeconds += deltaSeconds;
            if (_heldSeconds < _nextFireAt) return DebugMenuCommand.None;

            var interval = _heldSeconds >= AccelerateAfter ? FastRepeatInterval : RepeatInterval;
            _nextFireAt = _heldSeconds + interval;
            return pressed;
        }

        /// <summary>押し始めの状態へ戻す。メニューを開き直したときに呼ぶ。</summary>
        public void Reset()
        {
            _active = DebugMenuCommand.None;
            _heldSeconds = 0f;
            _nextFireAt = 0f;
        }

        /// <summary>
        /// 押されている操作を 1 つだけ拾う。決定と取り消しを先に見るのは、
        /// 方向と同時に押されたときに移動より確定を優先したいため。
        /// </summary>
        private static DebugMenuCommand FirstHeld(in DebugMenuInputState state)
        {
            if (state.ToggleMenu) return DebugMenuCommand.ToggleMenu;
            if (state.Decide) return DebugMenuCommand.Decide;
            if (state.Cancel) return DebugMenuCommand.Cancel;
            if (state.Search) return DebugMenuCommand.Search;
            if (state.Undo) return DebugMenuCommand.Undo;
            if (state.Redo) return DebugMenuCommand.Redo;
            if (state.ToggleFavorite) return DebugMenuCommand.ToggleFavorite;
            if (state.ResetValue) return DebugMenuCommand.ResetValue;
            if (state.PreviousPage) return DebugMenuCommand.PreviousPage;
            if (state.NextPage) return DebugMenuCommand.NextPage;
            if (state.PageUp) return DebugMenuCommand.PageUp;
            if (state.PageDown) return DebugMenuCommand.PageDown;
            if (state.Up) return DebugMenuCommand.Up;
            if (state.Down) return DebugMenuCommand.Down;
            if (state.Left) return DebugMenuCommand.Left;
            if (state.Right) return DebugMenuCommand.Right;

            return DebugMenuCommand.None;
        }
    }

    /// <summary>
    /// 解釈済みの操作をメニューへ適用する。
    /// <para>
    /// 入力の読み取りと、それをメニューに対して何をするかの対応付けを分けてある。
    /// テストではこちらだけを直接叩ける。
    /// </para>
    /// </summary>
    public static class DebugMenuCommandDispatcher
    {
        /// <summary>1 画面ぶんの行数。<see cref="DebugMenuCommand.PageUp"/> などで使う。</summary>
        public const int PageStep = 10;

        /// <summary>操作をメニューへ適用する。</summary>
        /// <param name="menu">対象のメニュー。</param>
        /// <param name="command">実行する操作。</param>
        public static void Dispatch(DebugMenuRoot menu, DebugMenuCommand command)
        {
            Dispatch(menu, command, null);
        }

        /// <summary>履歴操作を含む操作をメニューへ適用する。</summary>
        /// <param name="menu">対象のメニュー。</param>
        /// <param name="command">実行する操作。</param>
        /// <param name="history">Undo / Redo の対象。履歴操作以外では null でよい。</param>
        public static void Dispatch(DebugMenuRoot menu, DebugMenuCommand command, DebugMenuHistory history)
        {
            if (menu == null) return;

            switch (command)
            {
                case DebugMenuCommand.ToggleMenu:
                    menu.Toggle();
                    break;
                case DebugMenuCommand.Up:
                    menu.MoveCursor(-1);
                    break;
                case DebugMenuCommand.Down:
                    menu.MoveCursor(1);
                    break;
                case DebugMenuCommand.PageUp:
                    menu.MoveCursor(-PageStep);
                    break;
                case DebugMenuCommand.PageDown:
                    menu.MoveCursor(PageStep);
                    break;
                case DebugMenuCommand.PreviousPage:
                    menu.MoveRootPage(-1);
                    break;
                case DebugMenuCommand.NextPage:
                    menu.MoveRootPage(1);
                    break;
                case DebugMenuCommand.Left:
                    menu.Adjust(-1);
                    break;
                case DebugMenuCommand.Right:
                    menu.Adjust(1);
                    break;
                case DebugMenuCommand.Decide:
                    menu.Decide();
                    break;
                case DebugMenuCommand.Cancel:
                    menu.Cancel();
                    break;
                case DebugMenuCommand.ToggleFavorite:
                {
                    var element = menu.CurrentPage?.CurrentElement;
                    element?.SetFavorite(!element.IsFavorite);
                    break;
                }
                case DebugMenuCommand.ResetValue:
                {
                    var element = menu.CurrentPage?.CurrentElement;
                    element?.ResetToDefault();
                    break;
                }
                case DebugMenuCommand.Undo:
                    history?.Undo();
                    break;
                case DebugMenuCommand.Redo:
                    history?.Redo();
                    break;
            }
        }
    }
}
