#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Uno.Application.AI;
using Uno.Application.Events;
using Uno.Application.States;
using Uno.Application.Turns;
using Uno.Audio;
using Uno.Core.Deck;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Rules;
using Uno.Core.Session;
using Uno.Infrastructure.Networking;
using Uno.Infrastructure.Session;
using Uno.Presentation.CameraControl;
using Uno.Presentation.Cards;
using Uno.Presentation.Hands;
using Uno.Presentation.Input;
using Uno.Presentation.Table;
using Uno.UI.HUD;
using UnityEngine.SceneManagement;

namespace Uno.Presentation.Controllers
{
    /// <summary>
    /// Master orchestrator connecting domain rules, FSM, 3D presentation, HUD, audio, and online sync.
    /// </summary>
    public class UnoGameController : MonoBehaviour
    {
        [Header("Presentation Dependencies")]
        [SerializeField] private CardFactory? _cardFactory;
        [SerializeField] private PlayerCardRaycaster? _playerRaycaster;
        [SerializeField] private AudioManager? _audioManager;

        private GameSessionConfig _session = GameSessionConfig.CreateDefaultOfflineSeats("You");
        private int _localSeat;
        private bool _isAuthority = true;
        private bool _isClientViewer;

        private Deck _deck = null!;
        private TurnManager _turnManager = null!;
        private StateMachine _stateMachine = null!;
        private EventBus _eventBus = null!;
        private UnoRuleValidator _ruleValidator = null!;
        private UnoHeuristicAiStrategy _aiStrategy = null!;

        private readonly List<List<Card>> _playerHandsData = new List<List<Card>>(4);
        private readonly List<List<Card3DView>> _playerHandsViews = new List<List<Card3DView>>(4);
        private readonly List<Card3DView> _discardPileViews = new List<Card3DView>(108);
        private readonly bool[] _unoCalledFlags = new bool[4];

        private BootstrapState _bootstrapState = null!;
        private DealState _dealState = null!;
        private PlayerTurnState _playerTurnState = null!;
        private AITurnState _aiTurnState = null!;
        private ActionResolutionState _actionResolutionState = null!;
        private ColorSelectionState _colorSelectionState = null!;
        private RoundEndState _roundEndState = null!;

        private UnoHudView? _hudView;
        private Card? _pendingHumanWildCard;
        private int _pendingHumanWildSeat;
        private float _unoGraceRemaining;
        private bool _waitingForUnoGrace;
        private bool _waitingForRemote;
        private float _remoteTurnTimer;
        private int _remoteSeat = -1;
        private string _lastLog = string.Empty;

        public CardColor ActiveColor { get; private set; } = CardColor.Red;
        public bool IsGameActive { get; private set; }

        private void Awake()
        {
            LoadSession();
            EnsureSceneDependencies();
            InitializeCoreArchitecture();
            WireNetwork();
        }

        private void Start()
        {
            // One-frame delay so HUD Awake/BindRuntimeRefs and camera adapt complete first.
            StartCoroutine(StartGameNextFrame());
        }

        private void LoadSession()
        {
            GameSessionBootstrap bootstrap = GameSessionBootstrap.Ensure();
            _session = bootstrap.Config ?? GameSessionConfig.CreateDefaultOfflineSeats("You");
            _localSeat = bootstrap.LocalSeatIndex;
            _isClientViewer = _session.Mode == GameMode.OnlineClient;
            _isAuthority = !_isClientViewer;
            if (_session.Mode == GameMode.OfflineBots && (_session.Seats == null || _session.Seats.Length == 0))
            {
                _session = GameSessionConfig.CreateDefaultOfflineSeats("You");
            }
        }

        private void WireNetwork()
        {
            GameSessionBootstrap? bootstrap = GameSessionBootstrap.Instance;
            if (bootstrap?.Host != null)
            {
                bootstrap.Host.OnPlayerCommand -= OnHostPlayerCommand;
                bootstrap.Host.OnPlayerCommand += OnHostPlayerCommand;
            }

            if (bootstrap?.RelayHost != null)
            {
                bootstrap.RelayHost.OnPlayerCommand -= OnHostPlayerCommand;
                bootstrap.RelayHost.OnPlayerCommand += OnHostPlayerCommand;
            }

            if (bootstrap?.Client != null)
            {
                bootstrap.Client.OnSnapshot -= ApplyClientSnapshot;
                bootstrap.Client.OnSnapshot += ApplyClientSnapshot;
            }
        }

        private System.Collections.IEnumerator StartGameNextFrame()
        {
            yield return null;
            try
            {
                ResponsiveCameraController? camCtrl = FindAnyObjectByType<ResponsiveCameraController>();
                camCtrl?.AdaptCameraToAspect(forceImmediate: true);

                GameSessionBootstrap bootstrap = GameSessionBootstrap.Ensure();
                if (!bootstrap.HasPendingMatch && bootstrap.Config.Mode != GameMode.OnlineClient)
                {
                    // Show polished menu first (offline / online friends).
                    Uno.UI.Menus.MainMenuController.Ensure();
                    yield break;
                }

                BeginSessionFromMenu();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UNO 3D] Game Initialization Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Called after Main Menu confirms Offline or Online match start.
        /// </summary>
        public void BeginSessionFromMenu()
        {
            LoadSession();
            WireNetwork();
            GameSessionBootstrap.Ensure().ConsumePendingMatch();

            _hudView = HudBootstrap.EnsureHud();
            _hudView.BindEventBus(_eventBus);
            ApplySeatNamesToHud();
            _hudView.UpdateTurnUI(_isClientViewer ? "SYNCING WITH HOST..." : "DEALING CARDS...", false);
            Debug.Log($"[UNO 3D] Session begin mode={_session.Mode} localSeat={_localSeat} authority={_isAuthority}");

            if (_isAuthority)
            {
                // Rebuild bootstrap state with the session seed chosen in the menu/lobby.
                int? seed = _session.Seed != 0 ? _session.Seed : null;
                _bootstrapState = new BootstrapState(_stateMachine, _deck, _dealState, seed);
                StartNewGame();
            }
        }

        private void ApplySeatNamesToHud()
        {
            _hudView?.UpdateOpponentNames(
                _session.GetSeatName(1),
                _session.GetSeatName(2),
                _session.GetSeatName(3));
        }

        private void EnsureSceneDependencies()
        {
            if (TableAnchorManager.Instance == null)
            {
                TableAnchorManager? existing = FindAnyObjectByType<TableAnchorManager>();
                if (existing == null)
                {
                    GameObject anchorObj = new GameObject("Table Anchor Manager");
                    existing = anchorObj.AddComponent<TableAnchorManager>();
                }
            }

            TableAnchorManager.Instance?.EnsureAnchorsInitialized();
            EnsureDrawPileCollider();

            if (_cardFactory == null)
            {
                _cardFactory = FindAnyObjectByType<CardFactory>() ?? gameObject.AddComponent<CardFactory>();
            }

            if (_playerRaycaster == null)
            {
                Camera mainCam = Camera.main;
                _playerRaycaster = FindAnyObjectByType<PlayerCardRaycaster>();
                if (_playerRaycaster == null)
                {
                    GameObject host = mainCam != null ? mainCam.gameObject : gameObject;
                    _playerRaycaster = host.AddComponent<PlayerCardRaycaster>();
                }
            }

            Camera camera = Camera.main;
            if (camera != null && camera.GetComponent<ResponsiveCameraController>() == null)
            {
                camera.gameObject.AddComponent<ResponsiveCameraController>();
            }

            if (_audioManager == null)
            {
                _audioManager = FindAnyObjectByType<AudioManager>();
                if (_audioManager == null)
                {
                    GameObject audioObj = new GameObject("Audio Manager");
                    _audioManager = audioObj.AddComponent<AudioManager>();
                }
            }

            _hudView = HudBootstrap.EnsureHud();
        }

        private void EnsureDrawPileCollider()
        {
            TableAnchorManager? anchors = TableAnchorManager.Instance;
            if (anchors == null || anchors.DrawPileAnchor == null)
            {
                return;
            }

            Transform draw = anchors.DrawPileAnchor;
            if (draw.GetComponent<Collider>() == null)
            {
                BoxCollider box = draw.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1.3f, 0.35f, 1.8f);
                box.center = new Vector3(0f, 0.15f, 0f);
            }

            EnsurePileMarker(draw, "DrawPileVisual", CardFaceBaker.GetBackTexture(), new Vector3(1.2f, 1.7f, 0.25f));
            if (anchors.DiscardPileAnchor != null)
            {
                EnsurePileMarker(anchors.DiscardPileAnchor, "DiscardPileBase", null, new Vector3(1.35f, 0.04f, 1.9f), faint: true);
            }
        }

        private static void EnsurePileMarker(
            Transform anchor,
            string childName,
            Texture2D? texture,
            Vector3 scale,
            bool faint = false)
        {
            Transform? existing = anchor.Find(childName);
            if (existing != null)
            {
                return;
            }

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = childName;
            marker.transform.SetParent(anchor, false);
            marker.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            marker.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            marker.transform.localScale = scale;

            Collider? col = marker.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            MeshRenderer? renderer = marker.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            Shader? shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Texture")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            Material mat = new Material(shader);
            if (texture != null)
            {
                CardFactory.SetMaterialTexture(mat, texture);
                CardFactory.SetMaterialColor(mat, Color.white);
            }
            else
            {
                CardFactory.SetMaterialColor(mat, faint
                    ? new Color(0.2f, 0.22f, 0.28f, 0.55f)
                    : new Color(0.72f, 0.1f, 0.16f, 1f));
            }

            renderer.sharedMaterial = mat;
        }

        private void InitializeCoreArchitecture()
        {
            _eventBus = new EventBus();
            _deck = new Deck();
            _turnManager = new TurnManager();
            _stateMachine = new StateMachine();
            _ruleValidator = new UnoRuleValidator();
            _aiStrategy = new UnoHeuristicAiStrategy(_ruleValidator);
            _turnManager.Initialize(4, 0);

            for (int i = 0; i < 4; i++)
            {
                _playerHandsData.Add(new List<Card>());
                _playerHandsViews.Add(new List<Card3DView>());
            }

            _roundEndState = new RoundEndState(_turnManager, _eventBus, _playerHandsData);

            _actionResolutionState = new ActionResolutionState(
                _stateMachine, _turnManager, _deck, _eventBus, _playerHandsData, _unoCalledFlags, _roundEndState);

            int humanSeat = _isAuthority ? 0 : _localSeat;
            _playerTurnState = new PlayerTurnState(
                _turnManager, _deck, _ruleValidator, _eventBus, _playerHandsData[humanSeat], humanSeat);
            _playerTurnState.OnTurnEndedAutomatically = () =>
            {
                _hudView?.SetDrawButtonLabel("DRAW");
                _turnManager.AdvanceTurn(1);
                TransitionToCurrentPlayerTurn();
            };

            _aiTurnState = new AITurnState(
                _turnManager, _deck, _aiStrategy, _eventBus, _playerHandsData, OnAITurnActionCompleted);

            _dealState = new DealState(_deck, _turnManager, _eventBus, _playerHandsData);

            int? seed = _session.Seed != 0 ? _session.Seed : null;
            _bootstrapState = new BootstrapState(_stateMachine, _deck, _dealState, seed);

            _colorSelectionState = new ColorSelectionState(_eventBus, OnWildColorResolved);

            _dealState.ConfigureCallbacks(
                color => ActiveColor = color,
                TransitionToCurrentPlayerTurn);

            _actionResolutionState.ConfigureCallback(TransitionToCurrentPlayerTurn);

            _eventBus.Subscribe<TurnChangedEvent>(OnTurnChanged);
            _eventBus.Subscribe<ColorChangedEvent>(OnColorChanged);
            _eventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
            _eventBus.Subscribe<CardDrawnEvent>(OnCardDrawn);
            _eventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
            _eventBus.Subscribe<UnoDeclaredEvent>(OnUnoDeclared);

            if (_playerRaycaster != null)
            {
                _playerRaycaster.Configure(_ruleValidator, ActiveColor, default);
                _playerRaycaster.OnLegalCardClicked += OnHumanPlayerCardClicked;
                _playerRaycaster.OnDrawPileClicked += OnHumanPlayerDrawClicked;
                _playerRaycaster.OnKeyboardCardIndexRequested += OnKeyboardCardIndexRequested;
                _playerRaycaster.OnUnoRequested += OnHumanPlayerUnoDeclared;
            }

            UnoHudView.OnDrawCardRequested += OnHumanPlayerDrawClicked;
            UnoHudView.OnUnoCallRequested += OnHumanPlayerUnoDeclared;
            UnoHudView.OnNextRoundRequested += OnNextRoundRequested;
            UnoHudView.OnWildColorSelected += OnHudWildColorSelected;
            UnoHudView.OnMainMenuRequested += ReturnToMainMenu;

            _hudView?.BindEventBus(_eventBus);
            _audioManager?.BindEventBus(_eventBus);
        }

        public void StartNewGame()
        {
            if (!_isAuthority)
            {
                return;
            }

            IsGameActive = true;
            _pendingHumanWildCard = null;
            _waitingForRemote = false;
            _cardFactory?.ReleaseAllActiveCards();

            for (int i = 0; i < 4; i++)
            {
                _playerHandsData[i].Clear();
                _playerHandsViews[i].Clear();
                _unoCalledFlags[i] = false;
            }

            _discardPileViews.Clear();
            _hudView?.HideRoundEndModal();
            _hudView?.HideColorPicker();
            ApplySeatNamesToHud();
            _audioManager?.PlayShuffle();
            _lastLog = "Shuffling & dealing...";
            _eventBus.Publish(new GameLogEvent(_lastLog));
            _stateMachine.ChangeState(_bootstrapState);
            RefreshHudStats();
            Debug.Log($"[UNO 3D] New game started ({_session.Mode}). Hands={_playerHandsData[0].Count}/{_playerHandsData[1].Count}/{_playerHandsData[2].Count}/{_playerHandsData[3].Count}");
        }

        private void OnNextRoundRequested()
        {
            if (_isAuthority)
            {
                StartNewGame();
            }
        }

        private void RefreshHudStats()
        {
            int left = MapOpponentCount(1);
            int top = MapOpponentCount(2);
            int right = MapOpponentCount(3);
            _hudView?.UpdateOpponentCounts(left, top, right);
            _hudView?.UpdatePileCount(_deck.DrawPileCount, _deck.DiscardPileCount);

            Card? topCard = _deck.TopDiscardCard;
            if (topCard.HasValue)
            {
                Texture2D face = CardFaceBaker.GetFaceTexture(topCard.Value);
                _hudView?.UpdateDiscardPreview(topCard, face, CardFaceBaker.GetDisplayName(topCard.Value));
            }
            else
            {
                _hudView?.UpdateDiscardPreview(null, null, "WAITING...");
            }

            if (_isAuthority && _session.Mode == GameMode.OnlineHost)
            {
                BroadcastSnapshots();
            }
        }

        private int MapOpponentCount(int seat) =>
            seat < _playerHandsData.Count ? _playerHandsData[seat].Count : 0;

        private void TransitionToCurrentPlayerTurn()
        {
            if (!IsGameActive)
            {
                return;
            }

            if (HasUncalledUno())
            {
                _waitingForUnoGrace = true;
                _unoGraceRemaining = 1.75f;
                _hudView?.ShowUnoButton(_playerHandsData[0].Count == 1);
                _eventBus.Publish(new GameLogEvent("UNO check — call UNO now or take a penalty!"));
                _playerRaycaster?.SetInputEnabled(false);
                return;
            }

            BeginCurrentPlayerTurn();
        }

        private bool HasUncalledUno()
        {
            for (int i = 0; i < _playerHandsData.Count; i++)
            {
                if (_playerHandsData[i].Count == 1 && !_unoCalledFlags[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void BeginCurrentPlayerTurn()
        {
            int currentSeat = _turnManager.CurrentPlayerIndex;
            Card topCard = _deck.TopDiscardCard ?? default;
            PlayerKind kind = _session.GetSeatKind(currentSeat);
            string name = _session.GetSeatName(currentSeat);

            if (_playerRaycaster != null)
            {
                _playerRaycaster.UpdateGameState(ActiveColor, topCard);
                _playerRaycaster.SetInputEnabled(kind == PlayerKind.LocalHuman && currentSeat == _localSeat);
            }

            _hudView?.ShowUnoButton(_playerHandsData[_localSeat].Count <= 2);
            _waitingForRemote = false;

            if (kind == PlayerKind.LocalHuman)
            {
                _playerTurnState.ActiveColor = ActiveColor;
                _stateMachine.ChangeState(_playerTurnState);
                _hudView?.UpdateTurnUI("YOUR TURN — TAP A CARD OR DRAW", true);
            }
            else if (kind == PlayerKind.Bot)
            {
                _aiTurnState.ActiveColor = ActiveColor;
                _stateMachine.ChangeState(_aiTurnState);
                _hudView?.UpdateTurnUI($"{name.ToUpperInvariant()} THINKING...", false);
            }
            else
            {
                _waitingForRemote = true;
                _remoteSeat = currentSeat;
                _remoteTurnTimer = 20f;
                _playerRaycaster?.SetInputEnabled(false);
                _hudView?.UpdateTurnUI($"WAITING FOR {name.ToUpperInvariant()}...", false);
                RefreshHudStats();
            }
        }

        private void OnAITurnActionCompleted(Card playedCard, CardColor? chosenWildColor)
        {
            // Default(Card) has a null Id — that is the only reliable "no play" signal.
            // Do not check Type == default; Number cards use Type value 0.
            if (string.IsNullOrEmpty(playedCard.Id))
            {
                _turnManager.AdvanceTurn(1);
                TransitionToCurrentPlayerTurn();
                return;
            }

            if (chosenWildColor.HasValue)
            {
                ActiveColor = chosenWildColor.Value;
            }

            _actionResolutionState.PlayedCard = playedCard;
            _actionResolutionState.ActingPlayerId = _turnManager.CurrentPlayerIndex;
            _stateMachine.ChangeState(_actionResolutionState);
        }

        private void OnKeyboardCardIndexRequested(int index)
        {
            if (!IsLocalPlayersTurn())
            {
                return;
            }

            List<Card3DView> humanHandViews = _playerHandsViews[_localSeat];
            if (index >= 0 && index < humanHandViews.Count)
            {
                OnHumanPlayerCardClicked(humanHandViews[index]);
            }
        }

        private bool IsLocalPlayersTurn()
        {
            if (_isClientViewer)
            {
                return true; // host validates; client may attempt
            }

            return _turnManager.CurrentPlayerIndex == _localSeat;
        }

        private void OnHumanPlayerCardClicked(Card3DView cardView)
        {
            if (_isClientViewer)
            {
                GameSessionBootstrap.Instance?.Client?.SendCommand(new PlayerCommandMsg
                {
                    Command = PlayerCommandType.PlayCard,
                    CardId = cardView.CardData.Id,
                    SeatIndex = _localSeat
                });
                return;
            }

            if (_turnManager.CurrentPlayerIndex != _localSeat)
            {
                return;
            }

            if (_stateMachine.CurrentState == _colorSelectionState)
            {
                return;
            }

            Card cardData = cardView.CardData;

            if (cardData.Type == CardType.Wild || cardData.Type == CardType.WildDrawFour)
            {
                bool accepted = _playerTurnState.SubmitCardPlay(cardData, null);
                if (!accepted)
                {
                    return;
                }

                _pendingHumanWildCard = cardData;
                _pendingHumanWildSeat = _localSeat;
                _colorSelectionState.Prepare(_localSeat, cardData, null);
                _stateMachine.ChangeState(_colorSelectionState);
                return;
            }

            bool success = _playerTurnState.SubmitCardPlay(cardData, null);
            if (!success)
            {
                return;
            }

            _actionResolutionState.PlayedCard = cardData;
            _actionResolutionState.ActingPlayerId = _localSeat;
            _stateMachine.ChangeState(_actionResolutionState);
        }

        private void OnHudWildColorSelected(CardColor color)
        {
            if (_isClientViewer)
            {
                GameSessionBootstrap.Instance?.Client?.SendCommand(new PlayerCommandMsg
                {
                    Command = PlayerCommandType.SelectColor,
                    Color = color,
                    SeatIndex = _localSeat
                });
                return;
            }

            if (_stateMachine.CurrentState != _colorSelectionState)
            {
                return;
            }

            _colorSelectionState.SubmitColor(color);
        }

        private void OnWildColorResolved(Card card, CardColor color)
        {
            ActiveColor = color;
            _pendingHumanWildCard = null;
            _actionResolutionState.PlayedCard = card;
            _actionResolutionState.ActingPlayerId = _pendingHumanWildSeat;
            _stateMachine.ChangeState(_actionResolutionState);
        }

        private void OnHumanPlayerDrawClicked()
        {
            if (_isClientViewer)
            {
                GameSessionBootstrap.Instance?.Client?.SendCommand(new PlayerCommandMsg
                {
                    Command = PlayerCommandType.DrawCard,
                    SeatIndex = _localSeat
                });
                return;
            }

            if (_turnManager.CurrentPlayerIndex != _localSeat)
            {
                return;
            }

            if (_stateMachine.CurrentState != _playerTurnState)
            {
                return;
            }

            if (_playerTurnState.AwaitingPlayOrPassAfterDraw)
            {
                if (_playerTurnState.SubmitPassAfterDraw())
                {
                    _hudView?.SetDrawButtonLabel("DRAW");
                    _turnManager.AdvanceTurn(1);
                    TransitionToCurrentPlayerTurn();
                }

                return;
            }

            (Card _, bool turnEnded) = _playerTurnState.SubmitDrawCard();
            if (_playerTurnState.AwaitingPlayOrPassAfterDraw)
            {
                _hudView?.SetDrawButtonLabel("PASS");
                _hudView?.UpdateTurnUI("PLAY DRAWN CARD OR PASS", true);
                return;
            }

            if (turnEnded)
            {
                _turnManager.AdvanceTurn(1);
                TransitionToCurrentPlayerTurn();
            }
        }

        private void OnHumanPlayerUnoDeclared()
        {
            if (_isClientViewer)
            {
                GameSessionBootstrap.Instance?.Client?.SendCommand(new PlayerCommandMsg
                {
                    Command = PlayerCommandType.CallUno,
                    SeatIndex = _localSeat
                });
                return;
            }

            // Allow calling with 2 cards (about to play down to 1) or exactly 1 card.
            if (_playerHandsData[_localSeat].Count > 2 || _playerHandsData[_localSeat].Count == 0)
            {
                _eventBus.Publish(new GameLogEvent("UNO only works with 1–2 cards."));
                return;
            }

            _unoCalledFlags[_localSeat] = true;
            _eventBus.Publish(new UnoDeclaredEvent(_localSeat));
            _eventBus.Publish(new GameLogEvent($"{_session.GetSeatName(_localSeat)} called UNO!"));
            _hudView?.ShowUnoButton(true);
        }

        private void OnUnoDeclared(UnoDeclaredEvent evt)
        {
            if (evt.PlayerId >= 0 && evt.PlayerId < _unoCalledFlags.Length)
            {
                _unoCalledFlags[evt.PlayerId] = true;
            }
        }

        private void OnTurnChanged(TurnChangedEvent evt)
        {
            ReorganizeHandViews(evt.CurrentPlayerId);
            for (int i = 0; i < 4; i++)
            {
                if (i != evt.CurrentPlayerId)
                {
                    ReorganizeHandViews(i);
                }
            }
        }

        private void OnColorChanged(ColorChangedEvent evt)
        {
            ActiveColor = evt.NewActiveColor;
            Card topCard = _deck.TopDiscardCard ?? default;
            _playerRaycaster?.UpdateGameState(ActiveColor, topCard);
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            TableAnchorManager? anchorManager = TableAnchorManager.Instance;
            Transform? discardAnchor = anchorManager != null ? anchorManager.DiscardPileAnchor : null;
            Vector3 discardPos = discardAnchor != null
                ? discardAnchor.position
                : new Vector3(1.2f, 0.05f, 0f);

            Vector3 target = discardPos + Vector3.up * (0.12f + _discardPileViews.Count * 0.012f);
            Quaternion faceCamera = Quaternion.Euler(55f, 0f, 0f);

            // Opening discard (dealer flip) — spawn a fresh face-up card.
            if (evt.PlayerId < 0)
            {
                if (_cardFactory == null)
                {
                    return;
                }

                Card3DView opening = _cardFactory.GetCardView(evt.Card, discardPos + Vector3.up * 0.4f, faceCamera);
                opening.SetFaceUp(true);
                opening.SetVisualScale(1.35f);
                if (discardAnchor != null)
                {
                    opening.transform.SetParent(discardAnchor, true);
                }

                opening.AnimateTo(target, faceCamera, 0.4f);
                _discardPileViews.Add(opening);
                _hudView?.UpdateTurnUI("DEAL COMPLETE — GET READY", false);
                RefreshHudStats();
                Debug.Log($"[UNO 3D] Opening discard: {evt.Card}. Hand views: {_playerHandsViews[0].Count}/{_playerHandsViews[1].Count}/{_playerHandsViews[2].Count}/{_playerHandsViews[3].Count}");
                return;
            }

            if (evt.PlayerId >= _playerHandsViews.Count)
            {
                return;
            }

            List<Card3DView> handViews = _playerHandsViews[evt.PlayerId];
            Card3DView? targetView = null;
            for (int i = 0; i < handViews.Count; i++)
            {
                if (handViews[i].CardData == evt.Card)
                {
                    targetView = handViews[i];
                    handViews.RemoveAt(i);
                    break;
                }
            }

            if (targetView == null)
            {
                return;
            }

            targetView.SetFaceUp(true);
            targetView.SetVisualScale(1.35f);
            if (discardAnchor != null)
            {
                targetView.transform.SetParent(discardAnchor, true);
            }

            targetView.AnimateTo(target, faceCamera, 0.35f);
            _discardPileViews.Add(targetView);
            ReorganizeHandViews(evt.PlayerId);
            RefreshHudStats();

            if (evt.PlayerId == _localSeat)
            {
                _hudView?.ShowUnoButton(_playerHandsData[_localSeat].Count <= 2);
            }
        }

        private void OnCardDrawn(CardDrawnEvent evt)
        {
            if (_cardFactory == null || evt.PlayerId < 0 || evt.PlayerId >= _playerHandsViews.Count)
            {
                return;
            }

            TableAnchorManager? anchorManager = TableAnchorManager.Instance;
            Vector3 drawPos = anchorManager != null && anchorManager.DrawPileAnchor != null
                ? anchorManager.DrawPileAnchor.position
                : new Vector3(-1.2f, 0.05f, 0f);

            try
            {
                Card3DView cardView = _cardFactory.GetCardView(evt.Card, drawPos, Quaternion.Euler(65f, 0f, 0f));
                cardView.SetFaceUp(evt.PlayerId == _localSeat);
                cardView.transform.localScale = Vector3.one;
                _playerHandsViews[evt.PlayerId].Add(cardView);
                ReorganizeHandViews(evt.PlayerId);
                RefreshHudStats();

                if (evt.PlayerId == _localSeat && _playerHandsData[_localSeat].Count > 1)
                {
                    _unoCalledFlags[_localSeat] = false;
                    _hudView?.ShowUnoButton(false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UNO 3D] Failed to spawn drawn card for seat {evt.PlayerId}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ReorganizeHandViews(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _playerHandsViews.Count)
            {
                return;
            }

            TableAnchorManager? anchorManager = TableAnchorManager.Instance;
            Transform? seatAnchor = anchorManager != null ? anchorManager.GetSeatAnchor(seatIndex) : null;
            if (seatAnchor == null)
            {
                return;
            }

            HandLayout3D? handLayout = seatAnchor.GetComponent<HandLayout3D>();
            if (handLayout == null)
            {
                return;
            }

            handLayout.ArrangeHand(_playerHandsViews[seatIndex], 0.35f);
        }

        private void OnRoundEnded(RoundEndedEvent evt)
        {
            IsGameActive = false;
            string winner = _session.GetSeatName(evt.WinnerPlayerId);
            _lastLog = $"{winner} wins with {evt.FinalScore} pts.";
            Debug.Log($"[UNO 3D] Round ended — {_lastLog}");
            RefreshHudStats();
        }

        private void Update()
        {
            if (_isClientViewer)
            {
                return;
            }

            if (!IsGameActive || _stateMachine == null)
            {
                return;
            }

            if (_waitingForRemote)
            {
                _remoteTurnTimer -= Time.deltaTime;
                _hudView?.UpdateTimer(_remoteTurnTimer);
                if (_remoteTurnTimer <= 0f)
                {
                    // Remote timeout: auto-draw and pass.
                    AutoDrawForSeat(_remoteSeat);
                    _waitingForRemote = false;
                    _turnManager.AdvanceTurn(1);
                    TransitionToCurrentPlayerTurn();
                }

                return;
            }

            if (_waitingForUnoGrace)
            {
                if (!HasUncalledUno())
                {
                    _waitingForUnoGrace = false;
                    BeginCurrentPlayerTurn();
                    return;
                }

                _unoGraceRemaining -= Time.deltaTime;
                if (_unoGraceRemaining <= 0f)
                {
                    _waitingForUnoGrace = false;
                    _actionResolutionState.EnforcePendingUnoPenalties();
                    BeginCurrentPlayerTurn();
                }

                return;
            }

            _stateMachine.Update(Time.deltaTime);

            if (_stateMachine.CurrentState == _playerTurnState)
            {
                _hudView?.UpdateTimer(_playerTurnState.RemainingTurnTimeSec);
            }
        }

        private void OnDestroy()
        {
            if (GameSessionBootstrap.Instance?.Host != null)
            {
                GameSessionBootstrap.Instance.Host.OnPlayerCommand -= OnHostPlayerCommand;
            }

            if (GameSessionBootstrap.Instance?.RelayHost != null)
            {
                GameSessionBootstrap.Instance.RelayHost.OnPlayerCommand -= OnHostPlayerCommand;
            }

            if (GameSessionBootstrap.Instance?.Client != null)
            {
                GameSessionBootstrap.Instance.Client.OnSnapshot -= ApplyClientSnapshot;
            }

            _eventBus?.ClearAllSubscriptions();
            UnoHudView.OnDrawCardRequested -= OnHumanPlayerDrawClicked;
            UnoHudView.OnUnoCallRequested -= OnHumanPlayerUnoDeclared;
            UnoHudView.OnNextRoundRequested -= OnNextRoundRequested;
            UnoHudView.OnWildColorSelected -= OnHudWildColorSelected;
            UnoHudView.OnMainMenuRequested -= ReturnToMainMenu;
        }

        private void OnHostPlayerCommand(PlayerCommandMsg cmd)
        {
            if (!_isAuthority || !IsGameActive)
            {
                return;
            }

            int seat = FindSeatByClient(cmd.ClientId);
            if (seat < 0)
            {
                seat = cmd.SeatIndex;
            }

            if (cmd.Command == PlayerCommandType.CallUno)
            {
                if (_playerHandsData[seat].Count <= 2)
                {
                    _unoCalledFlags[seat] = true;
                    _eventBus.Publish(new UnoDeclaredEvent(seat));
                    _lastLog = $"{_session.GetSeatName(seat)} called UNO!";
                    _eventBus.Publish(new GameLogEvent(_lastLog));
                    RefreshHudStats();
                }

                return;
            }

            if (_waitingForRemote && seat == _remoteSeat)
            {
                HandleRemoteTurnCommand(seat, cmd);
                return;
            }

            if (_turnManager.CurrentPlayerIndex != seat)
            {
                return;
            }

            HandleRemoteTurnCommand(seat, cmd);
        }

        private void HandleRemoteTurnCommand(int seat, PlayerCommandMsg cmd)
        {
            Card top = _deck.TopDiscardCard ?? default;
            switch (cmd.Command)
            {
                case PlayerCommandType.PlayCard:
                {
                    Card? card = FindCardInHand(seat, cmd.CardId);
                    if (!card.HasValue)
                    {
                        return;
                    }

                    if (!_ruleValidator.IsMoveLegal(card.Value, top, ActiveColor))
                    {
                        return;
                    }

                    _playerHandsData[seat].RemoveAll(c => c.Id == card.Value.Id);
                    _deck.Discard(card.Value);
                    _eventBus.Publish(new CardPlayedEvent(seat, card.Value));
                    _waitingForRemote = false;

                    if (card.Value.Type == CardType.Wild || card.Value.Type == CardType.WildDrawFour)
                    {
                        _pendingHumanWildSeat = seat;
                        _pendingHumanWildCard = card;
                        _colorSelectionState.Prepare(seat, card.Value, null);
                        _stateMachine.ChangeState(_colorSelectionState);
                        RefreshHudStats();
                        return;
                    }

                    _actionResolutionState.PlayedCard = card.Value;
                    _actionResolutionState.ActingPlayerId = seat;
                    _stateMachine.ChangeState(_actionResolutionState);
                    break;
                }
                case PlayerCommandType.DrawCard:
                {
                    Card drawn = _deck.Draw();
                    _playerHandsData[seat].Add(drawn);
                    _eventBus.Publish(new CardDrawnEvent(seat, drawn));
                    bool canPlay = _ruleValidator.IsMoveLegal(drawn, top, ActiveColor);
                    if (!canPlay)
                    {
                        _waitingForRemote = false;
                        _turnManager.AdvanceTurn(1);
                        TransitionToCurrentPlayerTurn();
                    }
                    else
                    {
                        _lastLog = $"{_session.GetSeatName(seat)} drew — may play or pass.";
                        _eventBus.Publish(new GameLogEvent(_lastLog));
                        RefreshHudStats();
                    }

                    break;
                }
                case PlayerCommandType.PassAfterDraw:
                    _waitingForRemote = false;
                    _turnManager.AdvanceTurn(1);
                    TransitionToCurrentPlayerTurn();
                    break;
                case PlayerCommandType.SelectColor:
                    if (_stateMachine.CurrentState == _colorSelectionState)
                    {
                        _colorSelectionState.SubmitColor(cmd.Color);
                    }

                    break;
            }
        }

        private Card? FindCardInHand(int seat, string cardId)
        {
            for (int i = 0; i < _playerHandsData[seat].Count; i++)
            {
                if (_playerHandsData[seat][i].Id == cardId)
                {
                    return _playerHandsData[seat][i];
                }
            }

            return null;
        }

        private int FindSeatByClient(string clientId)
        {
            for (int i = 0; i < _session.Seats.Length; i++)
            {
                if (_session.Seats[i].NetworkClientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void AutoDrawForSeat(int seat)
        {
            if (seat < 0 || seat >= _playerHandsData.Count)
            {
                return;
            }

            Card drawn = _deck.Draw();
            _playerHandsData[seat].Add(drawn);
            _eventBus.Publish(new CardDrawnEvent(seat, drawn));
            _lastLog = $"{_session.GetSeatName(seat)} timed out and drew.";
            _eventBus.Publish(new GameLogEvent(_lastLog));
        }

        private void BroadcastSnapshots()
        {
            GameSessionBootstrap? bootstrap = GameSessionBootstrap.Instance;
            if (bootstrap == null)
            {
                return;
            }

            for (int seat = 0; seat < _session.Seats.Length; seat++)
            {
                SeatConfig s = _session.Seats[seat];
                if (s.Kind != PlayerKind.RemoteHuman || string.IsNullOrEmpty(s.NetworkClientId))
                {
                    continue;
                }

                GameSnapshotMsg snap = BuildSnapshotForSeat(seat);
                bootstrap.RelayHost?.SendSnapshotTo(s.NetworkClientId, snap);
                bootstrap.Host?.SendSnapshotTo(s.NetworkClientId, snap);
            }
        }

        private GameSnapshotMsg BuildSnapshotForSeat(int seat)
        {
            var names = new string[4];
            var kinds = new string[4];
            var counts = new int[4];
            for (int i = 0; i < 4; i++)
            {
                names[i] = _session.GetSeatName(i);
                kinds[i] = _session.GetSeatKind(i).ToString();
                counts[i] = _playerHandsData[i].Count;
            }

            CardDto[] myHand = new CardDto[_playerHandsData[seat].Count];
            for (int i = 0; i < myHand.Length; i++)
            {
                Card c = _playerHandsData[seat][i];
                myHand[i] = new CardDto { Id = c.Id, Color = c.Color, Type = c.Type, Value = c.Value };
            }

            Card? top = _deck.TopDiscardCard;
            bool hasTop = top.HasValue;
            var snap = new GameSnapshotMsg
            {
                LocalSeat = seat,
                Names = names,
                Kinds = kinds,
                HandCounts = counts,
                MyHand = myHand,
                HasTopDiscard = hasTop,
                TopDiscard = hasTop
                    ? new CardDto { Id = top!.Value.Id, Color = top.Value.Color, Type = top.Value.Type, Value = top.Value.Value }
                    : new CardDto(),
                DrawCount = _deck.DrawPileCount,
                DiscardCount = _deck.DiscardPileCount,
                ActiveColor = ActiveColor,
                CurrentSeat = _turnManager.CurrentPlayerIndex,
                Direction = _turnManager.Direction.ToString(),
                TimerSeconds = _waitingForRemote ? _remoteTurnTimer : (_stateMachine.CurrentState == _playerTurnState ? _playerTurnState.RemainingTurnTimeSec : 15f),
                Log = _lastLog,
                ShowColorPicker = _stateMachine.CurrentState == _colorSelectionState && _pendingHumanWildSeat == seat,
                ShowUnoButton = _playerHandsData[seat].Count <= 2,
                RoundEnded = !IsGameActive,
                WinnerSeat = -1,
                Score = 0,
                InputEnabled = _turnManager.CurrentPlayerIndex == seat && IsGameActive,
                TurnMessage = _turnManager.CurrentPlayerIndex == seat
                    ? "YOUR TURN — TAP A CARD OR DRAW"
                    : $"{_session.GetSeatName(_turnManager.CurrentPlayerIndex).ToUpperInvariant()}'S TURN"
            };
            return snap;
        }

        private void ApplyClientSnapshot(GameSnapshotMsg snap)
        {
            IsGameActive = !snap.RoundEnded;
            _localSeat = snap.LocalSeat;
            ActiveColor = snap.ActiveColor;
            _lastLog = snap.Log;

            if (snap.Names != null && snap.Names.Length >= 4)
            {
                _hudView?.UpdateOpponentNames(snap.Names[1], snap.Names[2], snap.Names[3]);
            }

            if (snap.HandCounts != null && snap.HandCounts.Length >= 4)
            {
                _hudView?.UpdateOpponentCounts(snap.HandCounts[1], snap.HandCounts[2], snap.HandCounts[3]);
            }

            _hudView?.UpdatePileCount(snap.DrawCount, snap.DiscardCount);
            _hudView?.UpdateTimer(snap.TimerSeconds);
            _hudView?.UpdateTurnUI(snap.TurnMessage, snap.InputEnabled);
            _hudView?.ShowUnoButton(snap.ShowUnoButton);
            _playerRaycaster?.SetInputEnabled(snap.InputEnabled);
            _playerRaycaster?.UpdateGameState(snap.ActiveColor, snap.HasTopDiscard
                ? new Card(snap.TopDiscard.Id, snap.TopDiscard.Color, snap.TopDiscard.Type, snap.TopDiscard.Value)
                : default);

            if (snap.ShowColorPicker)
            {
                _hudView?.ShowColorPicker();
            }
            else
            {
                _hudView?.HideColorPicker();
            }

            if (snap.HasTopDiscard)
            {
                Card top = new Card(snap.TopDiscard.Id, snap.TopDiscard.Color, snap.TopDiscard.Type, snap.TopDiscard.Value);
                _hudView?.UpdateDiscardPreview(top, CardFaceBaker.GetFaceTexture(top), CardFaceBaker.GetDisplayName(top));
            }

            SyncClientHand(snap.MyHand);
            SyncClientDiscard(snap);

            if (snap.RoundEnded)
            {
                _hudView?.ShowRoundEndModal(snap.WinnerSeat, snap.Score);
            }
        }

        private void SyncClientHand(CardDto[] hand)
        {
            if (_cardFactory == null || hand == null)
            {
                return;
            }

            // Rebuild local hand views to match authoritative snapshot.
            for (int i = _playerHandsViews[_localSeat].Count - 1; i >= 0; i--)
            {
                _cardFactory.ReleaseCardView(_playerHandsViews[_localSeat][i]);
            }

            _playerHandsViews[_localSeat].Clear();
            _playerHandsData[_localSeat].Clear();

            TableAnchorManager? anchors = TableAnchorManager.Instance;
            Vector3 spawn = anchors != null && anchors.PlayerHandAnchor != null
                ? anchors.PlayerHandAnchor.position
                : Vector3.zero;

            for (int i = 0; i < hand.Length; i++)
            {
                CardDto dto = hand[i];
                Card card = new Card(dto.Id, dto.Color, dto.Type, dto.Value);
                _playerHandsData[_localSeat].Add(card);
                Card3DView view = _cardFactory.GetCardView(card, spawn, Quaternion.Euler(58f, 0f, 0f));
                view.SetFaceUp(true);
                _playerHandsViews[_localSeat].Add(view);
            }

            ReorganizeHandViews(_localSeat);
        }

        private void SyncClientDiscard(GameSnapshotMsg snap)
        {
            if (!snap.HasTopDiscard || _cardFactory == null)
            {
                return;
            }

            Card top = new Card(snap.TopDiscard.Id, snap.TopDiscard.Color, snap.TopDiscard.Type, snap.TopDiscard.Value);
            if (_discardPileViews.Count > 0 && _discardPileViews[_discardPileViews.Count - 1].CardData.Id == top.Id)
            {
                return;
            }

            TableAnchorManager? anchors = TableAnchorManager.Instance;
            Transform? discardAnchor = anchors != null ? anchors.DiscardPileAnchor : null;
            Vector3 pos = discardAnchor != null ? discardAnchor.position : new Vector3(1.2f, 0.05f, 0f);
            Quaternion rot = Quaternion.Euler(55f, 0f, 0f);
            Card3DView view = _cardFactory.GetCardView(top, pos + Vector3.up * 0.2f, rot);
            view.SetFaceUp(true);
            view.SetVisualScale(1.35f);
            if (discardAnchor != null)
            {
                view.transform.SetParent(discardAnchor, true);
            }

            view.AnimateTo(pos + Vector3.up * (0.12f + _discardPileViews.Count * 0.012f), rot, 0.2f);
            _discardPileViews.Add(view);
        }

        private void ReturnToMainMenu()
        {
            IsGameActive = false;
            GameSessionBootstrap.Instance?.DisposeNetwork();
            _cardFactory?.ReleaseAllActiveCards();
            if (_hudView != null)
            {
                Destroy(_hudView.gameObject);
                _hudView = null;
            }

            Uno.UI.Menus.MainMenuController.Ensure().gameObject.SetActive(true);
        }
    }
}
