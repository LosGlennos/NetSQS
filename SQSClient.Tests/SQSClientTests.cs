using Amazon.SQS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NetSQS.Tests
{
    public class SQSClientTests
    {
        [Fact]
        public async Task CreateSQSClientAsync_ShouldConnect_IfCorrectlyConfigured()
        {
            var client = CreateSQSClient();
            var queues = await client.ListQueuesAsync();
            Assert.NotNull(queues);
        }

        [Fact]
        public async Task CreateStandardQueueAsync_ShouldCreateStandardQueue()
        {
            var client = CreateSQSClient();
            var queueName = Guid.NewGuid().ToString();
            var queueUrl = await client.CreateStandardQueueAsync(queueName);
            Assert.NotEmpty(queueUrl);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task CreateStandardFifoQueueAsync_ShouldCreateFifoQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            var queueUrl = await client.CreateStandardFifoQueueAsync(queueName);
            Assert.NotEmpty(queueUrl);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task CreateQueueAsync_ShouldError_IfNameContainsFifo()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            await Assert.ThrowsAsync<ArgumentException>(() => client.CreateQueueAsync(queueName, false, true));
        }

        [Fact]
        public async Task CreateQueueAsync_ShouldCreateFifoQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            var queueUrl = await client.CreateQueueAsync(queueName, true, true);
            Assert.NotEmpty(queueUrl);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task DeleteQueueAsync_ShouldDeleteQueue_IfNameExists()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var success = await client.DeleteQueueAsync(queueName);
            Assert.True(success);
        }

        [Fact]
        public async Task ListQueuesAsync_ShouldListNewlyCreatedQueue_WhenCreated()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var queues = await client.ListQueuesAsync();
            var queueExists = queues.Where(x => x.Contains(queueName));
            Assert.True(queueExists.Count() == 1);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task SendMessageAsync_ShouldPutMessageOnQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var message = "Hello World!";
            var messageId = await client.SendMessageAsync(message, queueName);

            Assert.NotNull(messageId);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task SendFifoMessageAsync_ShouldPutMessageOnQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            await client.CreateStandardFifoQueueAsync(queueName);

            var message = "Hello World!";
            var messageId = await client.SendMessageAsync(message, queueName);

            Assert.NotNull(messageId);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task SendBatchMessageAsync_ShouldPutMessageOnQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid()}";
            await client.CreateStandardQueueAsync(queueName);

            var batch = new List<BatchMessageRequest>
            {
                new BatchMessageRequest("testMessage1", new Dictionary<string, string>
                {
                    {"key1", "value1"}
                }),
                new BatchMessageRequest("testMessage2", new Dictionary<string, string>
                {
                    {"key2", "value2"}
                })
            }.ToArray();

            var response = await client.SendMessageBatchAsync(batch, queueName);

            Assert.Empty(response.GetFailed());
            Assert.Equal(2, response.GetSuccessful().Length);
            Assert.True(response.Success);

            Assert.True(response.SendResults[0].Success);
            Assert.Equal("testMessage1", response.SendResults[0].MessageRequest.Message);
            Assert.Equal("value1", response.SendResults[0].MessageRequest.MessageAttributes["key1"]);
            Assert.Null(response.SendResults[0].Error);

            Assert.True(response.SendResults[1].Success);
            Assert.Equal("testMessage2", response.SendResults[1].MessageRequest.Message);
            Assert.Equal("value2", response.SendResults[1].MessageRequest.MessageAttributes["key2"]);
            Assert.Null(response.SendResults[1].Error);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task SendBatchMessageAsyncWithNoAttributes_ShouldPutMessageOnQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid()}";
            await client.CreateStandardQueueAsync(queueName);

            var batch = new List<BatchMessageRequest>
            {
                new BatchMessageRequest("testMessage1"),
                new BatchMessageRequest("testMessage2", new Dictionary<string, string>
                {
                    {"key2", "value2"}
                })
            }.ToArray();

            var response = await client.SendMessageBatchAsync(batch, queueName);

            Assert.Empty(response.GetFailed());
            Assert.Equal(2, response.GetSuccessful().Length);
            Assert.True(response.Success);

            Assert.True(response.SendResults[0].Success);
            Assert.Equal("testMessage1", response.SendResults[0].MessageRequest.Message);
            Assert.Null(response.SendResults[0].Error);

            Assert.True(response.SendResults[1].Success);
            Assert.Equal("testMessage2", response.SendResults[1].MessageRequest.Message);
            Assert.Equal("value2", response.SendResults[1].MessageRequest.MessageAttributes["key2"]);
            Assert.Null(response.SendResults[1].Error);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task SendTooManyBatchMessageAsync_ShouldError()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid()}";
            await client.CreateStandardQueueAsync(queueName);

            var batch = new List<BatchMessageRequest>
            {
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
                new BatchMessageRequest("testMessage"),
            }.ToArray();

            await Assert.ThrowsAsync<ArgumentException>(() => client.SendMessageBatchAsync(batch, queueName));
        }

        [Fact]
        public async Task SendFifoBatchMessageAsync_ShouldPutMessageOnQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            await client.CreateStandardFifoQueueAsync(queueName);

            var batch = new List<BatchMessageRequest>
            {
                new BatchMessageRequest("testMessage1", new Dictionary<string, string>
                {
                    {"key1", "value1"}
                }),
                new BatchMessageRequest("testMessage2", new Dictionary<string, string>
                {
                    {"key2", "value2"}
                })
            }.ToArray();

            var response = await client.SendMessageBatchAsync(batch, queueName);

            Assert.Empty(response.GetFailed());
            Assert.Equal(2, response.GetSuccessful().Length);
            Assert.True(response.Success);

            Assert.True(response.SendResults[0].Success);
            Assert.Equal("testMessage1", response.SendResults[0].MessageRequest.Message);
            Assert.Equal("value1", response.SendResults[0].MessageRequest.MessageAttributes["key1"]);
            Assert.Null(response.SendResults[0].Error);

            Assert.True(response.SendResults[1].Success);
            Assert.Equal("testMessage2", response.SendResults[1].MessageRequest.Message);
            Assert.Equal("value2", response.SendResults[1].MessageRequest.MessageAttributes["key2"]);
            Assert.Null(response.SendResults[1].Error);

            await client.DeleteQueueAsync(queueName);
        }

        private bool MessagePicked { get; set; }

        [Fact]
        public async Task StartMessageReceiver_ShouldPickMessageFromQueue_GivenSynchronousMethod()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var message = "Hello World!";
            await client.SendMessageAsync(message, queueName);

            MessagePicked = false;
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

           _ = client.StartMessageReceiver(queueName, 1, 1, (string receivedMessage) =>
            {
                Assert.Equal("Hello World!", receivedMessage);
                MessagePicked = true;
                return true;
            }, cancellationToken);

            Task.Delay(1000).Wait();
            cancellationTokenSource.Cancel();

            Assert.True(MessagePicked);
            MessagePicked = false;

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task StartMessageReceiver_ShouldPickMessageFromQueue_GivenAsynchronousMethod()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var message = "Hello World!";
            await client.SendMessageAsync(message, queueName);

            MessagePicked = false;

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            _ = client.StartMessageReceiver(queueName, 1, 1, async (string receivedMessage) =>
            {
                Assert.Equal("Hello World!", receivedMessage);
                MessagePicked = true;
                return await Task.FromResult(true);
            }, cancellationToken);

            Task.Delay(1000).Wait();
            cancellationTokenSource.Cancel();

            Assert.True(MessagePicked);
            MessagePicked = false;

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task StartMessageReceiver_ShouldOnlyPickMessageTwoMessages_IfMessageIsAcked()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var firstMessage = "Foo";
            await client.SendMessageAsync(firstMessage, queueName);
            var secondMessage = "Bar";
            await client.SendMessageAsync(secondMessage, queueName);

            var numberOfPickedMessages = 0;

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;


            _ = client.StartMessageReceiver(queueName, 1, 1, async (ISQSMessage receivedMessage) =>
            {
                numberOfPickedMessages += 1;
                if (numberOfPickedMessages == 1)
                {
                    Assert.Equal("Foo", receivedMessage.Body);
                }
                else if (numberOfPickedMessages == 2)
                {
                    Assert.Equal("Bar", receivedMessage.Body);
                }
                await receivedMessage.Ack();
            }, cancellationToken);

            Task.Delay(5000).Wait();
            cancellationTokenSource.Cancel();

            Assert.Equal(2, numberOfPickedMessages);

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task StartMessageReceiver_ShouldGetMessageAttributes_WhenSupplied()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var firstMessage = "Foo";
            var messageAttributes = new Dictionary<string, string>
            {
                {"TestAttribute", "TestAttributeValue"}
            };

            await client.SendMessageAsync(firstMessage, queueName, messageAttributes);

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;


            _ = client.StartMessageReceiver(queueName, 1, 1, async (ISQSMessage receivedMessage) =>
            {
                Assert.Equal("Foo", receivedMessage.Body);

                var attributes = receivedMessage.MessageAttributes;

                var exists = attributes.TryGetValue("TestAttribute", out var attribute);
                Assert.Equal(attribute, "TestAttributeValue");
                Assert.True(exists);

                await receivedMessage.Ack();
            }, cancellationToken);

            Task.Delay(5000).Wait();
            cancellationTokenSource.Cancel();

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public void StartMessageReceiver_ShouldThrowErrorWithAsyncMethod_IfQueueDoesNotExist()
        {
            var queueName = $"{Guid.NewGuid().ToString()}";
            var client = CreateSQSClient();

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            Assert.ThrowsAsync<QueueDoesNotExistException>(() => client.StartMessageReceiver(queueName, 1, 1, 2, 1, 10, async (string message) =>
             {
                 Assert.Equal("Hello World!", message);
                 return await Task.FromResult(true);
             }, cancellationToken));
        }

        [Fact]
        public void StartMessageReceiver_ShouldThrowError_IfQueueDoesNotExist()
        {
            var queueName = $"{Guid.NewGuid().ToString()}";
            var client = CreateSQSClient();

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            Assert.ThrowsAsync<QueueDoesNotExistException>(() => client.StartMessageReceiver(queueName, 1, 1, 2, 1, 10, (string message) =>
            {
                Assert.Equal("Hello World!", message);
                return true;
            }, cancellationToken));
        }

        [Fact]
        public async Task GetNumberOfMessagesOnQueue_ShouldReturnApproximateTotalNumberOfMessages()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            await client.CreateStandardFifoQueueAsync(queueName);
            var message = "Hello World!";
            await client.SendMessageAsync(message, queueName);

            var actual = await client.GetNumberOfMessagesOnQueue(queueName);

            Assert.Equal(1, actual.ApproximateNumberOfMessages);

            await client.DeleteQueueAsync(queueName);
        }

        private SQSClient CreateSQSClient()
        {
            return new SQSClient("http://localhost:9324", null, "mock", "mock");
        }
    }
}
