namespace NetSQS
{
    /// <summary>
    /// Contains information regarding the number of messages on a specific queue.
    /// </summary>
    public class NumberOfMessagesResponse
    {
        /// <summary>
        /// Name of the queue that this response is related to.
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// Approximate total number of messages on the queue.
        /// </summary>
        public int ApproximateNumberOfMessages { get; set; }

        /// <summary>
        /// Approximate total number of messages on the queue that are currently not visible.
        /// </summary>
        public int ApproximateNumberOfMessagesNotVisible { get; set; }

        /// <summary>
        /// Approximate total number of delayed messages on the queue.
        /// </summary>
        public int ApproximateNumberOfMessagesDelayed { get; set; }
    }
}
