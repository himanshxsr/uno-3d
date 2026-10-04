#nullable enable

using System;
using System.Collections.Generic;
using Uno.Application.Events;
using Uno.Application.Turns;
using Uno.Core.Deck;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Application.States
{
    /// <summary>
    /// FSM State: Deals 7 cards to each seat, flips the opening discard, resolves opening action effects.
    /// </summary>
    public class DealState : IGameState
    {
        private const int CardsPerPlayer = 7;

        private readonly IDeck _deck;
        private readonly ITurnManager _turnManager;
        private readonly IEventBus _eventBus;
        private readonly List<List<Card>> _playerHands;
        private Action<CardColor>? _setActiveColorCallback;
        private Action? _onDealCompletedCallback;

        public DealState(
            IDeck deck,
            ITurnManager turnManager,
            IEventBus eventBus,
            List<List<Card>> playerHands)
        {
            _deck = deck;
            _turnManager = turnManager;
            _eventBus = eventBus;
            _playerHands = playerHands;
        }

        public void ConfigureCallbacks(Action<CardColor> setActiveColorCallback, Action onDealCompletedCallback)
        {
            _setActiveColorCallback = setActiveColorCallback;
            _onDealCompletedCallback = onDealCompletedCallback;
        }

        public void Enter()
        {
            for (int i = 0; i < _playerHands.Count; i++)
            {
                _playerHands[i].Clear();
            }

            _eventBus.Publish(new GameLogEvent("Dealing 7 cards to each player..."));

            for (int cardRound = 0; cardRound < CardsPerPlayer; cardRound++)
            {
                for (int player = 0; player < _playerHands.Count; player++)
                {
                    Card drawn = _deck.Draw();
                    _playerHands[player].Add(drawn);
                    _eventBus.Publish(new CardDrawnEvent(player, drawn));
                }
            }

            Card initialCard = DrawValidOpeningCard();
            _deck.Discard(initialCard);
            _eventBus.Publish(new CardPlayedEvent(-1, initialCard));

            CardColor activeColor = initialCard.Color != CardColor.Wild ? initialCard.Color : CardColor.Red;
            _setActiveColorCallback?.Invoke(activeColor);
            _eventBus.Publish(new ColorChangedEvent(activeColor));

            ResolveInitialCardEffect(initialCard);
        }

        private Card DrawValidOpeningCard()
        {
            Card initialCard = _deck.Draw();
            int safety = 0;
            while (initialCard.Type == CardType.WildDrawFour && safety < 20)
            {
                _deck.Discard(initialCard);
                _deck.RecycleDiscardIntoDrawPile();
                initialCard = _deck.Draw();
                safety++;
            }

            return initialCard;
        }

        private void ResolveInitialCardEffect(Card initialCard)
        {
            switch (initialCard.Type)
            {
                case CardType.Skip:
                    _turnManager.SkipNextPlayer(1);
                    break;

                case CardType.Reverse:
                    _turnManager.ReverseDirection();
                    break;

                case CardType.DrawTwo:
                {
                    int firstPlayer = _turnManager.CurrentPlayerIndex;
                    for (int i = 0; i < 2; i++)
                    {
                        Card drawn = _deck.Draw();
                        _playerHands[firstPlayer].Add(drawn);
                        _eventBus.Publish(new CardDrawnEvent(firstPlayer, drawn));
                    }

                    _turnManager.AdvanceTurn(1);
                    break;
                }

                case CardType.Wild:
                    _eventBus.Publish(new GameLogEvent("Opening Wild — active color set to Red."));
                    break;
            }

            _eventBus.Publish(new TurnChangedEvent(_turnManager.CurrentPlayerIndex, _turnManager.Direction));
            _onDealCompletedCallback?.Invoke();
        }

        public void Update(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
