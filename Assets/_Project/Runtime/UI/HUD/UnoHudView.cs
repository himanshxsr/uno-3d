#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Application.Events;
using Uno.Application.Turns;

namespace Uno.UI.HUD
{
    /// <summary>
    /// Portrait gameplay HUD using Unity UI.Text (no TextMeshPro dependency for labels).
    /// </summary>
    public class UnoHudView : MonoBehaviour
    {
        [SerializeField] private Text? _turnText;
        [SerializeField] private Text? _directionText;
        [SerializeField] private Text? _timerText;
        [SerializeField] private Text? _logText;
        [SerializeField] private Image? _activeColorBadge;

        [SerializeField] private Text? _leftBotText;
        [SerializeField] private Text? _topBotText;
        [SerializeField] private Text? _rightBotText;
        [SerializeField] private Text? _pileCountText;
        [SerializeField] private Image? _discardCardImage;
        [SerializeField] private Text? _discardCardLabel;

        [SerializeField] private Button? _unoButton;
        [SerializeField] private Button? _drawButton;
        [SerializeField] private Text? _drawButtonLabel;

        [SerializeField] private GameObject? _roundEndModal;
        [SerializeField] private Text? _winnerText;
        [SerializeField] private Text? _scoreText;
        [SerializeField] private Button? _nextRoundButton;

        [SerializeField] private GameObject? _colorPickerRoot;
        [SerializeField] private Button? _colorRed;
        [SerializeField] private Button? _colorBlue;
        [SerializeField] private Button? _colorGreen;
        [SerializeField] private Button? _colorYellow;

        private IEventBus? _eventBus;
        private Sprite? _discardSprite;

        public static event Action? OnUnoCallRequested;
        public static event Action? OnDrawCardRequested;
        public static event Action? OnNextRoundRequested;
        public static event Action? OnMainMenuRequested;
        public static event Action<CardColor>? OnWildColorSelected;

        public void BindRefs(
            Text turnText,
            Text directionText,
            Text timerText,
            Text logText,
            Image colorBadge,
            Text leftBot,
            Text topBot,
            Text rightBot,
            Text pileCount,
            Button unoButton,
            Button drawButton,
            Text drawLabel,
            GameObject roundEndModal,
            Text winnerText,
            Text scoreText,
            Button nextRoundButton,
            GameObject colorPickerRoot,
            Button colorRed,
            Button colorBlue,
            Button colorGreen,
            Button colorYellow,
            Image? discardCardImage = null,
            Text? discardCardLabel = null)
        {
            _turnText = turnText;
            _directionText = directionText;
            _timerText = timerText;
            _logText = logText;
            _activeColorBadge = colorBadge;
            _leftBotText = leftBot;
            _topBotText = topBot;
            _rightBotText = rightBot;
            _pileCountText = pileCount;
            _unoButton = unoButton;
            _drawButton = drawButton;
            _drawButtonLabel = drawLabel;
            _roundEndModal = roundEndModal;
            _winnerText = winnerText;
            _scoreText = scoreText;
            _nextRoundButton = nextRoundButton;
            _colorPickerRoot = colorPickerRoot;
            _colorRed = colorRed;
            _colorBlue = colorBlue;
            _colorGreen = colorGreen;
            _colorYellow = colorYellow;
            _discardCardImage = discardCardImage;
            _discardCardLabel = discardCardLabel;

            WireButtons();
            HideRoundEndModal();
            HideColorPicker();
        }

        private void Awake()
        {
            WireButtons();
            HideRoundEndModal();
            HideColorPicker();
        }

        private void WireButtons()
        {
            if (_unoButton != null)
            {
                _unoButton.onClick.RemoveAllListeners();
                _unoButton.onClick.AddListener(() => OnUnoCallRequested?.Invoke());
            }

            if (_drawButton != null)
            {
                _drawButton.onClick.RemoveAllListeners();
                _drawButton.onClick.AddListener(() => OnDrawCardRequested?.Invoke());
            }

            if (_nextRoundButton != null)
            {
                _nextRoundButton.onClick.RemoveAllListeners();
                _nextRoundButton.onClick.AddListener(() =>
                {
                    HideRoundEndModal();
                    OnNextRoundRequested?.Invoke();
                });
            }

            WireColor(_colorRed, CardColor.Red);
            WireColor(_colorBlue, CardColor.Blue);
            WireColor(_colorGreen, CardColor.Green);
            WireColor(_colorYellow, CardColor.Yellow);
        }

        private void WireColor(Button? button, CardColor color)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                OnWildColorSelected?.Invoke(color);
                HideColorPicker();
            });
        }

        public void BindEventBus(IEventBus eventBus)
        {
            UnbindEventBus();
            _eventBus = eventBus;
            _eventBus.Subscribe<TurnChangedEvent>(OnTurnChanged);
            _eventBus.Subscribe<ColorChangedEvent>(OnColorChanged);
            _eventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
            _eventBus.Subscribe<ColorSelectionRequestedEvent>(OnColorSelectionRequested);
            _eventBus.Subscribe<GameLogEvent>(OnGameLog);
            _eventBus.Subscribe<UnoPenaltyEvent>(OnUnoPenalty);
            _eventBus.Subscribe<CardDrawnEvent>(OnCardDrawn);
            _eventBus.Subscribe<CardPlayedEvent>(_ => RefreshPileHint());
        }

        public void UnbindEventBus()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<TurnChangedEvent>(OnTurnChanged);
            _eventBus.Unsubscribe<ColorChangedEvent>(OnColorChanged);
            _eventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
            _eventBus.Unsubscribe<ColorSelectionRequestedEvent>(OnColorSelectionRequested);
            _eventBus.Unsubscribe<GameLogEvent>(OnGameLog);
            _eventBus.Unsubscribe<UnoPenaltyEvent>(OnUnoPenalty);
            _eventBus.Unsubscribe<CardDrawnEvent>(OnCardDrawn);
            _eventBus = null;
        }

        public void UpdateTurnUI(string message, bool isHumanTurn)
        {
            if (_turnText != null)
            {
                _turnText.text = message;
                _turnText.color = isHumanTurn
                    ? new Color(1f, 0.85f, 0.1f, 1f)
                    : Color.white;
            }

            if (_drawButton != null)
            {
                _drawButton.interactable = isHumanTurn;
            }
        }

        public void UpdateTimer(float remainingSeconds)
        {
            if (_timerText == null)
            {
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            _timerText.text = $"{seconds}s";
            _timerText.color = seconds <= 5
                ? new Color(1f, 0.25f, 0.25f, 1f)
                : Color.white;
        }

        public void SetDrawButtonLabel(string label)
        {
            if (_drawButtonLabel != null)
            {
                _drawButtonLabel.text = label;
            }
        }

        public void ShowUnoButton(bool visible)
        {
            if (_unoButton != null)
            {
                _unoButton.gameObject.SetActive(visible);
            }
        }

        private string _leftName = "SARAH";
        private string _topName = "ALEX";
        private string _rightName = "DAVID";

        public void UpdateOpponentNames(string left, string top, string right)
        {
            _leftName = string.IsNullOrWhiteSpace(left) ? "SARAH" : left.ToUpperInvariant();
            _topName = string.IsNullOrWhiteSpace(top) ? "ALEX" : top.ToUpperInvariant();
            _rightName = string.IsNullOrWhiteSpace(right) ? "DAVID" : right.ToUpperInvariant();
        }

        public void UpdateOpponentCounts(int leftCount, int topCount, int rightCount)
        {
            if (_leftBotText != null)
            {
                _leftBotText.text = $"{_leftName}\n{leftCount} cards";
            }

            if (_topBotText != null)
            {
                _topBotText.text = $"{_topName}\n{topCount} cards";
            }

            if (_rightBotText != null)
            {
                _rightBotText.text = $"{_rightName}\n{rightCount} cards";
            }
        }

        public void RequestMainMenu() => OnMainMenuRequested?.Invoke();

        public void UpdatePileCount(int drawCount, int discardCount)
        {
            if (_pileCountText != null)
            {
                _pileCountText.text = $"DRAW {drawCount}\nDISCARD {discardCount}";
            }
        }

        /// <summary>
        /// Updates the dump-deck preview with a readable face texture and label.
        /// </summary>
        public void UpdateDiscardPreview(Card? topCard, Texture2D? faceTexture, string displayName)
        {
            if (_discardCardLabel != null)
            {
                _discardCardLabel.text = string.IsNullOrEmpty(displayName) ? "—" : displayName;
                if (topCard.HasValue)
                {
                    _discardCardLabel.color = topCard.Value.Color == CardColor.Yellow
                        ? new Color(1f, 0.92f, 0.2f, 1f)
                        : Color.white;
                }
            }

            if (_discardCardImage == null)
            {
                return;
            }

            if (faceTexture == null)
            {
                _discardCardImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);
                _discardCardImage.sprite = null;
                return;
            }

            if (_discardSprite != null)
            {
                Destroy(_discardSprite);
                _discardSprite = null;
            }

            _discardSprite = Sprite.Create(
                faceTexture,
                new Rect(0f, 0f, faceTexture.width, faceTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _discardCardImage.sprite = _discardSprite;
            _discardCardImage.color = Color.white;
            _discardCardImage.preserveAspect = true;
        }

        public void ShowColorPicker()
        {
            if (_colorPickerRoot != null)
            {
                _colorPickerRoot.SetActive(true);
            }
        }

        public void HideColorPicker()
        {
            if (_colorPickerRoot != null)
            {
                _colorPickerRoot.SetActive(false);
            }
        }

        public void ShowRoundEndModal(int winnerSeatId, int score)
        {
            if (_roundEndModal != null)
            {
                _roundEndModal.SetActive(true);
            }

            if (_winnerText != null)
            {
                _winnerText.text = winnerSeatId == 0 ? "YOU WIN!" : $"BOT {winnerSeatId} WINS!";
            }

            if (_scoreText != null)
            {
                _scoreText.text = $"+{score} PTS";
            }
        }

        public void HideRoundEndModal()
        {
            if (_roundEndModal != null)
            {
                _roundEndModal.SetActive(false);
            }
        }

        private void OnTurnChanged(TurnChangedEvent evt)
        {
            bool human = evt.CurrentPlayerId == 0;
            UpdateTurnUI(
                human ? "YOUR TURN — TAP A CARD OR DRAW" : $"BOT {evt.CurrentPlayerId} THINKING...",
                human);
            if (_directionText != null)
            {
                _directionText.text = evt.Direction == TurnDirection.Clockwise
                    ? "DIR: CLOCKWISE"
                    : "DIR: COUNTER-CLOCKWISE";
            }

            SetDrawButtonLabel("DRAW");
        }

        private void OnColorChanged(ColorChangedEvent evt)
        {
            if (_activeColorBadge != null)
            {
                _activeColorBadge.color = SuitColor(evt.NewActiveColor);
            }

            HideColorPicker();
        }

        private void OnColorSelectionRequested(ColorSelectionRequestedEvent evt)
        {
            if (evt.PlayerId == 0)
            {
                ShowColorPicker();
                UpdateTurnUI("CHOOSE A COLOR", true);
            }
        }

        private void OnRoundEnded(RoundEndedEvent evt)
        {
            ShowRoundEndModal(evt.WinnerPlayerId, evt.FinalScore);
        }

        private void OnGameLog(GameLogEvent evt)
        {
            if (_logText != null)
            {
                _logText.text = evt.Message;
            }
        }

        private void OnUnoPenalty(UnoPenaltyEvent evt)
        {
            if (_logText != null)
            {
                string who = evt.PlayerId == 0 ? "YOU" : $"BOT {evt.PlayerId}";
                _logText.text = $"{who} forgot UNO! +{evt.CardsDrawn}";
            }
        }

        private void OnCardDrawn(CardDrawnEvent evt)
        {
            RefreshPileHint();
        }

        private void RefreshPileHint()
        {
            // Counts are pushed explicitly from the controller for accuracy.
        }

        private void OnDestroy()
        {
            UnbindEventBus();
        }

        private static Color SuitColor(CardColor color)
        {
            return color switch
            {
                CardColor.Red => new Color(0.906f, 0.114f, 0.212f, 1f),
                CardColor.Blue => new Color(0f, 0.4f, 1f, 1f),
                CardColor.Green => new Color(0f, 0.722f, 0.396f, 1f),
                CardColor.Yellow => new Color(1f, 0.784f, 0f, 1f),
                _ => new Color(0.2f, 0.2f, 0.25f, 1f)
            };
        }
    }
}
