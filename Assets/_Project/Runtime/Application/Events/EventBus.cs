#nullable enable

using System;
using System.Collections.Generic;

namespace Uno.Application.Events
{
    /// <summary>
    /// Thread-safe, allocation-conscious implementation of <see cref="IEventBus"/>
    /// using delegate multicast combinations for zero-heap allocation during event dispatch.
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _subscriptions = new Dictionary<Type, Delegate>();
        private readonly object _lock = new object();

        /// <inheritdoc />
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (_lock)
            {
                Type eventType = typeof(T);
                if (_subscriptions.TryGetValue(eventType, out Delegate? existingDelegate))
                {
                    _subscriptions[eventType] = Delegate.Combine(existingDelegate, handler);
                }
                else
                {
                    _subscriptions[eventType] = handler;
                }
            }
        }

        /// <inheritdoc />
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (_lock)
            {
                Type eventType = typeof(T);
                if (_subscriptions.TryGetValue(eventType, out Delegate? existingDelegate))
                {
                    Delegate? newDelegate = Delegate.Remove(existingDelegate, handler);
                    if (newDelegate == null)
                    {
                        _subscriptions.Remove(eventType);
                    }
                    else
                    {
                        _subscriptions[eventType] = newDelegate;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Publish<T>(T eventData)
        {
            Delegate? handlerDelegate;
            lock (_lock)
            {
                _subscriptions.TryGetValue(typeof(T), out handlerDelegate);
            }

            if (handlerDelegate is Action<T> action)
            {
                action.Invoke(eventData);
            }
        }

        /// <inheritdoc />
        public void ClearAllSubscriptions()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
            }
        }
    }
}
