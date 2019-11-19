[![Build Status](https://dev.azure.com/martinhugosvensson/martinhugosvensson/_apis/build/status/LosGlennos.NetSQS?branchName=master)](https://dev.azure.com/martinhugosvensson/martinhugosvensson/_build/latest?definitionId=1&branchName=master) [![Build Status](https://img.shields.io/nuget/v/NetSQS)](https://img.shields.io/nuget/v/NetSQS)

# NetSQS

A small wrapper around the Amazon SQS SDK to simplify usage. A nod to https://github.com/ryannel/gosqs who made the go implementation of this wrapper first.

## Usage

### Connect
To connect to the queue you can either use the built-in `IServiceCollection` extension like this:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSQSService("yourendpoint", "yourregion", "awsAccessKeyId", "awsSecretAccessKey");
}
```

Or you can create it manually like this: `var client = new SQSClient("yourendpoint", "yourregion", "awsAccessKeyId", "awsSecretAccessKey")`

**Note**: AWS only cares about either the endpoint or the region. If you specify both, the region will overwrite the endpoint.
**Note 2**: `awsAccessKeyId` and `awsSecretAccessKey` are optional. If these are not specified, Amazon gets the credentials by using the  CredentialProfileStoreChain class. Read: https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html

If you want to get instant feedback on whether or not you are connected to the SQS endpoint. You can try and list the queues on the SQS by running: `await client.ListQueuesAsync()`.

### Creating a queue

You can create a default Standard queue like this:
```csharp
var queueUrl = await client.CreateStandardQueueAsync("nameofthequeue");
```
Or create a default FIFO queue like this:
```csharp
var queueUrl = await client.CreateFifoQueueAsync("nameofthequeue.fifo");
```
If you want to customize the queue, you can create a queue like this:
```csharp
var queueUrl = await client.CreateQueueAsync(queueName: "nameofthequeue", isFifo: false, isEncrypted: false, retentionPeriod: 345600, visibilityTimeout: 30);
```

### Putting a message on the queue
```csharp
var messageId = await client.SendMessageAsync("yourmessage", "nameofthequeue");
```

### Putting a batch of messages on the queue
```csharp
var batch = new BatchMessageRequest[2]
{
    new BatchMessageRequest("testMessage1", new Dictionary<string, string>
    {
        {"attrubuteName", "attributeValue"}
    }),
    new BatchMessageRequest("testMessage2", new Dictionary<string, string>
    {
        {"attrubuteName", "attributeValue"}
    })
};

var response =  await client.SendMessageBatchAsync(batch, "nameofthequeue");

if (!response.Success)
{
    var failed = response.GetFailed();
    # Do something with failed messages.
}

var sentMessageIds = response.GetSuccessful().Select(x => x.MessageId).ToArray();
```

### Deleting a queue
```csharp
var successfullyDeleted = await client.DeleteQueueAsync("nameofthequeue");
```

### Getting a message from the queue
To be able to get a message from SQS you need to poll the queue for new messages. You can use both sync and async methods as processors for the message returned from the queue.

```csharp
public async Task AddSomethingToDb(string message) 
{
    await myDbContext.Messages.Add(message);
    await myDbContext.SaveChangesAsync();
	return true;
}

public void WriteToConsole(string message)
{
    Console.WriteLine(message);
	return true;
}

public MessagePolling() 
{
	var  = new CancellationTokenSource();
	var cancellationToken = cancellationTokenSource.Token;

    client.StartMessageReceiver("nameofthequeue", 0, 1, AddSomethingToDb, cancellationToken);
    client.StartMessageReceiver("nameofthequeue", 0, 1, WriteToConsole, cancellationToken);
    
    // If you want to cancel the parallel tasks created by these methods. Do this:
    cancellationTokenSource.Cancel();
}
```
You can also use `StartMessageReceiver` to automatically retry a connection to the queue if there has been an error while connecting. This is specified with a number of retries and a min and max backoff for the retry:
```csharp
var task = StartMessageReceiver(queueName: "nameofthequeue", pollWaitTime: 0, maxNumberOfMessagesPerPoll: 1, numRetries: 20, minBackOff: 1, maxBackOff: 20, AddSomethingToDb, cancellationToken);
```

#### Explicit acking of messages from queue

If you need to explicitly acknowledge a message before a processor is finished you can do this:

```csharp
public Task MessageProcessor(ISQSMessage message) 
{
	var messageBody = message.Body;
	await myDbContext.Messages.Add(message);

	message.Ack();
}

public MessagePolling() 
{
	var  = new CancellationTokenSource();
	var cancellationToken = cancellationTokenSource.Token;

    client.StartMessageReceiver("nameofthequeue", 0, 1, MessageProcessor, cancellationToken);
    
    // If you want to cancel the parallel tasks created by these methods. Do this:
    cancellationTokenSource.Cancel();
}
```

This can be useful if you have long processing times and you want to make sure that the message is deleted before, for example, commiting a transaction.

## Local Testing
In order to test this library locally we recomending using the `alpine-sqs` docker image by executing the following in your terminal with docker installed: 

```
docker run --name alpine-sqs -p 9324:9324 -d roribio16/alpine-sqs:latest
```

## Contributing
We will gladly review pull requests.
