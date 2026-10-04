#nullable enable

using System;
using UnityEngine;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Presentation.Cards
{
    /// <summary>
    /// ScriptableObject data provider mapping domain Card data (Color, Type, Value)
    /// to corresponding 2D Texture2D card face rendering assets.
    /// </summary>
    [CreateAssetMenu(fileName = "CardVisualProvider", menuName = "UNO 3D/Card Visual Provider")]
    public class CardVisualProvider : ScriptableObject
    {
        [Header("Card Back Texture")]
        [SerializeField] private Texture2D? _cardBackTexture;

        [Header("Red Suit Textures")]
        [SerializeField] private Texture2D[] _redNumberTextures = new Texture2D[10];
        [SerializeField] private Texture2D? _redSkipTexture;
        [SerializeField] private Texture2D? _redReverseTexture;
        [SerializeField] private Texture2D? _redDrawTwoTexture;

        [Header("Blue Suit Textures")]
        [SerializeField] private Texture2D[] _blueNumberTextures = new Texture2D[10];
        [SerializeField] private Texture2D? _blueSkipTexture;
        [SerializeField] private Texture2D? _blueReverseTexture;
        [SerializeField] private Texture2D? _blueDrawTwoTexture;

        [Header("Green Suit Textures")]
        [SerializeField] private Texture2D[] _greenNumberTextures = new Texture2D[10];
        [SerializeField] private Texture2D? _greenSkipTexture;
        [SerializeField] private Texture2D? _greenReverseTexture;
        [SerializeField] private Texture2D? _greenDrawTwoTexture;

        [Header("Yellow Suit Textures")]
        [SerializeField] private Texture2D[] _yellowNumberTextures = new Texture2D[10];
        [SerializeField] private Texture2D? _yellowSkipTexture;
        [SerializeField] private Texture2D? _yellowReverseTexture;
        [SerializeField] private Texture2D? _yellowDrawTwoTexture;

        [Header("Wild Textures")]
        [SerializeField] private Texture2D? _wildChooseColorTexture;
        [SerializeField] private Texture2D? _wildDrawFourTexture;

        public Texture2D? CardBackTexture => _cardBackTexture;

        /// <summary>
        /// Retrieves the corresponding front face texture for a given domain card.
        /// </summary>
        /// <param name="card">Target card data model.</param>
        /// <returns>Mapped <see cref="Texture2D"/> asset, or null if unassigned.</returns>
        public Texture2D? GetCardFrontTexture(Card card)
        {
            return card.Type switch
            {
                CardType.Number => GetNumberTexture(card.Color, card.Value),
                CardType.Skip => GetActionTexture(card.Color, CardType.Skip),
                CardType.Reverse => GetActionTexture(card.Color, CardType.Reverse),
                CardType.DrawTwo => GetActionTexture(card.Color, CardType.DrawTwo),
                CardType.Wild => _wildChooseColorTexture,
                CardType.WildDrawFour => _wildDrawFourTexture,
                _ => null
            };
        }

        private Texture2D? GetNumberTexture(CardColor color, int value)
        {
            if (value < 0 || value > 9) return null;
            return color switch
            {
                CardColor.Red => GetSafeTexture(_redNumberTextures, value),
                CardColor.Blue => GetSafeTexture(_blueNumberTextures, value),
                CardColor.Green => GetSafeTexture(_greenNumberTextures, value),
                CardColor.Yellow => GetSafeTexture(_yellowNumberTextures, value),
                _ => null
            };
        }

        private Texture2D? GetActionTexture(CardColor color, CardType actionType)
        {
            return color switch
            {
                CardColor.Red => actionType switch
                {
                    CardType.Skip => _redSkipTexture,
                    CardType.Reverse => _redReverseTexture,
                    CardType.DrawTwo => _redDrawTwoTexture,
                    _ => null
                },
                CardColor.Blue => actionType switch
                {
                    CardType.Skip => _blueSkipTexture,
                    CardType.Reverse => _blueReverseTexture,
                    CardType.DrawTwo => _blueDrawTwoTexture,
                    _ => null
                },
                CardColor.Green => actionType switch
                {
                    CardType.Skip => _greenSkipTexture,
                    CardType.Reverse => _greenReverseTexture,
                    CardType.DrawTwo => _greenDrawTwoTexture,
                    _ => null
                },
                CardColor.Yellow => actionType switch
                {
                    CardType.Skip => _yellowSkipTexture,
                    CardType.Reverse => _yellowReverseTexture,
                    CardType.DrawTwo => _yellowDrawTwoTexture,
                    _ => null
                },
                _ => null
            };
        }

        private static Texture2D? GetSafeTexture(Texture2D[] array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return null;
            return array[index];
        }
    }
}
