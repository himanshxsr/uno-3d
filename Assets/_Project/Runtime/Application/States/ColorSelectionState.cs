#nullable enable

using System;
using Uno.Application.Events;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Application.States
{
    /// <summary>
    /// FSM State: Waits for a human wild-color selection, or immediately resolves a pre-chosen bot color.
    /// </summary>
    public class ColorSelectionState : IGameState
    {
        private readonly IEventBus _eventBus;
        private readonly Action<Card, CardColor> _onColorResolved;

        private Card _pendingCard;
        private int _playerId;
        private CardColor? _preselectedColor;
        private bool _isWaiting;

        public ColorSelectionState(IEventBus eventBus, Action<Card, CardColor> onColorResolved)
        {
            _eventBus = eventBus;
            _onColorResolved = onColorResolved;
        }

        /// <summary>
        /// Configures the pending wild card before the state is entered.
        /// </summary>
        /// <param name="playerId">Seat that played the wild.</param>
        /// <param name="card">Wild card being resolved.</param>
        /// <param name="preselectedColor">Optional bot-chosen color; null waits for human input.</param>
        public void Prepare(int playerId, Card card, CardColor? preselectedColor)
        {
            _playerId = playerId;
            _pendingCard = card;
            _preselectedColor = preselectedColor;
        }

        public void Enter()
        {
            if (_preselectedColor.HasValue)
            {
                Resolve(_preselectedColor.Value);
                return;
            }

            _isWaiting = true;
            _eventBus.Publish(new ColorSelectionRequestedEvent(_playerId));
            _eventBus.Publish(new GameLogEvent("Choose a color for the Wild card."));
        }

        /// <summary>
        /// Submits the human player's chosen suit color.
        /// </summary>
        public void SubmitColor(CardColor color)
        {
            if (!_isWaiting)
            {
                return;
            }

            if (color == CardColor.Wild)
            {
                return;
            }

            Resolve(color);
        }

        private void Resolve(CardColor color)
        {
            _isWaiting = false;
            _eventBus.Publish(new ColorChangedEvent(color));
            _onColorResolved.Invoke(_pendingCard, color);
        }

        public void Update(float deltaTime)
        {
        }

        public void Exit()
        {
            _isWaiting = false;
        }
    }
}
