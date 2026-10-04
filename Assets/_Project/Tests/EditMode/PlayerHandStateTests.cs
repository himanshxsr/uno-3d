#nullable enable

using NUnit.Framework;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Players;

namespace Uno.Tests.Editor
{
    [TestFixture]
    public class PlayerHandStateTests
    {
        [Test]
        public void TryCallUno_OnlyValidWithSingleCard()
        {
            PlayerHandState state = new PlayerHandState(0, "You", true);
            state.AddCard(Card.CreateNumber("a", CardColor.Red, 1));
            state.AddCard(Card.CreateNumber("b", CardColor.Blue, 2));

            Assert.That(state.TryCallUno(), Is.False);

            state.RemoveCard(Card.CreateNumber("a", CardColor.Red, 1));
            Assert.That(state.CardCount, Is.EqualTo(1));
            Assert.That(state.TryCallUno(), Is.True);
            Assert.That(state.HasCalledUno, Is.True);
            Assert.That(state.IsUnoPenaltyEligible(), Is.False);
        }

        [Test]
        public void AddingCards_ClearsUnoCall()
        {
            PlayerHandState state = new PlayerHandState(1, "Bot", false);
            state.AddCard(Card.CreateNumber("a", CardColor.Yellow, 3));
            Assert.That(state.TryCallUno(), Is.True);

            state.AddCard(Card.CreateNumber("b", CardColor.Green, 4));
            Assert.That(state.HasCalledUno, Is.False);
            Assert.That(state.IsUnoPenaltyEligible(), Is.False);
        }
    }
}
