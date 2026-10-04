#nullable enable

using System;
using Uno.Core.Enums;

namespace Uno.Core.Models
{
    /// <summary>
    /// Represents an immutable UNO card data structure.
    /// Encapsulates card identity, color suit, functional type, numeric value, and point value.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        /// <summary>
        /// Gets the unique identifier for this card instance.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the suit color of the card.
        /// </summary>
        public CardColor Color { get; }

        /// <summary>
        /// Gets the functional type of the card.
        /// </summary>
        public CardType Type { get; }

        /// <summary>
        /// Gets the numeric face value (0-9 for Number cards, 0 for Action/Wild cards).
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Gets the point value of the card used in round-end scoring calculation.
        /// </summary>
        public int PointValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Card"/> struct.
        /// </summary>
        /// <param name="id">Unique card identifier.</param>
        /// <param name="color">Card suit color.</param>
        /// <param name="type">Card functional type.</param>
        /// <param name="value">Numeric face value (0-9 for number cards, otherwise 0).</param>
        public Card(string id, CardColor color, CardType type, int value = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Card ID cannot be null or whitespace.", nameof(id));
            }

            if (type == CardType.Number && (value < 0 || value > 9))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Number card values must be between 0 and 9.");
            }

            Id = id;
            Color = color;
            Type = type;
            Value = (type == CardType.Number) ? value : 0;
            PointValue = CalculatePointValue(type, Value);
        }

        /// <summary>
        /// Calculates the official UNO point value for a given card type and numeric value.
        /// </summary>
        /// <param name="type">Card type.</param>
        /// <param name="value">Numeric value.</param>
        /// <returns>Point value (0-9 for numbers, 20 for actions, 50 for wilds).</returns>
        public static int CalculatePointValue(CardType type, int value)
        {
            return type switch
            {
                CardType.Number => value,
                CardType.Skip => 20,
                CardType.Reverse => 20,
                CardType.DrawTwo => 20,
                CardType.Wild => 50,
                CardType.WildDrawFour => 50,
                _ => 0
            };
        }

        /// <summary>
        /// Factory method to create a suited number card.
        /// </summary>
        public static Card CreateNumber(string id, CardColor color, int value)
        {
            if (color == CardColor.Wild)
            {
                throw new ArgumentException("Number cards cannot have Wild color.", nameof(color));
            }
            return new Card(id, color, CardType.Number, value);
        }

        /// <summary>
        /// Factory method to create a suited action card (Skip, Reverse, DrawTwo).
        /// </summary>
        public static Card CreateAction(string id, CardColor color, CardType type)
        {
            if (color == CardColor.Wild)
            {
                throw new ArgumentException("Action cards must belong to a specific suit color.", nameof(color));
            }
            if (type == CardType.Number || type == CardType.Wild || type == CardType.WildDrawFour)
            {
                throw new ArgumentException("Invalid action card type specified.", nameof(type));
            }
            return new Card(id, color, type, 0);
        }

        /// <summary>
        /// Factory method to create a Wild or Wild Draw Four card.
        /// </summary>
        public static Card CreateWild(string id, CardType type)
        {
            if (type != CardType.Wild && type != CardType.WildDrawFour)
            {
                throw new ArgumentException("Card type must be Wild or WildDrawFour.", nameof(type));
            }
            return new Card(id, CardColor.Wild, type, 0);
        }

        /// <summary>
        /// Determines whether this instance equals another <see cref="Card"/>.
        /// </summary>
        public bool Equals(Card other)
        {
            return Id == other.Id;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current card.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is Card other && Equals(other);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        /// <summary>
        /// Overrides equality operator.
        /// </summary>
        public static bool operator ==(Card left, Card right) => left.Equals(right);

        /// <summary>
        /// Overrides inequality operator.
        /// </summary>
        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        /// <summary>
        /// Returns a human-readable string representation of the card.
        /// </summary>
        public override string ToString()
        {
            return Type switch
            {
                CardType.Number => $"{Color} {Value} [{Id}]",
                CardType.Skip => $"{Color} Skip [{Id}]",
                CardType.Reverse => $"{Color} Reverse [{Id}]",
                CardType.DrawTwo => $"{Color} Draw Two (+2) [{Id}]",
                CardType.Wild => $"Wild [{Id}]",
                CardType.WildDrawFour => $"Wild Draw Four (+4) [{Id}]",
                _ => $"Card [{Id}]"
            };
        }
    }
}
