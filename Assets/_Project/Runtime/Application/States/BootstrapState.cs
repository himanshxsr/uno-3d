#nullable enable

using Uno.Core.Deck;

namespace Uno.Application.States
{
    /// <summary>
    /// FSM State: Resets and shuffles the deck, then transitions into dealing.
    /// </summary>
    public class BootstrapState : IGameState
    {
        private readonly StateMachine _stateMachine;
        private readonly IDeck _deck;
        private readonly DealState _dealState;
        private readonly int? _shuffleSeed;

        public BootstrapState(
            StateMachine stateMachine,
            IDeck deck,
            DealState dealState,
            int? shuffleSeed = null)
        {
            _stateMachine = stateMachine;
            _deck = deck;
            _dealState = dealState;
            _shuffleSeed = shuffleSeed;
        }

        public void Enter()
        {
            _deck.Initialize();
            _deck.Shuffle(_shuffleSeed);
            _stateMachine.ChangeState(_dealState);
        }

        public void Update(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
