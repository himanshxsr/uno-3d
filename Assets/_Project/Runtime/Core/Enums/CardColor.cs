#nullable enable

namespace Uno.Core.Enums
{
    /// <summary>
    /// Represents the four standard card suit colors in UNO, plus the Wild neutral designation.
    /// </summary>
    public enum CardColor
    {
        /// <summary>
        /// Crimson Red suit.
        /// </summary>
        Red = 0,

        /// <summary>
        /// Royal Blue suit.
        /// </summary>
        Blue = 1,

        /// <summary>
        /// Emerald Green suit.
        /// </summary>
        Green = 2,

        /// <summary>
        /// Golden Yellow suit.
        /// </summary>
        Yellow = 3,

        /// <summary>
        /// Neutral color designation for Wild and Wild Draw Four cards prior to color selection.
        /// </summary>
        Wild = 4
    }
}
