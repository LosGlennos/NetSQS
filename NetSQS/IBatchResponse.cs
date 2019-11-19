namespace NetSQS
{
    /// <summary>
    /// The result from sending a batch of messages to a queue. 
    /// </summary>
    public interface IBatchResponse
    {
        /// <summary>
        /// True if all the messages in the batch were processed correctly
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// An array of metadata containing information on whether each message was processed successfully
        /// </summary>
        BatchResponse.SendResult[] SendResults { get; }

        /// <summary>
        /// Returns an array of successfully sent messages
        /// </summary>
        /// <returns></returns>
        BatchResponse.SendResult[] GetSuccessful();

        /// <summary>
        /// Returns an array of messages that failed to send
        /// </summary>
        /// <returns></returns>
        BatchResponse.SendResult[] GetFailed();
    }
}
