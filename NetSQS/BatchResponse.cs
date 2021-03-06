﻿using System.Linq;
using Amazon.SQS.Model;

namespace NetSQS
{
    /// <summary>
    /// The result from sending a batch of messages to a queue. 
    /// </summary>
    public class BatchResponse : IBatchResponse
    {
        /// <summary>
        /// Metadata on the success/failure of sending the message
        /// </summary>
        public class SendResult
        {
            public bool Success;
            public string MessageId;
            public BatchMessageRequest MessageRequest;
            public string Error;
        }

        /// <summary>
        /// True if all the messages in the batch were processed correctly
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// An array of message send results. Used to determine the status of sent messages
        /// </summary>
        public SendResult[] SendResults { get; set; }

        /// <summary>
        /// Returns an array of successfully sent messages
        /// </summary>
        /// <returns></returns>
        public SendResult[] GetSuccessful()
        {
            return SendResults.Where(x => x.Success).ToArray();
        }

        /// <summary>
        /// Returns an array of messages that failed to send
        /// </summary>
        /// <returns></returns>
        public SendResult[] GetFailed()
        {
            return SendResults.Where(x => !x.Success).ToArray();
        }

        /// <summary>
        /// Creates a BatchResponse from an AWS SendMessageBatchResponse type
        /// 
        /// This method creates an array of SendResults that mimics the original order of messages sent in
        /// </summary>
        /// <param name="batchResponse"></param>
        /// <param name="messages"></param>
        /// <param name="messageBatch"></param>
        /// <returns></returns>
        public static BatchResponse FromAwsBatchResponse(SendMessageBatchResponse batchResponse, BatchMessageRequest[] messageBatch)
        {
            var result = new BatchResponse
            {
                SendResults = new SendResult[batchResponse.Successful.Count + batchResponse.Failed.Count]
            };

            for (var messageId = 0; messageId < messageBatch.Length; messageId++)
            {
                var successSendResult = batchResponse.Successful.Where(x => x.Id == messageId.ToString()).Select(x => new SendResult
                {
                    Success = true,
                    MessageId = x.MessageId,
                    MessageRequest = messageBatch[messageId],
                    Error = null
                }).FirstOrDefault();

                var failedSendResult = batchResponse.Failed.Where(x => x.Id == messageId.ToString()).Select(x => new SendResult
                {
                    Success = false,
                    MessageId = null,
                    MessageRequest = messageBatch[messageId],
                    Error = x.Message,
                }).FirstOrDefault();

                result.SendResults[messageId] = successSendResult?.MessageId != null ? successSendResult : failedSendResult;
            }

            result.Success = !batchResponse.Failed.Any();
            return result;
        }
    }
}
