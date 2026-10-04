#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Uno.Application.AI;
using Uno.Application.Events;
using Uno.Application.States;
using Uno.Application.Turns;
using Uno.Core.Deck;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Rules;

namespace Uno.Tests.Editor
{
    [TestFixture]
    public class ApplicationTests
    {
        [Test]
        public void EventBus_SubscribeAndPublish_DeliversEventPayload()
        {
            EventBus eventBus = new EventBus();
            bool eventReceived = false;
            int receivedPlayerId = -1;
            Card? receivedCard = null;

            eventBus.Subscribe<CardDrawnEvent>(evt =>
            {
                eventReceived = true;
                receivedPlayerId = evt.PlayerId;
                receivedCard = evt.Card;
            });

            Card drawn = Card.CreateNumber("n1", CardColor.Red, 4);
            eventBus.Publish(new CardDrawnEvent(2, drawn));

            Assert.That(eventReceived, Is.True);
            Assert.That(receivedPlayerId, Is.EqualTo(2));
            Assert.That(receivedCard?.Id, Is.EqualTo("n1"));
        }

        [Test]
        public void EventBus_Unsubscribe_StopsEventDelivery()
        {
            EventBus eventBus = new EventBus();
            int callCount = 0;
            System.Action<UnoDeclaredEvent> handler = evt => callCount++;

            eventBus.Subscribe(handler);
            eventBus.Publish(new UnoDeclaredEvent(1));
            Assert.That(callCount, Is.EqualTo(1));

            eventBus.Unsubscribe(handler);
            eventBus.Publish(new UnoDeclaredEvent(1));
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void TurnManager_ClockwiseAdvancement_StepsSequentially()
        {
            TurnManager turnManager = new TurnManager();
            turnManager.Initialize(4, 0);

            Assert.That(turnManager.CurrentPlayerIndex, Is.EqualTo(0));

            turnManager.AdvanceTurn(1);
            Assert.That(turnManager.CurrentPlayerIndex, Is.EqualTo(1));

            turnManager.AdvanceTurn(1);
            Assert.That(turnManager.CurrentPlayerIndex, Is.EqualTo(2));

            turnManager.AdvanceTurn(2);
            Assert.That(turnManager.CurrentPlayerIndex, Is.EqualTo(0));
        }

        [Test]
        public void TurnManager_ReverseDirection_InvertsAdvancement()
        {
            TurnManager turnManager = new TurnManager();
            turnManager.Initialize(4, 0);

            turnManager.ReverseDirection();
            Assert.That(turnManager.Direction, Is.EqualTo(TurnDirection.CounterClockwise));

            turnManager.AdvanceTurn(1);
            Assert.That(turnManager.CurrentPlayerIndex, Is.EqualTo(3));
        }

        [Test]
        public void TurnManager_TwoPlayerReverse_ActsAsSkip()
        {
            TurnManager turnManager = new TurnManager();
            turnManager.Initialize(2, 0);

            turnManager.ReverseDirection();
            Assert.That(turnManager.CurrentPlayerIndex, Is.EqualTo(1));
            Assert.That(turnManager.Direction, Is.EqualTo(TurnDirection.Clockwise));
        }

        [Test]
        public void AiStrategy_SelectWildColor_ChoosesDominantSuit()
        {
            UnoRuleValidator validator = new UnoRuleValidator();
            UnoHeuristicAiStrategy strategy = new UnoHeuristicAiStrategy(validator);

            List<Card> hand = new List<Card>
            {
                Card.CreateNumber("1", CardColor.Blue, 4),
                Card.CreateNumber("2", CardColor.Blue, 9),
                Card.CreateNumber("3", CardColor.Blue, 2),
                Card.CreateNumber("4", CardColor.Red, 1),
                Card.CreateWild("5", CardType.Wild)
            };

            CardColor chosenColor = strategy.SelectWildColor(hand);
            Assert.That(chosenColor, Is.EqualTo(CardColor.Blue));
        }

        [Test]
        public void ActionResolution_DrawTwo_ForcesNextPlayerDrawAndSkip()
        {
            EventBus bus = new EventBus();
            Deck deck = new Deck();
            deck.Shuffle(42);
            TurnManager turns = new TurnManager();
            turns.Initialize(4, 0);

            List<List<Card>> hands = new List<List<Card>>
            {
                new List<Card> { Card.CreateNumber("keep", CardColor.Blue, 1) }, // still holding cards → not a win
                new List<Card>(),
                new List<Card>(),
                new List<Card>()
            };

            bool[] unoFlags = new bool[4];
            RoundEndState roundEnd = new RoundEndState(turns, bus, hands);
            StateMachine fsm = new StateMachine();
            ActionResolutionState resolution = new ActionResolutionState(
                fsm, turns, deck, bus, hands, unoFlags, roundEnd);

            int drawnEvents = 0;
            bus.Subscribe<CardDrawnEvent>(_ => drawnEvents++);

            resolution.PlayedCard = Card.CreateAction("r_dt", CardColor.Red, CardType.DrawTwo);
            resolution.ActingPlayerId = 0;
            resolution.ConfigureCallback(() => { });
            resolution.Enter();

            Assert.That(hands[1].Count, Is.EqualTo(2));
            Assert.That(drawnEvents, Is.EqualTo(2));
            Assert.That(turns.CurrentPlayerIndex, Is.EqualTo(2));
        }

        [Test]
        public void PlayerTurn_DrawPlayableCard_KeepsTurnOpenForPlayOrPass()
        {
            EventBus bus = new EventBus();
            Deck deck = new Deck();
            TurnManager turns = new TurnManager();
            turns.Initialize(4, 0);
            UnoRuleValidator rules = new UnoRuleValidator();

            // Force a known top discard and a known next draw by building a tiny controlled scenario:
            // Discard a red 5, then put a red 7 on top of draw stack via Initialize+manual approach.
            deck.Initialize();
            Card redFive = Card.CreateNumber("force_red5", CardColor.Red, 5);
            deck.Discard(redFive);

            List<Card> hand = new List<Card>();
            PlayerTurnState turn = new PlayerTurnState(turns, deck, rules, bus, hand, 0);
            turn.ActiveColor = CardColor.Red;
            turn.Enter();

            // Keep drawing until we either get a playable card or exhaust a safety budget.
            bool sawPlayOrPassWindow = false;
            for (int i = 0; i < 40; i++)
            {
                (Card drawn, bool ended) = turn.SubmitDrawCard();
                if (string.IsNullOrEmpty(drawn.Id))
                {
                    break;
                }

                if (!ended && turn.AwaitingPlayOrPassAfterDraw)
                {
                    sawPlayOrPassWindow = true;
                    Assert.That(turn.PendingDrawnCard?.Id, Is.EqualTo(drawn.Id));
                    break;
                }

                // Turn ended because drawn card was not playable — start a fresh turn for next attempt.
                turn.Enter();
            }

            Assert.That(sawPlayOrPassWindow, Is.True, "Expected at least one playable drawn card in 40 draws.");
        }
    }
}
