#nullable enable

namespace Uno.Core.Enums
{
    /// <summary>
    /// Specifies the card functional categories in the UNO deck.
    /// </summary>
    public enum CardType
    {
        /// <summary>
        /// Standard numeric card (values 0 through 9).
        /// </summary>
        Number = 0,

        /// <summary>
        /// Action card that skips the next player's turn.
        /// </summary>
        Skip = 1,

        /// <summary>
        /// Action card that reverses turn direction.
        /// </summary>
        Reverse = 2,

        /// <summary>
        /// Action card that forces the next player to draw 2 cards and forfeit their turn.
        /// </summary>
        DrawTwo = 3,

        /// <summary>
        /// Wild card allowing the active player to designate the active suit color.
        /// </summary>
        Wild = 4,

        /// <summary>
        /// Wild card allowing active suit color choice and forcing the next player to draw 4 cards and skip turn.
        /// </summary>
        WildDrawFour = 5
    }
}
