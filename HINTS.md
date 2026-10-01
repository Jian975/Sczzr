# Concepts Reference

Quick reference for the key concepts you'll need. Check the Microsoft docs for full API details.

---

## How the Starter Code Fits Together

The starter code gives you `Server` and `Client` classes with empty methods. You fill them in.

```
┌─────────────────┐                    ┌─────────────────────┐
│     Server      │◄─── connection ────│       Client        │
│                 │                    │                     │
│  /listen 5000   │                    │  /connect host port │
│                 │                    │                     │
│  You implement: │                    │  You implement:     │
│  - Start()      │                    │  - ConnectAsync()   │
│  - AcceptClients│                    │  - ReceiveAsync()   │
│  - ReceiveFrom..│                    │  - Send()           │
└─────────────────┘                    └─────────────────────┘
```

In Program.cs, you create instances of both, subscribe to their events, and wire up the input loop. When the user types
`/listen`, call `_server.Start(port)`. When they type `/connect`, call `_client.ConnectAsync(host, port)`. Events fire
automatically as things happen on the network.

**Test with three terminals:**

- Terminal 1: `/listen 5000` (the server, just relays messages)
- Terminal 2: `/connect 127.0.0.1 5000` (client)
- Terminal 3: `/connect 127.0.0.1 5000` (another client)

Clients 2 and 3 chat through server 1.

**Note:** nothing in `Server`/`Client` stops one process from calling both `Start()` and `ConnectAsync()` — they're
independent objects, neither aware of the other. For Sprint 1 and 2 don't do this: one process should be the relay
(`/listen` only), everyone else connects to it (`/connect` only). That's a project requirement, not a code restriction —
the code would let you do it. Sprint 3 flips this: a peer is exactly a node doing both at once.

**Wiring up events:**

```csharp
_server = new Server();
_client = new Client();

// These fire when things happen on the network
_server.OnClientConnected += (endpoint) => { /* someone connected */ };
_server.OnClientDisconnected += (endpoint) => { /* someone left */ };
_server.OnMessageReceived += (endpoint, message) => { /* relay it with Broadcast(), or
                                                          SendTo(endpoint, message) if it
                                                          should only go to one client */ };

_client.OnConnected += (endpoint) => { /* we connected to a server */ };
_client.OnDisconnected += (endpoint) => { /* lost connection */ };
_client.OnMessageReceived += (message) => { /* show the message */ };
```

`Server.OnMessageReceived` hands you the sender's endpoint along with the message - you need it
to know who to reply to, or who to exclude from a broadcast. `Client` only ever talks to one
thing (whatever it connected to), so it doesn't need that.

---

## Events and Actions

An `Action<T>` is just a reference to a method. Events let one class notify another when something happens.

**Declare it** (inside the class):

```csharp
public event Action<string>? OnSomething;
```

**Fire it** (inside the class):

```csharp
OnSomething?.Invoke("data");
```

**Subscribe** (from outside):

```csharp
obj.OnSomething += (data) => { /* handle it */ };
```

The `?.` is important - if nobody subscribed, the event is null, and calling Invoke on null would crash.

You can subscribe multiple handlers with `+=`. They all get called when the event fires.

---

## BlockingCollection<T>

A thread-safe queue where `Take()` blocks (waits) until something is available. Good for producer/consumer patterns.

- `Add(item)` - puts an item in, never blocks
- `Take()` - gets an item, blocks if the queue is empty
- `Take(token)` - same but respects cancellation
- `CompleteAdding()` - signals that nothing more will be added; unblocks waiting consumers

Without blocking, a consumer thread would just spin in a tight loop burning CPU while waiting for data.

**Note:** You don't need MessageQueue for Sprint 1. Handling messages directly in your event handlers is simpler and
works fine.

---

## Threads and Tasks

Starting background work:

```csharp
// Option A: explicit thread
var thread = new Thread(MethodName);
thread.IsBackground = true;  // won't prevent app from exiting
thread.Start();

// Option B: task (easier with async/await)
_ = Task.Run(() => DoWork());
```

Cancellation pattern - check this in your loops:

```csharp
while (!_cancellationTokenSource.IsCancellationRequested)
{
    // do work
}
```

---

## Locking

When multiple threads touch the same data, use `lock`:

```csharp
private readonly object _clientsLock = new();
private readonly List<TcpClient> _clients = new();

lock (_clientsLock)
{
    _clients.Add(client);
}
```

A few rules:

- Always use the **same lock object** for the same data
- If you need to return the list contents, return a copy: `_clients.ToList()`
- Don't hold a lock while doing slow stuff like network I/O

---

## TCP Basics

**Server side (TcpListener):**

1. Create a listener on a port
2. `Start()` it
3. `AcceptTcpClientAsync()` to wait for someone to connect
4. Get a `NetworkStream` from the client, read/write bytes on it

**Client side (TcpClient):**

1. Create a TcpClient
2. `ConnectAsync(host, port)`
3. `GetStream()` gives you the NetworkStream
4. Read/write bytes

**Length-prefix framing:**

TCP is a byte stream - it doesn't know where one message ends and the next begins. We solve this by sending the message
length first:

```
┌─────────────┬────────────────────────────┐
│ 4 bytes     │ N bytes                    │
│ (length N)  │ (JSON payload)             │
└─────────────┴────────────────────────────┘
```

Sending:

```csharp
var json = JsonSerializer.Serialize(message);
var payload = Encoding.UTF8.GetBytes(json);
var lengthPrefix = BitConverter.GetBytes(payload.Length);

stream.Write(lengthPrefix, 0, 4);
stream.Write(payload, 0, payload.Length);
```

Receiving:

```csharp
var lengthBuffer = new byte[4];
await stream.ReadAsync(lengthBuffer, 0, 4);
var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

var messageBuffer = new byte[messageLength];
// May need multiple reads - see below
await stream.ReadAsync(messageBuffer, 0, messageLength);

var json = Encoding.UTF8.GetString(messageBuffer);
var message = JsonSerializer.Deserialize<Message>(json);
```

---

## Watch Out For

1. **Null events** - Always use `?.Invoke()`, not just `Invoke()`. If nobody subscribed, the event is null.

2. **Returning your internal list** - Return `_clients.ToList()` (a copy), not the list itself. Otherwise another thread
   could modify it while you're iterating.

3. **Blocking the main thread** - Network code needs to run on background threads. The main thread should only handle
   console input.

4. **Closed connections** - If `ReadAsync` returns 0 bytes, the other side disconnected. Handle it.

5. **Race conditions** - If two threads touch the same data, use `lock` or a concurrent collection.

6. **Partial reads** - `ReadAsync` might not return all the bytes you asked for in one call. You need to loop until
   you've read the full expected length.
