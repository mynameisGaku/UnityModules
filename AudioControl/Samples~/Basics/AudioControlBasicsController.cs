using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioControl.Samples
{
    /// <summary>owner付きvoice、上限、steal、非スケールfadeを実Buttonで確認するサンプルです。</summary>
    [AddComponentMenu("StudioGaku/Audio Control Basics Controller")]
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(AudioControlController))]
    public sealed class AudioControlBasicsController : MonoBehaviour
    {
        /// <summary>sample構築完了を示すroot要素名です。</summary>
        public const string ReadyElementName = "audio-control-basics-ready";
        /// <summary>全表示を囲むcard要素名です。</summary>
        public const string CardElementName = "audio-control-basics-card";
        /// <summary>5操作を表示順に含む行要素名です。</summary>
        public const string ButtonRowElementName = "audio-control-basics-buttons";
        /// <summary>sample title要素名です。</summary>
        public const string TitleElementName = "audio-control-basics-title";
        /// <summary>機能説明要素名です。</summary>
        public const string DescriptionElementName = "audio-control-basics-description";
        /// <summary>voice数とtimeScaleを表示する要素名です。</summary>
        public const string StatusElementName = "audio-control-basics-status";
        /// <summary>直近操作を表示する要素名です。</summary>
        public const string StageElementName = "audio-control-basics-stage";
        /// <summary>voice poolを記号表示する要素名です。</summary>
        public const string MeterElementName = "audio-control-basics-meter";
        /// <summary>短いtoneを再生するButton名です。</summary>
        public const string PlayToneButtonElementName = "audio-control-basics-play-tone";
        /// <summary>loop toneを再生するButton名です。</summary>
        public const string PlayLoopButtonElementName = "audio-control-basics-play-loop";
        /// <summary>空きvoiceを満たすButton名です。</summary>
        public const string FillButtonElementName = "audio-control-basics-fill";
        /// <summary>最後の所有voiceをfade停止するButton名です。</summary>
        public const string StopOneButtonElementName = "audio-control-basics-stop-one";
        /// <summary>sample所有voiceを全停止するButton名です。</summary>
        public const string StopAllButtonElementName = "audio-control-basics-stop-all";

        private readonly List<AudioControlHandle> _ownedHandles = new List<AudioControlHandle>();
        private AudioControlController _controller;
        private AudioClip _toneClip;
        private AudioClip _loopClip;
        private VisualElement _sampleRoot;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _status;
        private Label _stage;
        private Label _meter;
        private Button _playTone;
        private Button _playLoop;
        private Button _fill;
        private Button _stopOne;
        private Button _stopAll;

        /// <summary>sampleが現在所有するactive voice数を取得します。</summary>
        public int OwnedVoiceCount
        {
            get
            {
                RemoveInactiveHandles();
                return _ownedHandles.Count;
            }
        }

        /// <summary>画面へ表示している直近操作を取得します。</summary>
        public string StageText => _stage?.text ?? string.Empty;

        private void OnEnable()
        {
            _controller = GetComponent<AudioControlController>();
            _toneClip = CreateTone("Audio Control Tone", 0.35f, 440f);
            _loopClip = CreateTone("Audio Control Loop", 2f, 220f);
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            SetStage("Ready / generated tones use no external asset");
            RefreshView();
        }

        private void Update()
        {
            RemoveInactiveHandles();
            RefreshView();
        }

        private void OnDisable()
        {
            DisposeOwnedHandles();
            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
            if (_toneClip != null) Destroy(_toneClip);
            if (_loopClip != null) Destroy(_loopClip);
            _toneClip = null;
            _loopClip = null;
        }

        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                Debug.LogError("[Audio Control Basics] UIDocument rootを取得できません。", this);
                enabled = false;
                return;
            }

            _sampleRoot = new VisualElement { name = ReadyElementName, pickingMode = PickingMode.Position };
            _sampleRoot.style.position = Position.Absolute;
            _sampleRoot.style.left = 0f;
            _sampleRoot.style.top = 0f;
            _sampleRoot.style.right = 0f;
            _sampleRoot.style.bottom = 0f;
            _sampleRoot.style.alignItems = Align.Center;
            _sampleRoot.style.justifyContent = Justify.Center;
            _sampleRoot.style.overflow = Overflow.Hidden;
            _sampleRoot.style.backgroundColor = new Color(0.035f, 0.025f, 0.07f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.maxWidth = 940f;
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxHeight = 700f;
            _card.style.paddingLeft = 32f;
            _card.style.paddingRight = 32f;
            _card.style.paddingTop = 22f;
            _card.style.paddingBottom = 22f;
            _card.style.borderTopLeftRadius = 22f;
            _card.style.borderTopRightRadius = 22f;
            _card.style.borderBottomLeftRadius = 22f;
            _card.style.borderBottomRightRadius = 22f;
            _card.style.backgroundColor = new Color(0.105f, 0.075f, 0.17f, 0.99f);
            _card.style.color = new Color(0.98f, 0.96f, 1f, 1f);
            _card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(_card);

            _title = new Label("Audio Control Basics") { name = TitleElementName };
            _title.style.fontSize = 32f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 6f;
            _card.Add(_title);

            _description = new Label("Generated toneをowner付きvoiceへ割り当て、上限・steal・pause中fadeを確認します。")
            {
                name = DescriptionElementName
            };
            _description.style.fontSize = 16f;
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginBottom = 12f;
            _card.Add(_description);

            _status = new Label { name = StatusElementName };
            _status.style.fontSize = 15f;
            _status.style.marginBottom = 7f;
            _card.Add(_status);

            _stage = new Label { name = StageElementName };
            _stage.style.fontSize = 16f;
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stage.style.color = new Color(0.72f, 0.52f, 1f, 1f);
            _stage.style.marginBottom = 10f;
            _card.Add(_stage);

            _meter = new Label { name = MeterElementName };
            _meter.style.height = 58f;
            _meter.style.paddingTop = 14f;
            _meter.style.paddingBottom = 14f;
            _meter.style.marginBottom = 12f;
            _meter.style.unityTextAlign = TextAnchor.MiddleCenter;
            _meter.style.unityFontStyleAndWeight = FontStyle.Bold;
            _meter.style.fontSize = 18f;
            _meter.style.borderTopLeftRadius = 12f;
            _meter.style.borderTopRightRadius = 12f;
            _meter.style.borderBottomLeftRadius = 12f;
            _meter.style.borderBottomRightRadius = 12f;
            _meter.style.backgroundColor = new Color(0.055f, 0.035f, 0.1f, 1f);
            _card.Add(_meter);

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);
            _playTone = CreateButton(PlayToneButtonElementName, "Play Tone", PlayTone);
            _playLoop = CreateButton(PlayLoopButtonElementName, "Play Loop", PlayLoop);
            _fill = CreateButton(FillButtonElementName, "Fill Voices", FillVoices);
            _stopOne = CreateButton(StopOneButtonElementName, "Fade One", StopOne);
            _stopAll = CreateButton(StopAllButtonElementName, "Stop All", StopAll);
            _buttonRow.Add(_playTone);
            _buttonRow.Add(_playLoop);
            _buttonRow.Add(_fill);
            _buttonRow.Add(_stopOne);
            _buttonRow.Add(_stopAll);

            _sampleRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            documentRoot.Add(_sampleRoot);
        }

        private static Button CreateButton(string name, string text, Action clicked)
        {
            var button = new Button(clicked) { name = name, text = text };
            button.style.flexBasis = 116f;
            button.style.flexGrow = 1f;
            button.style.flexShrink = 1f;
            button.style.minWidth = 105f;
            button.style.maxWidth = 170f;
            button.style.height = 42f;
            button.style.marginLeft = 4f;
            button.style.marginRight = 4f;
            button.style.marginTop = 4f;
            button.style.marginBottom = 4f;
            button.style.fontSize = 14f;
            return button;
        }

        private void PlayTone()
        {
            var request = new AudioPlayRequest(0.75f, 1f, false, 0.05f, 80, true);
            PlayOwned(_toneClip, request, "Play Tone / priority 80");
        }

        private void PlayLoop()
        {
            var request = new AudioPlayRequest(0.55f, 1f, true, 0.25f, 120, true);
            PlayOwned(_loopClip, request, "Play Loop / unscaled fade 0.25s");
        }

        private void FillVoices()
        {
            RemoveInactiveHandles();
            while (_ownedHandles.Count < _controller.VoiceLimit)
            {
                var request = new AudioPlayRequest(0.32f, 1f, true, 0.08f, 220, false);
                if (!_controller.TryPlay(_loopClip, request, out var handle, out _)) break;
                _ownedHandles.Add(handle);
            }

            SetStage("Fill Voices / active=" + _controller.ActiveVoiceCount);
            RefreshView();
        }

        private void StopOne()
        {
            RemoveInactiveHandles();
            if (_ownedHandles.Count == 0)
            {
                SetStage("Fade One / no owned voice");
                return;
            }

            var handle = _ownedHandles[_ownedHandles.Count - 1];
            var error = _controller.Stop(handle, 0.2f);
            SetStage("Fade One / " + error);
        }

        private void StopAll()
        {
            var count = _ownedHandles.Count;
            DisposeOwnedHandles();
            SetStage("Stop All / released=" + count);
            RefreshView();
        }

        private void PlayOwned(AudioClip clip, AudioPlayRequest request, string stage)
        {
            if (_controller.TryPlay(clip, request, out var handle, out var error))
            {
                _ownedHandles.Add(handle);
                SetStage(stage);
            }
            else
            {
                SetStage("Play failed / " + error);
            }

            RemoveInactiveHandles();
            RefreshView();
        }

        private void RefreshView()
        {
            if (_controller == null) return;
            RemoveInactiveHandles();
            if (_status != null)
            {
                _status.text = $"Voices: {_controller.ActiveVoiceCount}/{_controller.VoiceLimit}  Owned: {_ownedHandles.Count}  TimeScale: {Time.timeScale:0.##}";
            }

            if (_meter != null)
            {
                var filled = new string('●', _controller.ActiveVoiceCount);
                var empty = new string('○', Mathf.Max(0, _controller.VoiceLimit - _controller.ActiveVoiceCount));
                _meter.text = "VOICE POOL  " + filled + empty;
            }

            _stopOne?.SetEnabled(_ownedHandles.Count > 0);
            _stopAll?.SetEnabled(_ownedHandles.Count > 0);
        }

        private void SetStage(string value)
        {
            if (_stage != null) _stage.text = value;
        }

        private void RemoveInactiveHandles()
        {
            for (var index = _ownedHandles.Count - 1; index >= 0; index--)
            {
                if (_ownedHandles[index] == null || !_ownedHandles[index].IsActive) _ownedHandles.RemoveAt(index);
            }
        }

        private void DisposeOwnedHandles()
        {
            var handles = _ownedHandles.ToArray();
            _ownedHandles.Clear();
            for (var index = handles.Length - 1; index >= 0; index--) handles[index]?.Dispose();
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            var compact = evt.newRect.width < 720f || evt.newRect.height < 500f;
            _card.style.paddingLeft = compact ? 12f : 32f;
            _card.style.paddingRight = compact ? 12f : 32f;
            _card.style.paddingTop = compact ? 8f : 22f;
            _card.style.paddingBottom = compact ? 8f : 22f;
            _title.style.fontSize = compact ? 23f : 32f;
            _title.style.marginBottom = compact ? 2f : 6f;
            _description.style.fontSize = compact ? 11f : 16f;
            _description.style.marginBottom = compact ? 4f : 12f;
            _status.style.fontSize = compact ? 11f : 15f;
            _status.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 12f : 16f;
            _stage.style.marginBottom = compact ? 4f : 10f;
            _meter.style.height = compact ? 40f : 58f;
            _meter.style.paddingTop = compact ? 7f : 14f;
            _meter.style.paddingBottom = compact ? 7f : 14f;
            _meter.style.marginBottom = compact ? 4f : 12f;
            _meter.style.fontSize = compact ? 13f : 18f;
            foreach (var button in new[] { _playTone, _playLoop, _fill, _stopOne, _stopAll })
            {
                button.style.flexBasis = compact ? 142f : 116f;
                button.style.minWidth = compact ? 130f : 105f;
                button.style.maxWidth = compact ? 190f : 170f;
                button.style.height = compact ? 32f : 42f;
                button.style.fontSize = compact ? 11f : 14f;
                button.style.marginTop = compact ? 2f : 4f;
                button.style.marginBottom = compact ? 2f : 4f;
            }
        }

        private static AudioClip CreateTone(string name, float seconds, float frequencyHz)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var envelope = Mathf.Min(1f, index / (sampleRate * 0.01f));
                data[index] = Mathf.Sin(2f * Mathf.PI * frequencyHz * index / sampleRate) * 0.08f * envelope;
            }

            clip.SetData(data, 0);
            return clip;
        }
    }
}
