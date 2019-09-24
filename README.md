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
}

public void WriteToConsole(string message)
{
    Console.WriteLine(message);
}

public MessagePolling() 
{
    var fooCancellationToken = client.PollQueueAsync("nameofthequeue", 0, 1, AddSomethingToDb);
    var barCancellationToken = client.PollQueueAsync("nameofthequeue", 0, 1, WriteToConsole);
    
    // If you want to cancel the parallel tasks created by these methods. Do this:
    fooCancellationToken.Cancel();
    barCancellationToken.Cancel();
}
```
You can also use `PollQueueWithRetryAsync` to automatically retry a connection to the queue if there has been an error while connecting. This is specified with a number of retries and a min and max backoff for the retry:
```csharp
var task = PollQueueWithRetryAsync(queueName: "nameofthequeue", pollWaitTime: 0, maxNumberOfMessagesPerPoll: 1, numRetries: 20, minBackOff: 1, maxBackOff: 20, AddSomethingToDb);
```

## Contributing
If you want to contribute you need a running version of SQS in your local kubernetes cluster. You can start a local running instance by applying the yaml-file in `deployment_files/sqs.yaml`.
This will make testing easier, since you won't have to go towards a production environment.
