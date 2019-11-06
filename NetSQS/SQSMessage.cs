using System.Threading.Tasks;

namespace NetSQS
{
    public class SQSMessage : ISQSMessage
    {
        /// <summary>
        /// Creates a new SQSMessage instance, used when explicit acknowledging of the message is needed.
        /// </summary>
        /// <param name="client">The SQS client instance</param>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="receiptHandle">The receipt handle of the message</param>
        public SQSMessage(SQSClient client, string queueName, string receiptHandle)
        {
            Client = client;
            QueueName = queueName;
            ReceiptHandle = receiptHandle;
        }

        /// <summary>
        /// The content of the message picked off the queue
        /// </summary>
        public string Body { get; set; }
        private SQSClient Client { get; }
        private string QueueName { get; }
        private string ReceiptHandle { get; }

        /// <summary>
        /// Deletes the message from the Queue when called.
        /// </summary>
        /// <returns></returns>
        public async Task Ack()
        {
            await Client.DeleteMessageAsync(QueueName, ReceiptHandle);
        }
    }

    public interface ISQSMessage
    {
        /// <summary>
        /// The content of the message picked off the queue
        /// </summary>
        string Body { get; set; }

        /// <summary>
        /// Deletes the message from the Queue when called.
        /// </summary>
        /// <returns></returns>
        Task Ack();
    }
}
