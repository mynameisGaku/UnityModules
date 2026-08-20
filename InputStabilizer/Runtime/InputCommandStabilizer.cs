namespace InputStabilization
{
    /// <summary>同じ候補commandが指定回数連続した時だけ現在commandを更新するEngine非依存state machine。</summary>
    public sealed class InputCommandStabilizer
    {
        /// <summary>確定に要求できる最大連続sample数。</summary>
        public const int MaximumRequiredSampleCount = ushort.MaxValue;

        private short _currentCommand;
        private short _candidateCommand;
        private int _candidateSampleCount;

        /// <summary>確定に必要な連続sample数。</summary>
        public int RequiredConsecutiveSamples { get; }

        /// <summary>現在確定しているcommand。</summary>
        public short CurrentCommand => _currentCommand;

        /// <summary>確定待ちのcommand。待機中でない場合は現在値。</summary>
        public short CandidateCommand => _candidateSampleCount > 0 ? _candidateCommand : _currentCommand;

        /// <summary>候補が連続したsample数。待機中でない場合は0。</summary>
        public int CandidateSampleCount => _candidateSampleCount;

        /// <summary>現在値とは異なる候補を待機しているか。</summary>
        public bool HasPendingCandidate => _candidateSampleCount > 0;

        private InputCommandStabilizer(int requiredConsecutiveSamples, short initialCommand)
        {
            RequiredConsecutiveSamples = requiredConsecutiveSamples;
            _currentCommand = initialCommand;
            _candidateCommand = initialCommand;
        }

        /// <summary>必要連続sample数と初期commandを検証してstabilizerを作成する。</summary>
        /// <param name="requiredConsecutiveSamples">1以上65535以下の確定sample数。</param>
        /// <param name="initialCommand">初期確定command。</param>
        /// <param name="stabilizer">成功時のstate machine。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時は構成error。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(int requiredConsecutiveSamples, short initialCommand, out InputCommandStabilizer stabilizer, out InputStabilizationError error)
        {
            if (requiredConsecutiveSamples < 1 || requiredConsecutiveSamples > MaximumRequiredSampleCount)
            {
                stabilizer = null;
                error = InputStabilizationError.InvalidRequiredSampleCount;
                return false;
            }

            stabilizer = new InputCommandStabilizer(requiredConsecutiveSamples, initialCommand);
            error = InputStabilizationError.None;
            return true;
        }

        /// <summary>1つのcommand sampleを処理し、現在値と候補進捗を返す。</summary>
        /// <param name="command">callerが明示的に渡す今回のcommand。</param>
        /// <returns>処理後のimmutable status。</returns>
        public InputCommandStatus Push(short command)
        {
            if (command == _currentCommand)
            {
                ClearCandidate();
                return CreateStatus(false);
            }

            if (_candidateSampleCount == 0 || command != _candidateCommand)
            {
                _candidateCommand = command;
                _candidateSampleCount = 1;
            }
            else
            {
                _candidateSampleCount++;
            }

            if (_candidateSampleCount < RequiredConsecutiveSamples) return CreateStatus(false);

            _currentCommand = _candidateCommand;
            ClearCandidate();
            return CreateStatus(true);
        }

        /// <summary>確定commandを明示値へ戻し、待機中候補を破棄する。</summary>
        /// <param name="command">reset後の確定command。</param>
        public void Reset(short command)
        {
            _currentCommand = command;
            ClearCandidate();
        }

        /// <summary>現在状態を変更せずsnapshotとして返す。</summary>
        /// <returns>Changed=falseの現在status。</returns>
        public InputCommandStatus Snapshot() => CreateStatus(false);

        private void ClearCandidate()
        {
            _candidateCommand = _currentCommand;
            _candidateSampleCount = 0;
        }

        private InputCommandStatus CreateStatus(bool changed) => new InputCommandStatus(_currentCommand, CandidateCommand, _candidateSampleCount, RequiredConsecutiveSamples, changed);
    }
}
