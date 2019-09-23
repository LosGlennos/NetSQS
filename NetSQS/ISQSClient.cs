﻿using System;
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
        /// <returns></returns>
        Task<string> SendMessageAsync(string message, string queueName);

        /// <summary>
        /// Creates a Standard queue with default values
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        Task<string> CreateStandardQueueAsync(string queueName);

        /// <summary>
        /// Creates a FIFO queue with default values. Queue name must end with .fifo
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        Task<string> CreateFifoQueueAsync(string queueName);

        /// <summary>
        /// Creates a queue in SQS
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="retentionPeriod">The number of seconds the messages will be kept on the queue before being deleted. Valid values: 60 to 1,209,600. Default: 345,600</param>
        /// <param name="visibilityTimeout">The time period in seconds for which a message should not be picked by another processor. Valid values: 0 to 43,200. Default: 30</param>
        /// <param name="isFifo">Defines if the queue created is a FIFO queue.</param>
        /// <param name="isEncrypted">Used if server side encryption is active.</param>
        /// <returns></returns>
        Task<string> CreateQueueAsync(string queueName, bool isFifo, bool isEncrypted, int retentionPeriod = 345600, int visibilityTimeout = 30);

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
        /// Polls the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// Will run a polling task by starting a Task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTime">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="asyncMessageProcessor">The message processor that handles the message received from the queue.</param>
        /// <returns></returns>
        CancellationTokenSource PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, Task<bool>> asyncMessageProcessor);

        /// <summary>
        /// Polls the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// Will run a polling task by starting a Task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTime">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="messageProcessor">The message processor that handles the message received from the queue.</param>
        /// <returns></returns>
        CancellationTokenSource PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, bool> messageProcessor);

        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then polls the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will run a polling task by starting a Task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTime">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="asyncMessageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <returns></returns>
        Task<CancellationTokenSource> PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor);

        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then polls the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will run a polling task by starting a Task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTime">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="messageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <returns></returns>
        Task<CancellationTokenSource> PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, bool> messageProcessor);
    }
}