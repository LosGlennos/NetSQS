namespace NetSQS
{
    /// <summary>
    /// Options for the StartMessageReceiver methods.
    /// </summary>
    public class MessageReceiverOptions
    {
        /// <summary>
        /// Maximum number of seconds to wait for messages to arrive before doing a new request. A value greater than 0 will enable long polling. Valid range is 0 to 20 seconds.
        /// Defaults to 20.
        /// </summary>
        public int MessagePollWaitTimeSeconds { get; set; } = 20;

        /// <summary>
        /// Maximum number of messages per message poll. Valid range is 1 to 10.
        /// Defaults to 1.
        /// </summary>
        public int MaxNumberOfMessagesPerPoll { get; set; } = 1;

        /// <summary>
        /// The message visibility timeout in seconds. null will use the queue's default message timeout.
        /// Defaults to null.
        /// </summary>
        public int? VisibilityTimeoutSeconds { get; set; } = null;

        /// <summary>
        /// true if the message receiver should wait for the queue to exist. false to fail immediately if the queue does not exist.
        /// Defaults to false.
        /// </summary>
        public bool WaitForQueue { get; set; } = false;

        /// <summary>
        /// The time in seconds to wait for the queue to exist. Only applicable if WaitForQueue is true.
        /// Defaults to int.MaxValue seconds.
        /// </summary>
        public int WaitForQueueTimeoutSeconds { get; set; } = int.MaxValue;
    }
}
