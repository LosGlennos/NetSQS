using System;
using System.Collections.Generic;
using System.Text;

namespace NetSQS
{
    /// <summary>
    /// A message and attribute grouping
    /// </summary>
    public class BatchMessageRequest
    {
        /// <summary>
        /// The content of the message being sent
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// Dictionary of message attributes to be appended to the message
        /// </summary>
        public readonly Dictionary<string, string> MessageAttributes;

        /// <summary>
        /// Creates a message and attribute grouping
        /// </summary>
        /// <param name="message">The message to be sent</param>
        /// <param name="messageAttributes">Attributes to be attached to the message</param>
        public BatchMessageRequest(string message, Dictionary<string, string> messageAttributes = null)
        {
            Message = message;
            MessageAttributes = messageAttributes ?? new Dictionary<string, string>();
        }
    }
}
