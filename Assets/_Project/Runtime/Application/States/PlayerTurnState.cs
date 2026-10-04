#nullable enable

using System;
using System.Collections.Generic;
using Uno.Application.Events;
using Uno.Application.Turns;
using Uno.Core.Deck;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Rules;

namespace Uno.Application.States
{
    /// <summary>
    /// FSM State: Human turn — card play, draw, optional play-after-draw, and 15s timeout.
    /// </summary>
    public class PlayerTurnState : IGameState
    {
        private readonly ITurnManager _turnManager;
        private readonly IDeck _deck;
        private readonly IRuleValidator _ruleValidator;
        private readonly IEventBus _eventBus;
        private readonly List<Card> _playerHand;
        private readonly int _humanPlayerIndex;

        private bool _isTurnActive;
        private bool _awaitingPlayOrPassAfterDraw;
        private Card _pendingDrawnCard;

        public CardColor ActiveColor { get; set; } = CardColor.Red;
        public float RemainingTurnTimeSec { get; private set; } = 15.0f;
        public float MaxTurnDurationSec { get; set; } = 15.0f;

        /// <summary>
        /// True while the human turn is still accepting input.
        /// </summary>
        public bool IsTurnActive => _isTurnActive;

        /// <summary>
        /// Raised when the turn ends due to timeout auto-draw / auto-pass.
        /// </summary>
        public Action? OnTurnEndedAutomatically { get; set; }

        /// <summary>
        /// True when the human just drew a playable card and may play it or pass.
        /// </summary>
        public bool AwaitingPlayOrPassAfterDraw => _awaitingPlayOrPassAfterDraw;

        /// <summary>
        /// The card drawn this turn that may still be played, if any.
        /// </summary>
        public Card? PendingDrawnCard => _awaitingPlayOrPassAfterDraw ? _pendingDrawnCard : null;

        public PlayerTurnState(
            ITurnManager turnManager,
            IDeck deck,
            IRuleValidator ruleValidator,
            IEventBus eventBus,
            List<Card> playerHand,
            int humanPlayerIndex = 0)
        {
            _turnManager = turnManager;
            _deck = deck;
            _ruleValidator = ruleValidator;
            _eventBus = eventBus;
            _playerHand = playerHand;
            _humanPlayerIndex = humanPlayerIndex;
        }

        public void Enter()
        {
            _isTurnActive = true;
            _awaitingPlayOrPassAfterDraw = false;
            _pendingDrawnCard = default;
            RemainingTurnTimeSec = MaxTurnDurationSec;
            _eventBus.Publish(new TurnChangedEvent(_humanPlayerIndex, _turnManager.Direction));
        }

        /// <summary>
        /// Attempts to play a card from the human hand.
        /// </summary>
        public bool SubmitCardPlay(Card card, CardColor? chosenWildColor = null)
        {
            if (!_isTurnActive)
            {
                return false;
            }

            if (_awaitingPlayOrPassAfterDraw && card != _pendingDrawnCard)
            {
                return false;
            }

            Card topCard = _deck.TopDiscardCard ?? default;
            if (!_ruleValidator.IsMoveLegal(card, topCard, ActiveColor))
            {
                return false;
            }

            if (!_playerHand.Remove(card))
            {
                return false;
            }

            _isTurnActive = false;
            _awaitingPlayOrPassAfterDraw = false;
            _deck.Discard(card);
            _eventBus.Publish(new CardPlayedEvent(_humanPlayerIndex, card));

            if (chosenWildColor.HasValue && chosenWildColor.Value != CardColor.Wild)
            {
                ActiveColor = chosenWildColor.Value;
                _eventBus.Publish(new ColorChangedEvent(ActiveColor));
            }

            return true;
        }

        /// <summary>
        /// Draws one card. If playable, keeps the turn open for play-or-pass; otherwise ends the turn.
        /// </summary>
        /// <returns>
        /// Drawn card, plus whether the caller should end the turn immediately.
        /// </returns>
        public (Card DrawnCard, bool TurnEnded) SubmitDrawCard()
        {
            if (!_isTurnActive)
            {
                return (default, true);
            }

            if (_awaitingPlayOrPassAfterDraw)
            {
                // Second draw/pass press ends the turn without playing the drawn card.
                _isTurnActive = false;
                _awaitingPlayOrPassAfterDraw = false;
                return (_pendingDrawnCard, true);
            }

            Card drawnCard = _deck.Draw();
            _playerHand.Add(drawnCard);
            _eventBus.Publish(new CardDrawnEvent(_humanPlayerIndex, drawnCard));

            Card topCard = _deck.TopDiscardCard ?? default;
            bool canPlayDrawn = _ruleValidator.IsMoveLegal(drawnCard, topCard, ActiveColor);
            if (canPlayDrawn)
            {
                _awaitingPlayOrPassAfterDraw = true;
                _pendingDrawnCard = drawnCard;
                _eventBus.Publish(new GameLogEvent("Drawn card is playable — play it or press DRAW/PASS to end turn."));
                return (drawnCard, false);
            }

            _isTurnActive = false;
            return (drawnCard, true);
        }

        /// <summary>
        /// Explicitly ends the turn after a playable draw without playing the card.
        /// </summary>
        public bool SubmitPassAfterDraw()
        {
            if (!_isTurnActive || !_awaitingPlayOrPassAfterDraw)
            {
                return false;
            }

            _isTurnActive = false;
            _awaitingPlayOrPassAfterDraw = false;
            return true;
        }

        public void Update(float deltaTime)
        {
            if (!_isTurnActive)
            {
                return;
            }

            RemainingTurnTimeSec -= deltaTime;
            if (RemainingTurnTimeSec > 0f)
            {
                return;
            }

            if (_awaitingPlayOrPassAfterDraw)
            {
                if (SubmitPassAfterDraw())
                {
                    OnTurnEndedAutomatically?.Invoke();
                }

                return;
            }

            (_, bool turnEnded) = SubmitDrawCard();
            if (!turnEnded && _awaitingPlayOrPassAfterDraw)
            {
                SubmitPassAfterDraw();
                turnEnded = true;
            }

            if (turnEnded)
            {
                OnTurnEndedAutomatically?.Invoke();
            }
        }

        public void Exit()
        {
            _isTurnActive = false;
            _awaitingPlayOrPassAfterDraw = false;
        }
    }
}
