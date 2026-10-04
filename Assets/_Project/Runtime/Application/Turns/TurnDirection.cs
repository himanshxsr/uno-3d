#nullable enable

namespace Uno.Application.Turns
{
    /// <summary>
    /// Specifies turn execution rotation around the 4-player table.
    /// </summary>
    public enum TurnDirection
    {
        /// <summary>
        /// Clockwise turn advancement (+1 player index step).
        /// </summary>
        Clockwise = 1,

        /// <summary>
        /// Counter-clockwise turn advancement (-1 player index step).
        /// </summary>
        CounterClockwise = -1
    }
}
