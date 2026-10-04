#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Rules;

namespace Uno.Tests.Editor
{
    [TestFixture]
    public class RuleValidatorTests
    {
        private UnoRuleValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            _validator = new UnoRuleValidator();
        }

        [Test]
        public void IsMoveLegal_ColorMatch_ReturnsTrue()
        {
            Card candidate = Card.CreateNumber("c1", CardColor.Red, 5);
            Card topCard = Card.CreateNumber("t1", CardColor.Red, 2);

            bool isLegal = _validator.IsMoveLegal(candidate, topCard, CardColor.Red);
            Assert.That(isLegal, Is.True);
        }

        [Test]
        public void IsMoveLegal_NumberValueMatch_ReturnsTrue()
        {
            Card candidate = Card.CreateNumber("c1", CardColor.Blue, 7);
            Card topCard = Card.CreateNumber("t1", CardColor.Red, 7);

            bool isLegal = _validator.IsMoveLegal(candidate, topCard, CardColor.Red);
            Assert.That(isLegal, Is.True);
        }

        [Test]
        public void IsMoveLegal_ActionTypeMatch_ReturnsTrue()
        {
            Card candidate = Card.CreateAction("c1", CardColor.Green, CardType.Skip);
            Card topCard = Card.CreateAction("t1", CardColor.Yellow, CardType.Skip);

            bool isLegal = _validator.IsMoveLegal(candidate, topCard, CardColor.Yellow);
            Assert.That(isLegal, Is.True);
        }

        [Test]
        public void IsMoveLegal_WildCard_AlwaysReturnsTrue()
        {
            Card wild = Card.CreateWild("w1", CardType.Wild);
            Card wildFour = Card.CreateWild("w2", CardType.WildDrawFour);
            Card topCard = Card.CreateNumber("t1", CardColor.Red, 3);

            Assert.That(_validator.IsMoveLegal(wild, topCard, CardColor.Red), Is.True);
            Assert.That(_validator.IsMoveLegal(wildFour, topCard, CardColor.Red), Is.True);
        }

        [Test]
        public void IsMoveLegal_Mismatch_ReturnsFalse()
        {
            Card candidate = Card.CreateNumber("c1", CardColor.Blue, 3);
            Card topCard = Card.CreateNumber("t1", CardColor.Red, 9);

            bool isLegal = _validator.IsMoveLegal(candidate, topCard, CardColor.Red);
            Assert.That(isLegal, Is.False);
        }

        [Test]
        public void StrictWildDrawFour_IllegalWhenMatchingColorExists()
        {
            UnoRuleValidator strict = new UnoRuleValidator(enforceStrictWildDrawFour: true);
            Card top = Card.CreateNumber("t", CardColor.Red, 1);
            Card wildFour = Card.CreateWild("w4", CardType.WildDrawFour);
            List<Card> hand = new List<Card>
            {
                Card.CreateNumber("r2", CardColor.Red, 2),
                wildFour
            };

            Assert.That(strict.IsMoveLegal(wildFour, top, CardColor.Red, hand), Is.False);
        }

        [Test]
        public void StrictWildDrawFour_LegalWhenNoMatchingColor()
        {
            UnoRuleValidator strict = new UnoRuleValidator(enforceStrictWildDrawFour: true);
            Card top = Card.CreateNumber("t", CardColor.Red, 1);
            Card wildFour = Card.CreateWild("w4", CardType.WildDrawFour);
            List<Card> hand = new List<Card>
            {
                Card.CreateNumber("b2", CardColor.Blue, 2),
                wildFour
            };

            Assert.That(strict.IsMoveLegal(wildFour, top, CardColor.Red, hand), Is.True);
        }

        [Test]
        public void CanPlayAnyCard_ReturnsFalseForEmptyHand()
        {
            Card top = Card.CreateNumber("t", CardColor.Green, 3);
            Assert.That(_validator.CanPlayAnyCard(new List<Card>(), top, CardColor.Green), Is.False);
        }
    }
}
