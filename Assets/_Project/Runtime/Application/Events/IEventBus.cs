#nullable enable

using System;

namespace Uno.Application.Events
{
    /// <summary>
    /// Strongly-typed, allocation-conscious event bus interface enabling decoupled system communication.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes a typed event handler action.
        /// </summary>
        /// <typeparam name="T">Event payload type.</typeparam>
        /// <param name="handler">Handler action callback.</param>
        void Subscribe<T>(Action<T> handler);

        /// <summary>
        /// Unsubscribes a typed event handler action.
        /// </summary>
        /// <typeparam name="T">Event payload type.</typeparam>
        /// <param name="handler">Handler action callback.</param>
        void Unsubscribe<T>(Action<T> handler);

        /// <summary>
        /// Broadcasts an event instance to all registered handlers for payload type T.
        /// </summary>
        /// <typeparam name="T">Event payload type.</typeparam>
        /// <param name="eventData">Event data instance.</param>
        void Publish<T>(T eventData);

        /// <summary>
        /// Clears all active event subscriptions across all types.
        /// </summary>
        void ClearAllSubscriptions();
    }
}
