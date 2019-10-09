using Amazon.SQS.Model;
using System;
using System.Linq;
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
        public async Task CreateFifoQueueAsync_ShouldCreateFifoQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            var queueUrl = await client.CreateFifoQueueAsync(queueName);
            Assert.NotEmpty(queueUrl);

            await client.DeleteQueueAsync(queueName);
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
        public async Task SendMessageFifoAsync_ShouldPutMessageOnQueue()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}.fifo";
            await client.CreateFifoQueueAsync(queueName);

            var message = "Hello World!";
            var messageId = await client.SendMessageAsync(message, queueName);

            Assert.NotNull(messageId);

            await client.DeleteQueueAsync(queueName);
        }

        private bool MessagePicked { get; set; }

        [Fact]
        public async Task PollQueueAsync_ShouldPickMessageFromQueue_GivenSynchronousMethod()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var message = "Hello World!";
            await client.SendMessageAsync(message, queueName);

            MessagePicked = false;
            var cancellationToken = client.PollQueueAsync(queueName, 1, 1, (string receivedMessage) =>
            {
                Assert.Equal("Hello World!", receivedMessage);
                MessagePicked = true;
                return true;
            });

            Task.Delay(1000).Wait();
            cancellationToken.Cancel();

            Assert.True(MessagePicked);
            MessagePicked = false;

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task PollQueueAsync_ShouldPickMessageFromQueue_GivenAsynchronousMethod()
        {
            var client = CreateSQSClient();
            var queueName = $"{Guid.NewGuid().ToString()}";
            await client.CreateStandardQueueAsync(queueName);

            var message = "Hello World!";
            await client.SendMessageAsync(message, queueName);

            MessagePicked = false;

            var cancellationToken = client.PollQueueAsync(queueName, 1, 1, async (string receivedMessage) =>
            {
                Assert.Equal("Hello World!", receivedMessage);
                MessagePicked = true;
                return await Task.FromResult(true);
            });

            Task.Delay(1000).Wait();
            cancellationToken.Cancel();

            Assert.True(MessagePicked);
            MessagePicked = false;

            await client.DeleteQueueAsync(queueName);
        }

        [Fact]
        public async Task PollQueueWithRetryAsync_ShouldThrowErrorWithAsyncMethod_IfQueueDoesNotExist()
        {
            var queueName = $"{Guid.NewGuid().ToString()}";
            var client = CreateSQSClient();

            await Assert.ThrowsAsync<QueueDoesNotExistException>(() => client.PollQueueWithRetryAsync(queueName, 1, 1, 2, 1, 10, async (string message) =>
             {
                 Assert.Equal("Hello World!", message);
                 return await Task.FromResult(true);
             }));
        }

        [Fact]
        public async Task PollQueueWithRetryAsync_ShouldThrowError_IfQueueDoesNotExist()
        {
            var queueName = $"{Guid.NewGuid().ToString()}";
            var client = CreateSQSClient();

            await Assert.ThrowsAsync<QueueDoesNotExistException>(() => client.PollQueueWithRetryAsync(queueName, 1, 1, 2, 1, 10, (string message) =>
            {
                Assert.Equal("Hello World!", message);
                return true;
            }));
        }

        private SQSClient CreateSQSClient()
        {
            return new SQSClient("http://localhost:9324", null, "mock", "mock");
        }
    }
}
