using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetSQS
{
    // ReSharper disable InconsistentNaming
    public interface ISQSClient
    {
        /// <summary>
        /// Puts a message on the queue
        /// </summary>
        /// <param name="message">The message to be put on the queue</param>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="messageAttributes">A dictionary of string values to include in the message attributes</param>
        /// <returns></returns>
        Task<string> SendMessageAsync(string message, string queueName, Dictionary<string, string> messageAttributes = null);

        /// <summary>
        /// Puts a batch of messages on the queue
        /// </summary>
        /// <param name="messages">An array of messages to be put on the queue</param>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="messageAttributes">A dictionary of string values to include in the message attributes</param>
        /// <returns></returns>
        Task<IBatchResponse> SendMessageBatchAsync(BatchMessageRequest[] batchMessages, string queueName);

        /// <summary>
        /// Creates a Standard queue with default values
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        Task CreateStandardQueueAsync(string queueName);

        /// <summary>
        /// Creates a FIFO queue with default values. Queue name must end with .fifo
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        Task CreateStandardFifoQueueAsync(string queueName);

        /// <summary>
        /// Creates a queue in SQS
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="retentionPeriod">The number of seconds the messages will be kept on the queue before being deleted. Valid values: 60 to 1,209,600. Default: 345,600</param>
        /// <param name="visibilityTimeout">The time period in seconds for which a message should not be picked by another processor. Valid values: 0 to 43,200. Default: 30</param>
        /// <param name="isFifo">Defines if the queue created is a FIFO queue.</param>
        /// <param name="isEncrypted">Used if server side encryption is active.</param>
        Task CreateQueueAsync(string queueName, bool isFifo, bool isEncrypted, int retentionPeriod = 345600, int visibilityTimeout = 30);

        /// <summary>
        /// Deletes a queue with the specified name
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        Task<bool> DeleteQueueAsync(string queueName);

        /// <summary>
        /// Lists all the queues on the SQS.
        /// </summary>
        /// <returns></returns>
        Task<List<string>> ListQueuesAsync();

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="messageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        Task StartMessageReceiver(string queueName, MessageReceiverOptions options, Func<string, bool> messageProcessor,
            CancellationToken cancellationToken);

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="asyncMessageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        Task StartMessageReceiver(string queueName, MessageReceiverOptions options,
            Func<string, Task<bool>> asyncMessageProcessor, CancellationToken cancellationToken);

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="messageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        Task StartMessageReceiver(string queueName, MessageReceiverOptions options,
            Action<ISQSMessage> messageProcessor, CancellationToken cancellationToken);

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="asyncMessageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        Task StartMessageReceiver(string queueName, MessageReceiverOptions options,
            Func<ISQSMessage, Task> asyncMessageProcessor, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a message from the queue
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="receiptHandle">The identifier of the operation that received the message</param>
        /// <returns></returns>
        Task DeleteMessageAsync(string queueName, string receiptHandle);

        /// <summary>
        /// Gets information regarding the number of messages on a queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        Task<NumberOfMessagesResponse> GetNumberOfMessagesOnQueue(string queueName);
    }
}
