using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>元に戻せる操作 1 つ分。</summary>
    public interface IUndoableCommand
    {
        /// <summary>操作を実行する。<see cref="UndoRedoStack"/> はやり直し時にもこれを呼ぶ。</summary>
        void Execute();

        /// <summary><see cref="Execute"/> の効果を打ち消す。</summary>
        void Undo();

        /// <summary>履歴 UI に出す表示名。</summary>
        string Label { get; }
    }

    /// <summary>
    /// 操作の履歴を持ち、元に戻す／やり直すを提供するコンテナ。
    /// <para>
    /// 自作のレベルエディタ、ゲーム内の建築モード、デバッグツールなど、
    /// <c>Undo</c> クラス（エディタ専用）が使えない実行時の編集機能で必要になる。
    /// </para>
    /// <para>
    /// 実体は 2 本のスタック。新しい操作を積むとやり直し側は捨てられる ——
    /// 分岐した履歴を保持しないのは、ほぼ全ての編集ツールと同じ挙動に合わせるため。
    /// </para>
    /// </summary>
    public sealed class UndoRedoStack
    {
        private readonly List<IUndoableCommand> _undo = new List<IUndoableCommand>();
        private readonly List<IUndoableCommand> _redo = new List<IUndoableCommand>();

        private bool _applying;

        /// <param name="capacity">保持する履歴の上限。超えた分は古いものから捨てる。</param>
        public UndoRedoStack(int capacity = 128)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>履歴が変化したときに通知する。UI のボタン活性を更新するのに使う。</summary>
        public event Action Changed;

        /// <summary>次に元に戻る操作の表示名。無ければ null。</summary>
        public string NextUndoLabel => CanUndo ? _undo[_undo.Count - 1].Label : null;

        /// <summary>次にやり直す操作の表示名。無ければ null。</summary>
        public string NextRedoLabel => CanRedo ? _redo[_redo.Count - 1].Label : null;

        /// <summary>操作を実行して履歴に積む。やり直し履歴は破棄される。</summary>
        public void Do(IUndoableCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_applying) throw new InvalidOperationException("Undo / Redo の実行中に新しい操作は積めない。");

            command.Execute();
            Push(command);
        }

        /// <summary>既に実行済みの操作を、再実行せずに履歴へ積む。</summary>
        public void Push(IUndoableCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _undo.Add(command);
            _redo.Clear();

            if (_undo.Count > Capacity) _undo.RemoveAt(0);

            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;

            var command = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);

            _applying = true;
            try
            {
                command.Undo();
            }
            finally
            {
                _applying = false;
            }

            _redo.Add(command);
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;

            var command = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);

            _applying = true;
            try
            {
                command.Execute();
            }
            finally
            {
                _applying = false;
            }

            _undo.Add(command);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (_undo.Count == 0 && _redo.Count == 0) return;

            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>元に戻す側の履歴を新しい順に。履歴 UI の表示用。</summary>
        public IEnumerable<string> UndoLabels()
        {
            for (var i = _undo.Count - 1; i >= 0; i--) yield return _undo[i].Label;
        }
    }

    /// <summary>
    /// ラムダ 2 つで作る使い捨ての操作。専用クラスを立てるほどでもない編集に。
    /// </summary>
    public sealed class DelegateCommand : IUndoableCommand
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public DelegateCommand(string label, Action execute, Action undo)
        {
            Label = label ?? "(無題)";
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        }

        public string Label { get; }

        public void Execute() => _execute();
        public void Undo() => _undo();
    }

    /// <summary>
    /// 複数の操作をまとめて 1 回の Undo で戻せるようにする複合操作。
    /// 「選択中の 20 個を一括で動かした」を 20 回戻させないために使う。
    /// </summary>
    public sealed class CompositeCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands = new List<IUndoableCommand>();

        public CompositeCommand(string label) => Label = label ?? "(無題)";

        public string Label { get; }
        public int Count => _commands.Count;

        public CompositeCommand Add(IUndoableCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _commands.Add(command);
            return this;
        }

        public void Execute()
        {
            for (var i = 0; i < _commands.Count; i++) _commands[i].Execute();
        }

        /// <summary>戻すときは逆順。依存のある操作でも整合が取れる。</summary>
        public void Undo()
        {
            for (var i = _commands.Count - 1; i >= 0; i--) _commands[i].Undo();
        }
    }
}
