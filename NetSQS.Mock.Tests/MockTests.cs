using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Xunit;

namespace NetSQS.Mock.Tests
{
    public class MockTests
    {
        [Fact]
        public async Task Mock_ShouldListQueues_WhenClientAvailable()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            var queues = await client.ListQueuesAsync();
            Assert.NotNull(queues);
        }

        [Fact]
        public async Task MockCreateQueueAsync_ShouldReturnQueueUrl_WhenCreatingQueue()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            var queueUrl = await client.CreateQueueAsync("test", false, false);
            
            Assert.Equal("https://mockRegion/queue/test", queueUrl);
        }

        [Fact]
        public async Task MockCreateStandardQueueAsync_ShouldReturnQueueUrl_WhenCreatingStandardQueue()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            var queueUrl = await client.CreateStandardQueueAsync("test");

            Assert.Equal("https://mockRegion/queue/test", queueUrl);
        }

        [Fact]
        public async Task MockCreateFifoQueueAsync_ShouldReturnQueueUrl_WhenCreatingFifoQueue()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            var queueUrl = await client.CreateFifoQueueAsync("test.fifo");

            Assert.Equal("https://mockRegion/queue/test.fifo", queueUrl);
        }

        [Fact]
        public async Task MockCreateFifoQueueAsync_ShouldThrowArgumentException_WhenQueueNameDoesNotEndWithFifo()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");

            await Assert.ThrowsAsync<ArgumentException>(() => client.CreateFifoQueueAsync("test"));
        }

        [Fact]
        public async Task MockSendMessageAsync_ShouldPutAMessageOnTheQueue_WhenQueueExists()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            await client.CreateFifoQueueAsync("mockQueue.fifo");
            var messageId = await client.SendMessageAsync("Hello World!", "mockQueue.fifo");

            Assert.NotNull(messageId);
        }

        [Fact]
        public async Task MockSendMessageAsync_ShouldThrowQueueDoesNotExistException_WhenQueueDoesNotExist()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");

            await Assert.ThrowsAsync<QueueDoesNotExistException>(() => client.SendMessageAsync("test", "test"));
        }

        private bool _messagePicked;

        [Fact]
        public async Task MockPollQueueAsync_ShouldRetrieveMessage_WhenQueueAndMessageExists()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            await client.CreateFifoQueueAsync("mockQueue.fifo");
            await client.SendMessageAsync("Hello World!", "mockQueue.fifo");

            var cancellationToken = client.PollQueueAsync("mockQueue.fifo", 1, 1, message =>
            {
                Assert.Equal("Hello World!", message);
                _messagePicked = true;
                return true;
            });

            Task.Delay(1000).Wait();
            cancellationToken.Cancel();
            Assert.True(_messagePicked);
            _messagePicked = false;
        }

        [Fact]
        public async Task MockPollQueueAsync_ShouldRetrieveMessageWithAsyncProcessor_WhenQueueAndMessageExists()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            await client.CreateFifoQueueAsync("mockQueue.fifo");
            await client.SendMessageAsync("Hello World!", "mockQueue.fifo");

            var cancellationToken = client.PollQueueAsync("mockQueue.fifo", 1, 1, async (message) =>
            {
                Assert.Equal("Hello World!", message);
                _messagePicked = true;
                return await Task.FromResult(true);
            });

            Task.Delay(1000).Wait();
            cancellationToken.Cancel();
            Assert.True(_messagePicked);
            _messagePicked = false;
        }

        [Fact]
        public async Task MockPollQueueWithRetryAsync_ShouldRetrieveMessage_WhenQueueAndMessageExists()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            await client.CreateFifoQueueAsync("mockQueue.fifo");
            await client.SendMessageAsync("Hello World!", "mockQueue.fifo");

            var cancellationToken = await client.PollQueueWithRetryAsync("mockQueue.fifo", 1, 1, 10, 1, 10, message =>
            {
                Assert.Equal("Hello World!", message);
                _messagePicked = true;
                return true;
            });

            Task.Delay(1000).Wait();
            cancellationToken.Cancel();
            Assert.True(_messagePicked);
            _messagePicked = false;
        }

        [Fact]
        public async Task MockPollQueueWithRetryAsync_ShouldRetrieveMessageWithAsyncMessageProcessor_WhenQueueAndMessageExists()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            await client.CreateFifoQueueAsync("mockQueue.fifo");
            await client.SendMessageAsync("Hello World!", "mockQueue.fifo");

            var cancellationToken = await client.PollQueueWithRetryAsync("mockQueue.fifo", 1, 1, 10, 1, 10, async message =>
            {
                Assert.Equal("Hello World!", message);
                _messagePicked = true;
                return await Task.FromResult(true);
            });

            Task.Delay(1000).Wait();
            cancellationToken.Cancel();
            Assert.True(_messagePicked);
            _messagePicked = false;
        }

        [Fact]
        public async Task MockPollQueueWithRetryAsync_ShouldThrowQueueDoesNotExistException_WhenQueueDoesNotExist()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");

            await Assert.ThrowsAsync<QueueDoesNotExistException>(() => 
                client.PollQueueWithRetryAsync("mockQueue.fifo", 1, 1, 3, 1, 1, message => true));
        }

        [Fact]
        public async Task MockDeleteQueue_ShouldDeleteQueue_IfQueueExists()
        {
            var client = new SQSClientMock("mockEndpoint", "mockRegion");
            await client.CreateFifoQueueAsync("mockQueue.fifo");

            var queuesOnClientBeforeDeletion = await client.ListQueuesAsync();
            await client.DeleteQueueAsync("mockQueue.fifo");
            var queuesOnClientAfterDeletion = await client.ListQueuesAsync();

            Assert.Equal(1, queuesOnClientBeforeDeletion.Count);
            Assert.Equal(0, queuesOnClientAfterDeletion.Count);
        }
    }
}
