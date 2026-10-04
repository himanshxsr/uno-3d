#nullable enable

using System;
using System.Collections.Generic;
using Uno.Application.AI;
using Uno.Application.Events;
using Uno.Application.Turns;
using Uno.Core.Deck;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Application.States
{
    /// <summary>
    /// FSM State: AI bot deliberation with humanized delay, heuristic play, and UNO auto-call.
    /// </summary>
    public class AITurnState : IGameState
    {
        private readonly ITurnManager _turnManager;
        private readonly IDeck _deck;
        private readonly IAIDecisionStrategy _aiStrategy;
        private readonly IEventBus _eventBus;
        private readonly List<List<Card>> _playerHands;
        private readonly Action<Card, CardColor?> _onTurnActionCompleted;
        private readonly Random _random = new Random();

        private float _remainingDelaySec;
        private bool _decisionPending;
        private int _botIndex;

        public CardColor ActiveColor { get; set; } = CardColor.Red;
        public float MinDeliberationDelaySec { get; set; } = 0.75f;
        public float MaxDeliberationDelaySec { get; set; } = 1.5f;

        public AITurnState(
            ITurnManager turnManager,
            IDeck deck,
            IAIDecisionStrategy aiStrategy,
            IEventBus eventBus,
            List<List<Card>> playerHands,
            Action<Card, CardColor?> onTurnActionCompleted)
        {
            _turnManager = turnManager;
            _deck = deck;
            _aiStrategy = aiStrategy;
            _eventBus = eventBus;
            _playerHands = playerHands;
            _onTurnActionCompleted = onTurnActionCompleted;
        }

        public void Enter()
        {
            _botIndex = _turnManager.CurrentPlayerIndex;
            _eventBus.Publish(new TurnChangedEvent(_botIndex, _turnManager.Direction));

            float span = Math.Max(0f, MaxDeliberationDelaySec - MinDeliberationDelaySec);
            _remainingDelaySec = MinDeliberationDelaySec + (float)_random.NextDouble() * span;
            _decisionPending = true;
        }

        public void Update(float deltaTime)
        {
            if (!_decisionPending)
            {
                return;
            }

            _remainingDelaySec -= deltaTime;
            if (_remainingDelaySec > 0f)
            {
                return;
            }

            _decisionPending = false;
            ExecuteBotDecision();
        }

        private void ExecuteBotDecision()
        {
            List<Card> botHand = _playerHands[_botIndex];
            Card topCard = _deck.TopDiscardCard ?? default;
            int nextPlayerIndex = _turnManager.GetNextPlayerIndex(1);
            int nextPlayerCardCount = _playerHands[nextPlayerIndex].Count;

            Card? chosenCard = _aiStrategy.SelectCardToPlay(botHand, topCard, ActiveColor, nextPlayerCardCount);
            if (chosenCard.HasValue)
            {
                PlayCard(botHand, chosenCard.Value);
                return;
            }

            Card drawnCard = _deck.Draw();
            botHand.Add(drawnCard);
            _eventBus.Publish(new CardDrawnEvent(_botIndex, drawnCard));

            Card currentTop = _deck.TopDiscardCard ?? default;
            Card? drawnPlay = _aiStrategy.SelectCardToPlay(new[] { drawnCard }, currentTop, ActiveColor, nextPlayerCardCount);
            if (drawnPlay.HasValue)
            {
                PlayCard(botHand, drawnCard);
                return;
            }

            _onTurnActionCompleted.Invoke(default, null);
        }

        private void PlayCard(List<Card> botHand, Card playCard)
        {
            botHand.Remove(playCard);
            _deck.Discard(playCard);
            _eventBus.Publish(new CardPlayedEvent(_botIndex, playCard));

            CardColor? chosenWildColor = null;
            if (playCard.Type == CardType.Wild || playCard.Type == CardType.WildDrawFour)
            {
                chosenWildColor = _aiStrategy.SelectWildColor(botHand);
                ActiveColor = chosenWildColor.Value;
                _eventBus.Publish(new ColorChangedEvent(ActiveColor));
            }

            if (botHand.Count == 1)
            {
                _eventBus.Publish(new UnoDeclaredEvent(_botIndex));
            }

            _onTurnActionCompleted.Invoke(playCard, chosenWildColor);
        }

        public void Exit()
        {
            _decisionPending = false;
        }
    }
}
