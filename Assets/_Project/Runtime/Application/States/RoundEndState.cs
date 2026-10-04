#nullable enable

using System.Collections.Generic;
using Uno.Application.Events;
using Uno.Application.Turns;
using Uno.Core.Models;
using Uno.Core.Scoring;

namespace Uno.Application.States
{
    /// <summary>
    /// FSM State: Aggregates round score, calculates total points awarded to winner,
    /// broadcasts RoundEndedEvent, and presents round completion options.
    /// </summary>
    public class RoundEndState : IGameState
    {
        private readonly ITurnManager _turnManager;
        private readonly IEventBus _eventBus;
        private readonly List<List<Card>> _playerHands;

        public int WinnerPlayerId { get; private set; } = -1;
        public int FinalScore { get; private set; } = 0;

        public RoundEndState(
            ITurnManager turnManager,
            IEventBus eventBus,
            List<List<Card>> playerHands)
        {
            _turnManager = turnManager;
            _eventBus = eventBus;
            _playerHands = playerHands;
        }

        public void Enter()
        {
            WinnerPlayerId = _turnManager.CurrentPlayerIndex;

            // Collect all opponents' remaining hands
            List<IReadOnlyList<Card>> opponentsHands = new List<IReadOnlyList<Card>>();
            for (int i = 0; i < _playerHands.Count; i++)
            {
                if (i != WinnerPlayerId)
                {
                    opponentsHands.Add(_playerHands[i]);
                }
            }

            FinalScore = ScoreCalculator.CalculateRoundScore(opponentsHands);
            _eventBus.Publish(new RoundEndedEvent(WinnerPlayerId, FinalScore));
        }

        public void Update(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
