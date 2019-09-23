# NetSQS

A small wrapper around the Amazon SQS SDK to simplify usage.

## Usage

### Connect
To connect to the queue you can either use the built-in `IServiceCollection` extension like this:

```
public void ConfigureServices(IServiceCollection services)
{
    services.AddSQSService("yourendpoint", "yourregion"); 
}
```

Or you can create it manually like this: `var client = new SQSClient("yourendpoint", "yourregion")`

If you want to get instant feedback on whether or not you are connected to the SQS endpoint. You can try and list the queues on the SQS by running: `await client.ListQueuesAsync()`.

### Creating a queue

You can create a default Standard queue like this:
```
var queueUrl = await client.CreateStandardQueueAsync("nameofthequeue");
```
Or create a default FIFO queue like this:
```
var queueUrl = await client.CreateFifoQueueAsync("nameofthequeue.fifo");
```
If you want to customize the queue, you can create a queue like this:
```
var queueUrl = await client.CreateQueueAsync(queueName: "nameofthequeue", isFifo: false, isEncrypted: false, retentionPeriod: 345600, visibilityTimeout: 30);
```

### Putting a message on the queue
```
var messageId = await client.SendMessageAsync("yourmessage", "nameofthequeue");
```

### Deleting a queue
```
var successfullyDeleted = await client.DeleteQueueAsync("nameofthequeue");
```

### Getting a message from the queue
To be able to get a message from SQS you need to poll the queue for new messages. You can use both sync and async methods as processors for the message returned from the queue.

```
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
    var task = client.PollQueueAsync("nameofthequeue", 0, 1, AddSomethingToDb);
    var task = client.PollQueueAsync("nameofthequeue", 0, 1, WriteToConsole);
}
```
You can also use `PollQueueWithRetryAsync` to automatically retry a connection to the queue if there has been an error while connecting. This is specified with a number of retries and a min and max backoff for the retry:
```
var task = PollQueueWithRetryAsync(queueName: "nameofthequeue", pollWaitTime: 0, maxNumberOfMessagesPerPoll: 1, numRetries: 20, minBackOff: 1, maxBackOff: 20, AddSomethingToDb);
```