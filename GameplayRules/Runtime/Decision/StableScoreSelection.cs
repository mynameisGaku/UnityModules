namespace GameplayDecision
{
    /// <summary>current・best challenger・最終選択と入力順の全候補明細を保持する結果です。</summary>
    public sealed class StableScoreSelection
    {
        private readonly StableScoreCandidateLine[] _lines;

        internal StableScoreSelection(
            int requestedCurrentIdentifier,
            bool currentWasAvailable,
            int currentInputIndex,
            double currentScore,
            int bestCandidateIdentifier,
            int bestCandidateInputIndex,
            double bestCandidateScore,
            bool hasChallenger,
            int challengerCandidateIdentifier,
            int challengerInputIndex,
            double challengerScore,
            double challengerAdvantage,
            double minimumAdvantage,
            int selectedCandidateIdentifier,
            int selectedInputIndex,
            double selectedScore,
            StableScoreDecisionReason reason,
            StableScoreCandidateLine[] lines)
        {
            RequestedCurrentIdentifier = requestedCurrentIdentifier;
            CurrentWasAvailable = currentWasAvailable;
            CurrentInputIndex = currentInputIndex;
            CurrentScore = currentScore;
            BestCandidateIdentifier = bestCandidateIdentifier;
            BestCandidateInputIndex = bestCandidateInputIndex;
            BestCandidateScore = bestCandidateScore;
            HasChallenger = hasChallenger;
            ChallengerCandidateIdentifier = challengerCandidateIdentifier;
            ChallengerInputIndex = challengerInputIndex;
            ChallengerScore = challengerScore;
            ChallengerAdvantage = challengerAdvantage;
            MinimumAdvantage = minimumAdvantage;
            SelectedCandidateIdentifier = selectedCandidateIdentifier;
            SelectedInputIndex = selectedInputIndex;
            SelectedScore = selectedScore;
            Reason = reason;
            _lines = (StableScoreCandidateLine[])lines.Clone();
        }

        /// <summary>利用側が現在選択として要求した識別値です。0は未選択を表します。</summary>
        public int RequestedCurrentIdentifier { get; }

        /// <summary>要求された現在候補が入力に存在したかを示します。</summary>
        public bool CurrentWasAvailable { get; }

        /// <summary>現在候補の入力indexです。存在しない場合は-1です。</summary>
        public int CurrentInputIndex { get; }

        /// <summary>現在候補のscoreです。存在しない場合は0です。</summary>
        public double CurrentScore { get; }

        /// <summary>安定tie-break後の最高score候補の識別値です。</summary>
        public int BestCandidateIdentifier { get; }

        /// <summary>最高score候補の入力indexです。</summary>
        public int BestCandidateInputIndex { get; }

        /// <summary>最高score候補のscoreです。</summary>
        public double BestCandidateScore { get; }

        /// <summary>現在候補以外の比較対象が存在するかを示します。</summary>
        public bool HasChallenger { get; }

        /// <summary>最高score challengerの識別値です。存在しない場合は0です。</summary>
        public int ChallengerCandidateIdentifier { get; }

        /// <summary>最高score challengerの入力indexです。存在しない場合は-1です。</summary>
        public int ChallengerInputIndex { get; }

        /// <summary>最高score challengerのscoreです。存在しない場合は0です。</summary>
        public double ChallengerScore { get; }

        /// <summary>challenger scoreからcurrent scoreを引いた優位差です。比較不能な場合は0です。</summary>
        public double ChallengerAdvantage { get; }

        /// <summary>切替に要求された最小score優位差です。</summary>
        public double MinimumAdvantage { get; }

        /// <summary>最終的に選択された候補の識別値です。</summary>
        public int SelectedCandidateIdentifier { get; }

        /// <summary>最終的に選択された候補の入力indexです。</summary>
        public int SelectedInputIndex { get; }

        /// <summary>最終的に選択された候補のscoreです。</summary>
        public double SelectedScore { get; }

        /// <summary>利用側が指定した現在識別値と最終識別値が異なるかを示します。</summary>
        public bool ChangedFromRequestedCurrent => RequestedCurrentIdentifier > 0 && RequestedCurrentIdentifier != SelectedCandidateIdentifier;

        /// <summary>入力に存在した現在候補からchallengerへ切り替えたかを示します。</summary>
        public bool SwitchedFromAvailableCurrent => CurrentWasAvailable && RequestedCurrentIdentifier != SelectedCandidateIdentifier;

        /// <summary>候補を選択または維持した理由です。</summary>
        public StableScoreDecisionReason Reason { get; }

        /// <summary>評価した候補数です。</summary>
        public int CandidateCount => _lines.Length;

        /// <summary>指定した入力indexの候補明細を取得します。</summary>
        public bool TryGetCandidateLine(int index, out StableScoreCandidateLine line)
        {
            if (index < 0 || index >= _lines.Length)
            {
                line = default;
                return false;
            }

            line = _lines[index];
            return true;
        }
    }
}
