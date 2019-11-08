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
                var sqsMessageAttributes = new Dictionary<string, MessageAttributeValue>();
                foreach (var attribute in messageAttributes)
                {
                    sqsMessageAttributes.Add(
                        attribute.Key, 
                        new MessageAttributeValue
                        {
                            StringValue = attribute.Value, 
                            DataType = "String"
                        });
                }

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
        /// Creates a Standard queue with default values
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        public async Task<string> CreateStandardQueueAsync(string queueName)
        {
            return await CreateQueueAsync(queueName, false, true);
        }

        /// <summary>
        /// Creates a FIFO queue with default values. Queue name must end with .fifo
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        public async Task<string> CreateStandardFifoQueueAsync(string queueName)
        {
            return await CreateQueueAsync(queueName, true, true);
        }

        /// <summary>
        /// Creates a queue in SQS
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="retentionPeriod">The number of seconds the messages will be kept on the queue before being deleted. Valid values: 60 to 1,209,600. Default: 345,600</param>
        /// <param name="visibilityTimeout">The time period in seconds for which a message should not be picked by another processor. Valid values: 0 to 43,200. Default: 30</param>
        /// <param name="isFifo">Defines if the queue created is a FIFO queue.</param>
        /// <param name="isEncrypted">Used if server side encryption is active.</param>
        /// <returns></returns>
        public async Task<string> CreateQueueAsync(string queueName, bool isFifo, bool isEncrypted, int retentionPeriod = 345600, int visibilityTimeout = 30)
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

            var queue = await _client.CreateQueueAsync(request);
            return queue.QueueUrl;
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
        /// Waits for the queue to be available by checking its availability for a given number of retries, then continuously checks the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will start a long running task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="messageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, int numRetries,
            int minBackOff, int maxBackOff, Func<string, bool> messageProcessor, CancellationToken cancellationToken)
        {
            var task = Task.Run(async () => await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff), cancellationToken);
            try
            {
                task.Wait(cancellationToken);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is QueueDoesNotExistException)
                {
                    throw new QueueDoesNotExistException(e.InnerException.Message);
                }
            }

            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                async (arg) => messageProcessor(arg), cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="asyncMessageProcessor">The message processor that handles the message received from the queue.</param>
        /// <returns></returns>
        [Obsolete("Use StartMessageReceiver-method that takes cancellation token as a parameter. This method will be removed in future releases", true)]
        public CancellationTokenSource StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, Func<string, Task<bool>> asyncMessageProcessor)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                asyncMessageProcessor, cancellationTokenSource.Token);
            return cancellationTokenSource;
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="messageProcessor">The message processor that handles the message received from the queue.</param>
        /// <returns></returns>
        [Obsolete("Use StartMessageReceiver-method that takes cancellation token as a parameter. This method will be removed in future releases", true)]
        public CancellationTokenSource StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, Func<string, bool> messageProcessor)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                async (arg) => messageProcessor(arg), cancellationTokenSource.Token);
            return cancellationTokenSource;
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="asyncMessageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, Func<string, Task<bool>> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                asyncMessageProcessor, cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="messageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">Will be used to request cancellation of the receiver process.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, Func<string, bool> messageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                async (arg) => messageProcessor(arg), cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="asyncMessageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, Func<ISQSMessage, Task> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                asyncMessageProcessor, cancellationToken);
        }

        /// <summary>
        /// Starts a long running process that checks the queue for any new messages, and handles the messages on the queue in the processor specified.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The waiting time for each poll of the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages to get with each poll. Valid values: 1 to 10</param>
        /// <param name="messageProcessor">The message processor that handles the message received from the queue.</param>
        /// <param name="cancellationToken">Will be used to request cancellation of the receiver process.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, Action<ISQSMessage> messageProcessor, CancellationToken cancellationToken)
        {
            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                async (arg) => messageProcessor(arg), cancellationToken);
        }

        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then continuously checks the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will start a long running task in a parallel thread that is not awaited.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="messageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, int numRetries,
            int minBackOff, int maxBackOff, Action<ISQSMessage> messageProcessor, CancellationToken cancellationToken)
        {
            var task = Task.Run(async () => await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff), cancellationToken);
            try
            {
                task.Wait(cancellationToken);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is QueueDoesNotExistException)
                {
                    throw new QueueDoesNotExistException(e.InnerException.Message);
                }
            }

            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                async (arg) => messageProcessor(arg), cancellationToken);
        }


        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then continuously checks the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will start a long running task in a parallel thread that is not awaited.
        ///
        /// Message should be explicitly acked to be removed from queue.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="asyncMessageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, int numRetries,
            int minBackOff, int maxBackOff, Func<ISQSMessage, Task> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            var task = Task.Run(async () => await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff), cancellationToken);
            try
            {
                task.Wait(cancellationToken);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is QueueDoesNotExistException)
                {
                    throw new QueueDoesNotExistException(e.InnerException.Message);
                }
            }

            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                asyncMessageProcessor, cancellationToken);
        }

        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then continuously checks the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will run a polling task by starting a Task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="asyncMessageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <returns></returns>
        [Obsolete("Use StartMessageReceiver-method that takes cancellation token as a parameter. This method will be removed in future releases", true)]
        public CancellationTokenSource StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor)
        {
            var task = Task.Run(async () => await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff));
            try
            {
                task.Wait();
            }
            catch (AggregateException e)
            {
                if (e.InnerException is QueueDoesNotExistException)
                {
                    throw new QueueDoesNotExistException(e.InnerException.Message);
                }
            }

            var cancellationTokenSource = new CancellationTokenSource();
            StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll, asyncMessageProcessor, cancellationTokenSource.Token);
            return cancellationTokenSource;
        }

        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then continuously checks the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will start a long running task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="asyncMessageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <param name="cancellationToken">The receiver process will check the status of this token and cancel the long running process if cancellation is requested.</param>
        /// <returns></returns>
        public Task StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll, int numRetries,
            int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            var task = Task.Run(async () => await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff), cancellationToken);
            try
            {
                task.Wait(cancellationToken);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is QueueDoesNotExistException)
                {
                    throw new QueueDoesNotExistException(e.InnerException.Message);
                }
            }

            return StartMessageReceiverInternal(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll,
                asyncMessageProcessor, cancellationToken);
        }

        /// <summary>
        /// Waits for the queue to be available by checking its availability for a given number of retries, then continuously checks the queue for new messages.
        /// Handles the messages on the queue in the processor specified.
        /// Will run a polling task by starting a Task in a parallel thread that is not awaited.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="pollWaitTimeSeconds">The amount of time the client will look for messages on the queue</param>
        /// <param name="maxNumberOfMessagesPerPoll">The maximum number of messages that will be picked from the queue.</param>
        /// <param name="numRetries">Number of connection retries to the queue.</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <param name="messageProcessor">The message processor which will handle the message picked from the queue</param>
        /// <returns></returns>
        [Obsolete("Use StartMessageReceiver-method that takes cancellation token as a parameter. This method will be removed in future releases", true)]
        public CancellationTokenSource StartMessageReceiver(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll,
            int numRetries, int minBackOff, int maxBackOff, Func<string, bool> messageProcessor)
        {
            var task = Task.Run(async () => await WaitForQueueAsync(queueName, numRetries, minBackOff, maxBackOff));
            try
            {
                task.Wait();
            }
            catch (AggregateException e)
            {
                if (e.InnerException is QueueDoesNotExistException)
                {
                    throw new QueueDoesNotExistException(e.InnerException.Message);
                }
            }

            return StartMessageReceiver(queueName, pollWaitTimeSeconds, maxNumberOfMessagesPerPoll, messageProcessor);
        }

        /// <summary>
        /// Gets the URL for the queue from its name.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <returns></returns>
        private async Task<string> GetQueueUrlAsync(string queueName)
        {
            var request = new GetQueueUrlRequest(queueName);
            var response = await _client.GetQueueUrlAsync(request);
            return response.QueueUrl;
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
        /// Does a given number of retries to check if the queue is available.
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="numRetries">The number of retries for which to see if the queue is available</param>
        /// <param name="minBackOff">The minimum back off time for which to look for new messages</param>
        /// <param name="maxBackOff">The maximum back off time for which to look for new messages</param>
        /// <returns></returns>
        private async Task WaitForQueueAsync(string queueName, int numRetries, int minBackOff, int maxBackOff)
        {
            for (var i = 0; i < numRetries; i++)
            {
                try
                {
                    await GetQueueUrlAsync(queueName);
                    return;
                }
                catch (AmazonSQSException)
                {
                    var timeSleep = new Random().Next(maxBackOff - minBackOff) + minBackOff;
                    var timeSleepMilliseconds = (int)TimeSpan.FromSeconds(timeSleep).TotalMilliseconds;
                    Task.Delay(timeSleepMilliseconds).Wait();

                    if (i == numRetries - 1)
                    {
                        throw;
                    }
                }
            }
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
                        if (delayTime < 100) delayTime += 4;
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

        private Task StartMessageReceiverInternal(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll,
            Func<string, Task<bool>> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            if (maxNumberOfMessagesPerPoll > 10 || maxNumberOfMessagesPerPoll < 1)
            {
                throw new ArgumentException("Value must be between 1 and 10", nameof(maxNumberOfMessagesPerPoll));
            }

            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var receiveMessageResponse = await ReceiveMessageAsync(queueName, waitTimeSeconds: pollWaitTimeSeconds,
                        maxNumberOfMessages: maxNumberOfMessagesPerPoll);

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

        private Task StartMessageReceiverInternal(string queueName, int pollWaitTimeSeconds, int maxNumberOfMessagesPerPoll,
            Func<ISQSMessage, Task> asyncMessageProcessor, CancellationToken cancellationToken)
        {
            if (maxNumberOfMessagesPerPoll > 10 || maxNumberOfMessagesPerPoll < 1)
            {
                throw new ArgumentException("Value must be between 1 and 10", nameof(maxNumberOfMessagesPerPoll));
            }

            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var messageAttributesQuery = new List<string> { "All" };
                    var receiveMessageResponse = await ReceiveMessageAsync(queueName, waitTimeSeconds: pollWaitTimeSeconds,
                        maxNumberOfMessages: maxNumberOfMessagesPerPoll, messageAttributeNames: messageAttributesQuery);

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