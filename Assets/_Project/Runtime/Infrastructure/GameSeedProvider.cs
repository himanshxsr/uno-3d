#nullable enable

using System;

namespace Uno.Infrastructure
{
    /// <summary>
    /// Provides deterministic or time-based seeds for deck shuffling and AI delay variance.
    /// </summary>
    public static class GameSeedProvider
    {
        /// <summary>
        /// Returns a seed derived from the current UTC timestamp.
        /// </summary>
        public static int CreateTimeSeed()
        {
            return (int)(DateTime.UtcNow.Ticks % int.MaxValue);
        }

        /// <summary>
        /// Returns the provided seed, or a time-based seed when null.
        /// </summary>
        public static int Resolve(int? seed)
        {
            return seed ?? CreateTimeSeed();
        }
    }
}
