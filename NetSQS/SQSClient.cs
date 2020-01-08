using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NetSQS
{
    // ReSharper disable InconsistentNaming
    public static class NetSQS
    {
        /// <summary>
        /// Creates a new singleton instance of SQS Client. If both endpoint and region are specified, the region endpoint will override the ServiceURL endpoint.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="endpoint">The Amazon SQS endpoint</param>
        /// <param name="region">The system name for the region. Example: eu-west-1</param>
        /// <param name="awsAccessKeyId">If not specified, AWS will pick this using the Default Credential Provider Chain</param>
        /// <param name="awsSecretAccessKey">If not specified, AWS will pick this using the Default Credential Provider Chain</param>
        public static void AddSQSService(this IServiceCollection services, string endpoint, string region, string awsAccessKeyId = null, string awsSecretAccessKey = null)
        {
            services.TryAddSingleton<ISQSClient>(s => new SQSClient(endpoint, region, awsAccessKeyId, awsSecretAccessKey));
        }
    }

    public class SQSClient : ISQSClient
    {
        private readonly AmazonSQSClient _client;

        private Dictionary<string, string> _queueCache;

        /// <summary>
        /// Creates a new SQS Client. If both endpoint and region are specified, the region endpoint will override the ServiceURL endpoint.
        /// </summary>
        /// <param name="endpoint">SQS Endpoint</param>
        /// <param name="region">The system name for the region ex. eu-west-1</param>
        /// <param name="awsAccessKeyId">If not specified, AWS will pick this using the Default Credential Provider Chain</param>
        /// <param name="awsSecretAccessKey">If not specified, AWS will pick this using the Default Credential Provider Chain</param>
        /// <returns></returns>
        public SQSClient(string endpoint, string region, string awsAccessKeyId = null, string awsSecretAccessKey = null)
        {
            _client = CreateSQSClient(endpoint, region, awsAccessKeyId, awsSecretAccessKey);
        }

        /// <summary>
        /// Creates a new SQS Client. If both endpoint and region are specified, the region endpoint will override the ServiceURL endpoint.
        /// </summary>
        /// <param name="endpoint">SQS Endpoint</param>
        /// <param name="region">The system name for the region ex. eu-west-1</param>
        /// <param name="awsAccessKeyId">If not specified, AWS will pick this using the Default Credential Provider Chain</param>
        /// <param name="awsSecretAccessKey">If not specified, AWS will pick this using the Default Credential Provider Chain</param>
        /// <returns></returns>
        private AmazonSQSClient CreateSQSClient(string endpoint, string region, string awsAccessKeyId = null, string awsSecretAccessKey = null)
        {
            var config = new AmazonSQSConfig();
            if (endpoint != null)
            {
                config.ServiceURL = endpoint;
            }
            else if (region != null)
            {
                config.RegionEndpoint = Regions.GetEndpoint(region);
            }

            if (awsAccessKeyId == null && awsSecretAccessKey == null)
            {
                return new AmazonSQSClient(config);
            }
            else
            {
                return new AmazonSQSClient(awsAccessKeyId, awsSecretAccessKey, config);
            }
        }

        /// <summary>
        /// Puts a message on the queue
        /// </summary>
        /// <param name="message">The message to be put on the queue</param>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="messageAttributes">A dictionary of string values to include in the message attributes</param>
        /// <returns></returns>
        public async Task<string> SendMessageAsync(string message, string queueName, Dictionary<string, string> messageAttributes = null)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);

            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = message,
                MessageGroupId = queueName.EndsWith(".fifo") ? queueUrl : null
            };

            if (messageAttributes != null)
            {
                var sqsMessageAttributes = messageAttributes.ToDictionary(attribute => attribute.Key, attribute => new MessageAttributeValue {StringValue = attribute.Value, DataType = "String"});
                request.MessageAttributes = sqsMessageAttributes;
            }

            SendMessageResponse response = null;
            var retryCounter = 0;
            while (response == null)
            {
                try
                {
                    response = await _client.SendMessageAsync(request);
                }
                catch (AmazonSQSException e)
                {
                    if (e.Message.EndsWith("Throttled") && retryCounter < 10)
                    {
                        retryCounter += 1;
                        await Task.Delay(retryCounter * 3);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return response.MessageId;
        }

        /// <summary>
        /// Puts a batch of messages on the queue
        ///
        /// Supports a maximum of 10 messages.
        /// </summary>
        /// <param name="batchMessages">An array of messages to be put on the queue</param>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        public async Task<IBatchResponse> SendMessageBatchAsync(BatchMessageRequest[] batchMessages, string queueName)
        {
            if (batchMessages.Length > 10)
            {
                throw new ArgumentException($"AWS SQS supports a max message number of 10 messages. {batchMessages.Length} were received.", nameof(batchMessages));
            }

            var queueUrl = await GetQueueUrlAsync(queueName);

            var sendMessageBatchRequest = new SendMessageBatchRequest
            {
                Entries = batchMessages.Select((v, i) => new SendMessageBatchRequestEntry(i.ToString(), v.Message)
                {
                    MessageAttributes = v.MessageAttributes.ToDictionary(attribute => attribute.Key, attribute => new MessageAttributeValue {StringValue = attribute.Value, DataType = "String"}),
                    MessageGroupId = queueName.EndsWith(".fifo") ? queueUrl : null
                }).ToList(),
                QueueUrl = queueUrl
            };

            var retryCounter = 0;
            SendMessageBatchResponse awsBatchResponse = null;
            while (awsBatchResponse == null)
            {
                try
                {
                    awsBatchResponse = await _client.SendMessageBatchAsync(sendMessageBatchRequest);
                }
                catch (AmazonSQSException e)
                {
                    if (e.Message.EndsWith("Throttled") && retryCounter < 10)
                    {
                        retryCounter += 1;
                        await Task.Delay(retryCounter * 3);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return BatchResponse.FromAwsBatchResponse(awsBatchResponse, batchMessages);
        }

        /// <summary>
        /// Creates a Standard queue with default values
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        public async Task CreateStandardQueueAsync(string queueName)
        {
            await CreateQueueAsync(queueName, false, true);
        }

        /// <summary>
        /// Creates a FIFO queue with default values. Queue name must end with .fifo
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        public async Task CreateStandardFifoQueueAsync(string queueName)
        {
            await CreateQueueAsync(queueName, true, true);
        }

        /// <summary>
        /// Creates a queue in SQS
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="retentionPeriod">The number of seconds the messages will be kept on the queue before being deleted. Valid values: 60 to 1,209,600. Default: 345,600</param>
        /// <param name="visibilityTimeout">The time period in seconds for which a message should not be picked by another processor. Valid values: 0 to 43,200. Default: 30</param>
        /// <param name="isFifo">Defines if the queue created is a FIFO queue.</param>
        /// <param name="isEncrypted">Used if server side encryption is active.</param>
        public async Task CreateQueueAsync(string queueName, bool isFifo, bool isEncrypted, int retentionPeriod = 345600, int visibilityTimeout = 30)
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
                if (queueName.Length <= 5 || queueName.Substring(queueName.Length - 5) != ".fifo")
                {
                    throw new ArgumentException("Queue name must end with '.fifo'", nameof(queueName));
                }

                attributes.Add("FifoQueue", "true");
                attributes.Add("ContentBasedDeduplication", "true");
            }
            else if (queueName.EndsWith(".fifo"))
            {
                throw new ArgumentException("Non fifo queue names can't end with .fifo");
            }

            var request = new CreateQueueRequest
            {
                QueueName = queueName,
                Attributes = attributes
            };

            await _client.CreateQueueAsync(request);
        }

        /// <summary>
        /// Deletes a queue with the specified name
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        public async Task<bool> DeleteQueueAsync(string queueName)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var request = new DeleteQueueRequest(queueUrl);
            var response = await _client.DeleteQueueAsync(request);
            var success = (int)response.HttpStatusCode >= 200 && (int)response.HttpStatusCode <= 299;
            return success;
        }

        /// <summary>
        /// Lists all the queues on the SQS.
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> ListQueuesAsync()
        {
            var request = new ListQueuesRequest();
            var response = await _client.ListQueuesAsync(request);
            return response.QueueUrls;
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="messageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, MessageReceiverOptions options, Func<string, bool> messageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, options, async (arg) => messageProcessor(arg), cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="messageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, MessageReceiverOptions options,
            Action<ISQSMessage> messageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, options, async (arg) => messageProcessor(arg), cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="asyncMessageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, MessageReceiverOptions options, Func<ISQSMessage, Task> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, options, asyncMessageProcessor, cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="options">Options for the receiver behaviour.</param>
        /// <param name="asyncMessageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, MessageReceiverOptions options, Func<string, Task<bool>> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, options, asyncMessageProcessor, cancellationToken);
        }

        /// <summary>
        /// Gets the URL for the queue from its name.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        private async Task<string> GetQueueUrlAsync(string queueName)
        {
            if (_queueCache == null || !_queueCache.ContainsKey(queueName))
            {
                var request = new GetQueueUrlRequest(queueName);
                var response = await _client.GetQueueUrlAsync(request);

                _queueCache ??= new Dictionary<string, string>();
                _queueCache.Add(queueName, response.QueueUrl);
            }

            _queueCache.TryGetValue(queueName, out var queueUrl);
            return queueUrl;
        }

        /// <summary>
        /// Deletes a message from the queue
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="receiptHandle">The identifier of the operation that received the message</param>
        /// <returns></returns>
        public async Task DeleteMessageAsync(string queueName, string receiptHandle)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var request = new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            };

            await _client.DeleteMessageAsync(request);
        }

        /// <summary>
        /// Gets information regarding the number of messages on a queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        public async Task<NumberOfMessagesResponse> GetNumberOfMessagesOnQueue(string queueName)
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            var response = await _client.GetQueueAttributesAsync(new GetQueueAttributesRequest(queueUrl, new List<string>
            {
                "ApproximateNumberOfMessages",
                "ApproximateNumberOfMessagesNotVisible",
                "ApproximateNumberOfMessagesDelayed"
            }));

            return new NumberOfMessagesResponse
            {
                QueueName = queueName,
                ApproximateNumberOfMessages = response.ApproximateNumberOfMessages,
                ApproximateNumberOfMessagesNotVisible = response.ApproximateNumberOfMessagesNotVisible,
                ApproximateNumberOfMessagesDelayed = response.ApproximateNumberOfMessagesDelayed
            };
        }

        /// <summary>
        /// Synchronously waits for a queue to be available.
        /// </summary>
        private void WaitForQueue(string queueName, MessageReceiverOptions options, CancellationToken cancellationToken)
        {
            for (var i = 0; i < options.WaitForQueueTimeoutSeconds; i++)
            {
                try
                {
                    GetQueueUrlAsync(queueName).GetAwaiter().GetResult();
                    return;
                }
                catch (QueueDoesNotExistException)
                {
                    Task.Delay(1000, cancellationToken).Wait(cancellationToken);
                }
            }

            throw new QueueDoesNotExistException($"Queue {queueName} does still not exist after waiting for {options.WaitForQueueTimeoutSeconds} seconds.");
        }

        /// <summary>
        /// Receives a message from the queue
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="attributeNames">A list of attributes that need to be returned along with each message.</param>
        /// <param name="maxNumberOfMessages">The maximum number of messages that will be picked off the queue for each poll. Valid values: 1 to 10</param>
        /// <param name="messageAttributeNames">The message attribute names</param>
        /// <param name="receiveRequestAttemptId">Sets the receive request attempt id. Used if there is a networking error when getting a message.</param>
        /// <param name="visibilityTimeoutSeconds">The time for which the message should not be picked by other processors</param>
        /// <param name="waitTimeSeconds">The amount of time the client will try to get a message from the queue</param>
        /// <returns></returns>
        private async Task<ReceiveMessageResponse> ReceiveMessageAsync(
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

            ReceiveMessageResponse response = null;
            var delayTime = 0;

            while (response == null)
            {
                try
                {
                    response = await _client.ReceiveMessageAsync(request);
                }
                catch (AmazonSQSException e)
                {
                    if (e.Message.EndsWith("Throttled"))
                    {
                        if (delayTime < 1000) delayTime += 100;
                        await Task.Delay(delayTime);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return response;
        }

        private Task StartMessageReceiverInternal(string queueName, MessageReceiverOptions options,
            Func<string, Task<bool>> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            if (options.MaxNumberOfMessagesPerPoll > 10 || options.MaxNumberOfMessagesPerPoll < 1)
                throw new ArgumentException($"nameof(options.MaxNumberOfMessagesPerPoll) must be between 1 and 10",
                    nameof(options.MaxNumberOfMessagesPerPoll));

            if (options.MessagePollWaitTimeSeconds < 0 || options.MessagePollWaitTimeSeconds > 20)
                throw new ArgumentException($"{nameof(options.MessagePollWaitTimeSeconds)} must be within the range 0 to 20.",
                    nameof(options.MessagePollWaitTimeSeconds));

            if (options.VisibilityTimeoutSeconds.HasValue && options.VisibilityTimeoutSeconds.Value < 0)
                throw new ArgumentException($"{nameof(options.VisibilityTimeoutSeconds)} must be 0 or greater, or null.",
                    nameof(options.MessagePollWaitTimeSeconds));

            if (options.WaitForQueue) WaitForQueue(queueName, options, cancellationToken);

            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var receiveMessageResponse = await ReceiveMessageAsync(queueName,
                        waitTimeSeconds: options.MessagePollWaitTimeSeconds,
                        maxNumberOfMessages: options.MaxNumberOfMessagesPerPoll,
                        visibilityTimeoutSeconds: options.VisibilityTimeoutSeconds);

                    foreach (var message in receiveMessageResponse.Messages)
                    {
                        var success = await asyncMessageProcessor(message.Body);
                        if (success)
                        {
                            await DeleteMessageAsync(queueName, message.ReceiptHandle);
                        }
                    }
                }
            }, cancellationToken);
        }

        private Task StartMessageReceiverInternal(string queueName, MessageReceiverOptions options,
            Func<ISQSMessage, Task> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            if (options.MaxNumberOfMessagesPerPoll > 10 || options.MaxNumberOfMessagesPerPoll < 1)
                throw new ArgumentException($"nameof(options.MaxNumberOfMessagesPerPoll) must be between 1 and 10",
                    nameof(options.MaxNumberOfMessagesPerPoll));

            if (options.MessagePollWaitTimeSeconds < 0 || options.MessagePollWaitTimeSeconds > 20)
                throw new ArgumentException($"{nameof(options.MessagePollWaitTimeSeconds)} must be within the range 0 to 20.",
                    nameof(options.MessagePollWaitTimeSeconds));

            if (options.VisibilityTimeoutSeconds.HasValue && options.VisibilityTimeoutSeconds.Value < 0)
                throw new ArgumentException($"{nameof(options.VisibilityTimeoutSeconds)} must be 0 or greater, or null.",
                    nameof(options.MessagePollWaitTimeSeconds));

            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var messageAttributesQuery = new List<string> { "All" };
                    var receiveMessageResponse = await ReceiveMessageAsync(queueName,
                        waitTimeSeconds: options.MessagePollWaitTimeSeconds,
                        maxNumberOfMessages: options.MaxNumberOfMessagesPerPoll,
                        visibilityTimeoutSeconds: options.VisibilityTimeoutSeconds,
                        messageAttributeNames: messageAttributesQuery);

                    foreach (var message in receiveMessageResponse.Messages)
                    {
                        var sqsMessage = new SQSMessage(this, queueName, message.ReceiptHandle)
                        {
                            Body = message.Body
                        };

                        if (message.MessageAttributes.Count > 0)
                        {
                            var messageAttributes = new Dictionary<string, string>();
                            foreach (var messageAttribute in message.MessageAttributes)
                            {
                                if (messageAttribute.Value.StringValue != null)
                                    messageAttributes.Add(messageAttribute.Key, messageAttribute.Value.StringValue);
                            }

                            sqsMessage.MessageAttributes = messageAttributes;
                        }

                        await asyncMessageProcessor(sqsMessage);
                    }
                }
            }, cancellationToken);
        }
    }
}