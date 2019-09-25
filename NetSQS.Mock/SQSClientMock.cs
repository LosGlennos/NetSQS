using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace NetSQS.Mock
{
    public class SQSClientMock : ISQSClient
    {
        private SQSClientMockObject MockClientObject { get; set; }


        public SQSClientMock(string mockEndpoint, string mockRegion)
        {
            MockClientObject = new SQSClientMockObject
            {
                Endpoint = mockEndpoint,
                Region = mockRegion,
                Queues = new Dictionary<string, Queue<string>>()
            };
        }

        public async Task<string> SendMessageAsync(string message, string queueName)
        {
            MockClientObject.Queues.TryGetValue(queueName, out var queue);
            if (queue == null)
            {
                throw new QueueDoesNotExistException($"Queue: {queueName} does not exist");
            }

            queue.Enqueue(message);
            MockClientObject.Queues[queueName] = queue;

            return "theMessageId";
        }

        public async Task<string> CreateStandardQueueAsync(string queueName)
        {
            return await CreateQueueAsync(queueName, false, false);
        }

        public async Task<string> CreateFifoQueueAsync(string queueName)
        {
            return await CreateQueueAsync(queueName, true, true);
        }

        public async Task<string> CreateQueueAsync(string queueName, bool isFifo, bool isEncrypted, int retentionPeriod = 345600,
            int visibilityTimeout = 30)
        {
            if (isFifo)
            {
                if (queueName.Length <= 5 || queueName.Substring(queueName.Length - 5) != ".fifo")
                {
                    throw new ArgumentException("Queue name must end with '.fifo'", nameof(queueName));
                }
            }

            MockClientObject.Queues.Add(queueName, new Queue<string>());
            return await Task.FromResult($"https://{MockClientObject.Region}/queue/{queueName}");
        }

        public Task<bool> DeleteQueueAsync(string queueName)
        {
            return Task.FromResult(MockClientObject.Queues.Remove(queueName));
        }

        public async Task<List<string>> ListQueuesAsync()
        {
            var queues = MockClientObject.Queues.Keys.Select(x => x.ToString()).ToList();
            return await Task.FromResult(queues);
        }

        public CancellationTokenSource PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            Func<string, Task<bool>> asyncMessageProcessor)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    MockClientObject.Queues.TryGetValue(queueName, out var queue);
                    if (queue == null)
                    {
                        throw new QueueDoesNotExistException($"Queue {queueName} does not exist.");
                    }

                    var message = queue.Dequeue();
                    await asyncMessageProcessor(message);
                }
            }, cancellationToken);

            return cancellationTokenSource;
        }

        public CancellationTokenSource PollQueueAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll,
            Func<string, bool> messageProcessor)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    MockClientObject.Queues.TryGetValue(queueName, out var queue);
                    if (queue == null)
                    {
                        throw new QueueDoesNotExistException($"Queue {queueName} does not exist.");
                    }

                    var message = queue.Dequeue();
                    messageProcessor(message);
                }
            }, cancellationToken);

            return cancellationTokenSource;
        }

        public async Task<CancellationTokenSource> PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, int numRetries,
            int minBackOff, int maxBackOff, Func<string, Task<bool>> asyncMessageProcessor)
        {
            WaitForQueue(queueName, numRetries, minBackOff, maxBackOff);

            return PollQueueAsync(queueName, pollWaitTime, maxNumberOfMessagesPerPoll, asyncMessageProcessor);
        }

        public async Task<CancellationTokenSource> PollQueueWithRetryAsync(string queueName, int pollWaitTime, int maxNumberOfMessagesPerPoll, int numRetries,
            int minBackOff, int maxBackOff, Func<string, bool> messageProcessor)
        {
            WaitForQueue(queueName, numRetries, minBackOff, maxBackOff);

            return PollQueueAsync(queueName, pollWaitTime, maxNumberOfMessagesPerPoll, messageProcessor);
        }

        private void WaitForQueue(string queueName, int numRetries, int minBackOff, int maxBackOff)
        {
            for (var i = 0; i < numRetries; i++)
            {
                MockClientObject.Queues.TryGetValue(queueName, out var queue);
                if (queue == null && i == numRetries - 1)
                {
                    throw new QueueDoesNotExistException($"Queue {queueName} does not exist");
                }

                if (queue != null)
                    break;

                var timeSleep = new Random().Next(maxBackOff - minBackOff) + minBackOff;
                var timeSleepMilliseconds = (int)TimeSpan.FromSeconds(timeSleep).TotalMilliseconds;
                Task.Delay(timeSleepMilliseconds).Wait();
            }
        }

        private class SQSClientMockObject
        {
            public string Endpoint { get; set; }
            public string Region { get; set; }
            public Dictionary<string, Queue<string>> Queues { get; set; }
        }
    }
}
