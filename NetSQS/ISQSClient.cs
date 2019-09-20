using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace NetSQS
{
    // ReSharper disable InconsistentNaming
    public interface ISQSClient
    {
        Task<SendMessageResponse> SendMessageAsync(string message, string queue);
        Task<ReceiveMessageResponse> ReceiveMessageAsync(string queueUrl);
        Task<ReceiveMessageResponse> ReceiveMessageAsync(
            string queueUrl = null,
            List<string> attributeNames = null,
            int? maxNumberOfMessages = null,
            List<string> messageAttributeNames = null,
            string receiveRequestAttemptId = null,
            int? visibilityTimeoutSeconds = null,
            int waitTimeSeconds = 0);
        Task<string> CreateStandardQueueAsync(string queueName);
        Task<string> CreateFifoQueueAsync(string queueName);
        Task<string> CreateQueueAsync(string queueName, int retentionPeriod, int visibilityTimeout, bool isFifo, bool isEncrypted);
        Task DeleteQueueAsync(string queueName);
        Task<List<string>> ListQueuesAsync();
        Task PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, Task<bool>> asyncMessageProcessor);
        Task PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, bool> messageProcessor);
        Task PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor);

        Task PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, bool> messageProcessor);
    }
}
