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
    /// FSM State: Resolves Skip / Reverse / +2 / +4, win checks, and UNO failure penalties.
    /// </summary>
    public class ActionResolutionState : IGameState
    {
        private const int UnoPenaltyCards = 2;

        private readonly StateMachine _stateMachine;
        private readonly ITurnManager _turnManager;
        private readonly IDeck _deck;
        private readonly IEventBus _eventBus;
        private readonly List<List<Card>> _playerHands;
        private readonly bool[] _unoCalledFlags;
        private readonly RoundEndState _roundEndState;
        private Action? _onActionResolvedCallback;

        public Card PlayedCard { get; set; }
        public int ActingPlayerId { get; set; }

        public ActionResolutionState(
            StateMachine stateMachine,
            ITurnManager turnManager,
            IDeck deck,
            IEventBus eventBus,
            List<List<Card>> playerHands,
            bool[] unoCalledFlags,
            RoundEndState roundEndState)
        {
            _stateMachine = stateMachine;
            _turnManager = turnManager;
            _deck = deck;
            _eventBus = eventBus;
            _playerHands = playerHands;
            _unoCalledFlags = unoCalledFlags;
            _roundEndState = roundEndState;
        }

        public void ConfigureCallback(Action onActionResolvedCallback)
        {
            _onActionResolvedCallback = onActionResolvedCallback;
        }

        public void Enter()
        {
            int activePlayer = ActingPlayerId;

            if (activePlayer >= 0 && activePlayer < _playerHands.Count && _playerHands[activePlayer].Count == 0)
            {
                _stateMachine.ChangeState(_roundEndState);
                return;
            }

            ResolveCardEffect(PlayedCard);

            if (PlayedCard.Type == CardType.Number || PlayedCard.Type == CardType.Wild)
            {
                _turnManager.AdvanceTurn(1);
            }

            _eventBus.Publish(new TurnChangedEvent(_turnManager.CurrentPlayerIndex, _turnManager.Direction));
            _onActionResolvedCallback?.Invoke();
        }

        /// <summary>
        /// Applies the official UNO failure penalty to any seat that still has exactly one card
        /// without a registered call. Invoked at the start of the next player's turn window.
        /// </summary>
        public void EnforcePendingUnoPenalties()
        {
            for (int player = 0; player < _playerHands.Count; player++)
            {
                if (_playerHands[player].Count != 1)
                {
                    if (player < _unoCalledFlags.Length)
                    {
                        _unoCalledFlags[player] = false;
                    }

                    continue;
                }

                bool calledUno = player < _unoCalledFlags.Length && _unoCalledFlags[player];
                if (calledUno)
                {
                    continue;
                }

                for (int i = 0; i < UnoPenaltyCards; i++)
                {
                    Card drawn = _deck.Draw();
                    _playerHands[player].Add(drawn);
                    _eventBus.Publish(new CardDrawnEvent(player, drawn));
                }

                if (player < _unoCalledFlags.Length)
                {
                    _unoCalledFlags[player] = false;
                }

                _eventBus.Publish(new UnoPenaltyEvent(player, UnoPenaltyCards));
                _eventBus.Publish(new GameLogEvent($"Player {player} forgot UNO! Draws {UnoPenaltyCards}."));
            }
        }

        private void ResolveCardEffect(Card card)
        {
            switch (card.Type)
            {
                case CardType.Skip:
                    _turnManager.SkipNextPlayer(1);
                    break;

                case CardType.Reverse:
                    _turnManager.ReverseDirection();
                    if (_turnManager.PlayerCount > 2)
                    {
                        _turnManager.AdvanceTurn(1);
                    }
                    break;

                case CardType.DrawTwo:
                {
                    int target = _turnManager.GetNextPlayerIndex(1);
                    for (int i = 0; i < 2; i++)
                    {
                        Card drawn = _deck.Draw();
                        _playerHands[target].Add(drawn);
                        _eventBus.Publish(new CardDrawnEvent(target, drawn));
                    }

                    if (target < _unoCalledFlags.Length)
                    {
                        _unoCalledFlags[target] = false;
                    }

                    _turnManager.SkipNextPlayer(1);
                    break;
                }

                case CardType.WildDrawFour:
                {
                    int target = _turnManager.GetNextPlayerIndex(1);
                    for (int i = 0; i < 4; i++)
                    {
                        Card drawn = _deck.Draw();
                        _playerHands[target].Add(drawn);
                        _eventBus.Publish(new CardDrawnEvent(target, drawn));
                    }

                    if (target < _unoCalledFlags.Length)
                    {
                        _unoCalledFlags[target] = false;
                    }

                    _turnManager.SkipNextPlayer(1);
                    break;
                }
            }
        }

        public void Update(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
