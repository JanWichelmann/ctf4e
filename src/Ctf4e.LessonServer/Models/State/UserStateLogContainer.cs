using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Ctf4e.LessonServer.Models.State
{
    /// <summary>
    /// Manages log messages.
    /// Timestamps are stored in UTC format.
    /// </summary>
    public class UserStateLogContainer : IEnumerable<(DateTime timestamp, string message, string data)>
    {
        private readonly ConcurrentQueue<(DateTime timestamp, string message, string data)> _logMessages;
        
        /// <summary>
        /// Returns the maximum size of the log buffer.
        /// </summary>
        public int MaxSize { get; }

        /// <summary>
        /// Initializes a new log container.
        /// </summary>
        /// <param name="size">The maximum number of messages which can be stored in this container.</param>
        public UserStateLogContainer(int size)
        {
            if(size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "The size must be a positive, non-zero number.");

            _logMessages = new ConcurrentQueue<(DateTime timestamp, string message, string data)>();
            MaxSize = size;
        }

        /// <summary>
        /// Adds a new log message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="data">Data accompanying the message. Maybe used for user-provided inputs or error messages.</param>
        public void AddMessage(string message, string data)
        {
            // If the buffer is full, remove one entry to keep maximum size
            // We don't care about races here: Just make sure that there are never more than MaxSize items.
            if(_logMessages.Count == MaxSize)
                _logMessages.TryDequeue(out _);
            
            _logMessages.Enqueue((DateTime.UtcNow, message, data));
        }

        public IEnumerator<(DateTime timestamp, string message, string data)> GetEnumerator()
        {
            return _logMessages.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}