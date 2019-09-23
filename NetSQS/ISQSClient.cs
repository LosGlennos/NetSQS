using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace NetSQS
{
    // ReSharper disable InconsistentNaming
    public interface ISQSClient
    {
        Task<SendMessageResponse> SendMessageAsync(string message, string queueName);
        Task<ReceiveMessageResponse> ReceiveMessageAsync(string queueName);
        Task<ReceiveMessageResponse> ReceiveMessageAsync(
            string queueName = null,
            List<string> attributeNames = null,
            int? maxNumberOfMessages = null,
            List<string> messageAttributeNames = null,
            string receiveRequestAttemptId = null,
            int? visibilityTimeoutSeconds = null,
            int waitTimeSeconds = 0);
        Task<string> CreateStandardQueueAsync(string queueName);
        Task<string> CreateFifoQueueAsync(string queueName);
        Task<string> CreateQueueAsync(string queueName, bool isFifo, bool isEncrypted, int retentionPeriod = 345600, int visibilityTimeout = 30);
        Task DeleteQueueAsync(string queueName);
        Task<List<string>> ListQueuesAsync();
        Task PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, Task<bool>> asyncMessageProcessor);
        Task PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, bool> messageProcessor);
        Task<Task> PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor);

        Task<Task> PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, bool> messageProcessor);
    }
}
