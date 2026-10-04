#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Scoring;

namespace Uno.Tests.Editor
{
    [TestFixture]
    public class ScoreCalculatorTests
    {
        [Test]
        public void CalculateCardPoints_ReturnsCorrectValues()
        {
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateNumber("1", CardColor.Red, 7)), Is.EqualTo(7));
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateNumber("2", CardColor.Blue, 0)), Is.EqualTo(0));
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateAction("3", CardColor.Green, CardType.Skip)), Is.EqualTo(20));
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateAction("4", CardColor.Yellow, CardType.Reverse)), Is.EqualTo(20));
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateAction("5", CardColor.Red, CardType.DrawTwo)), Is.EqualTo(20));
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateWild("6", CardType.Wild)), Is.EqualTo(50));
            Assert.That(ScoreCalculator.CalculateCardPoints(Card.CreateWild("7", CardType.WildDrawFour)), Is.EqualTo(50));
        }

        [Test]
        public void CalculateHandPoints_SumsHandAccurately()
        {
            List<Card> hand = new List<Card>
            {
                Card.CreateNumber("1", CardColor.Red, 5),               // 5
                Card.CreateAction("2", CardColor.Blue, CardType.Skip),   // 20
                Card.CreateWild("3", CardType.Wild)                      // 50
            };

            int score = ScoreCalculator.CalculateHandPoints(hand);
            Assert.That(score, Is.EqualTo(75));
        }
    }
}
