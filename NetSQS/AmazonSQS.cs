using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;

namespace NetSQS
{
    // ReSharper disable InconsistentNaming
    public static class NetSQS
    {
        public static void AddSQSService(this IServiceCollection services, string endpoint, string region)
        {
            services.AddSingleton<ISQSClient>(s => new SQSClient(endpoint, region));
        }
    }

    public class SQSClient : ISQSClient
    {
        private readonly AmazonSQSClient _client;

        public SQSClient(string endpoint, string region)
        {
            _client = CreateSQSClient(endpoint, region);
        }

        /// <summary>
        /// Creates a new SQS Client
        /// </summary>
        /// <param name="endpoint">SQS Endpoint</param>
        /// <param name="region">The system name for the region ex. eu-west-1</param>
        /// <returns></returns>
        private AmazonSQSClient CreateSQSClient(string endpoint, string region)
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = endpoint,
                RegionEndpoint = Regions.GetEndpoint(region)
            };

            return new AmazonSQSClient(config);
        }

        public async Task<SendMessageResponse> SendMessageAsync(string message, string queueName)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = message
            };

            var response = await _client.SendMessageAsync(request);

            return response;
        }

        public async Task<ReceiveMessageResponse> ReceiveMessageAsync(string queueName)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var request = new ReceiveMessageRequest(queueUrl);

            var response = await _client.ReceiveMessageAsync(request);
            return response;
        }

        public async Task<ReceiveMessageResponse> ReceiveMessageAsync(
            string queueName,
            List<string> attributeNames = null,
            int? maxNumberOfMessages = null,
            List<string> messageAttributeNames = null,
            string receiveRequestAttemptId = null,
            int? visibilityTimeoutSeconds = null,
            int waitTimeSeconds = 0)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = attributeNames,

                // Valid values: 1 to 10, Default: 1. See: https://github.com/aws/aws-sdk-net/blob/master/sdk/src/Services/SQS/Generated/Model/ReceiveMessageRequest.cs 
                MaxNumberOfMessages = maxNumberOfMessages ?? 1,
                MessageAttributeNames = messageAttributeNames,
                ReceiveRequestAttemptId = receiveRequestAttemptId,

                // Valid values 0 to 43200, Default: 30. See: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html
                VisibilityTimeout = visibilityTimeoutSeconds ?? 30,

                // If greater than 0, long polling is in effect. See: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html#sqs-long-polling
                WaitTimeSeconds = waitTimeSeconds
            };

            var response = await _client.ReceiveMessageAsync(request);
            return response;
        }

        public async Task<string> CreateStandardQueueAsync(string queueName)
        {
            return await CreateQueueAsync(queueName, 345600, 30, false, true);
        }

        public async Task<string> CreateFifoQueueAsync(string queueName)
        {
            return await CreateQueueAsync(queueName, 345600, 30, true, true);
        }

        public async Task<string> CreateQueueAsync(string queueName, int retentionPeriod, int visibilityTimeout, bool isFifo, bool isEncrypted)
        {
            var attributes = new Dictionary<string, string>
            {
                {"MessageRetentionPeriod", retentionPeriod.ToString()},
                {"VisibilityTimeout", visibilityTimeout.ToString()}
            };

            if (isEncrypted)
            {
                attributes.Add("KmsMasterKeyId", "alias/aws/sqs");
                attributes.Add("KmsDataKeyReusePeriodSeconds", "300");
            }

            if (isFifo)
            {
                if (queueName.Substring(queueName.Length - 5) != ".fifo")
                {
                    throw new ArgumentException("Queue name must end with '.fifo'", nameof(queueName));
                }

                attributes.Add("FifoQueue", "true");
                attributes.Add("ContentBasedDeduplication", "true");
            }

            var request = new CreateQueueRequest
            {
                QueueName = queueName,
                Attributes = attributes
            };

            var queue = await _client.CreateQueueAsync(request);
            return queue.QueueUrl;
        }

        public async Task DeleteQueueAsync(string queueName)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var request = new DeleteQueueRequest(queueUrl);
            await _client.DeleteQueueAsync(request);
        }

        public async Task<List<string>> ListQueuesAsync()
        {
            var request = new ListQueuesRequest();
            var response = await _client.ListQueuesAsync(request);
            return response.QueueUrls;
        }

        public async Task PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, Task<bool>> asyncMessageProcessor)
        {
            if (maxNumberOfMessagesPerPoll > 10 || maxNumberOfMessagesPerPoll < 1)
            {
                throw new ArgumentException("Value must be between 1 and 10", nameof(maxNumberOfMessagesPerPoll));
            }

            var queueUrl = await GetQueueUrlAsync(queueName);

            while (true)
            {
                var receiveMessageResponse = await ReceiveMessageAsync(queueUrl, waitTimeSeconds: pollWaitTime, maxNumberOfMessages: maxNumberOfMessagesPerPoll);

                foreach (var message in receiveMessageResponse.Messages)
                {
                    var success = await asyncMessageProcessor(message.Body);
                    if (success)
                    {
                        await DeleteMessageAsync(queueUrl, message.ReceiptHandle);
                    }
                }
            }
        }

        public async Task PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, Func<string, bool> messageProcessor)
        {
            if (maxNumberOfMessagesPerPoll > 10 || maxNumberOfMessagesPerPoll < 1)
            {
                throw new ArgumentException("Value must be between 1 and 10", nameof(maxNumberOfMessagesPerPoll));
            }

            var queueUrl = await GetQueueUrlAsync(queueName);

            while (true)
            {
                var receiveMessageResponse = await ReceiveMessageAsync(queueUrl, waitTimeSeconds: pollWaitTime, maxNumberOfMessages: maxNumberOfMessagesPerPoll);

                foreach (var message in receiveMessageResponse.Messages)
                {
                    var success = messageProcessor(message.Body);
                    if (success)
                    {
                        await DeleteMessageAsync(queueUrl, message.ReceiptHandle);
                    }
                }
            }
        }

        public async Task PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor)
        {
            await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff);

            await PollQueueAsync(queueName, pollWaitTime, maxNumberOfMessagesPerPoll, asyncMessageProcessor);
        }

        public async Task PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, bool> messageProcessor)
        {
            await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff);

            await PollQueueAsync(queueName, pollWaitTime, maxNumberOfMessagesPerPoll, messageProcessor);
        }

        private async Task<string> GetQueueUrlAsync(string queueName)
        {
            var request = new GetQueueUrlRequest(queueName);
            var response = await _client.GetQueueUrlAsync(request);
            return response.QueueUrl;
        }

        private async Task DeleteMessageAsync(string queueUrl, string receiptHandle)
        {
            var request = new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            };

            await _client.DeleteMessageAsync(request);
        }

        private async Task WaitForQueueAsync(string queueName, int numRetries, int minBackOff, int maxBackOff)
        {
            for (var i = 0; i < numRetries; i++)
            {
                try
                {
                    await GetQueueUrlAsync(queueName);
                    return;
                }
                catch (AmazonSQSException e)
                {
                    var timeSleep = new Random().Next(maxBackOff - minBackOff) + minBackOff;
                    var timeSleepMilliseconds = (int) TimeSpan.FromSeconds(timeSleep).TotalMilliseconds;
                    Task.Delay(timeSleepMilliseconds).Wait();

                    if (i == numRetries - 1)
                    {
                        throw e;
                    }
                }
            }
        }
    }
}
