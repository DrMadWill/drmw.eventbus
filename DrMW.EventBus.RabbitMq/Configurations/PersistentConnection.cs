using DrMW.EventBus.RabbitMq.Errors;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DrMW.EventBus.RabbitMq.Configurations;

public class PersistentConnection : IDisposable
{
    /// <summary>
    /// Connection Factory
    /// </summary>
    private readonly ConnectionFactory _connectionFactory;
    /// <summary>
    /// Connection
    /// </summary>
    private IConnection _connection;
    /// <summary>
    /// Is Disposed ?
    /// </summary>
    private bool _disposed;
    /// <summary>
    /// Connection Retry Count
    /// </summary>
    private readonly int _tryCount;
    /// <summary>
    /// Lock Object
    /// </summary>
    private static readonly object LockObject = new object();
    /// <summary>
    /// Connection Try In Trying Time
    /// </summary>
    private bool _tryConnection;
    /// <summary>
    /// Timer For Check Connection
    /// </summary>
    private Timer _connectionCheckTimer;
    
    /// <summary>
    /// Singleton instance
    /// </summary>
    private static PersistentConnection? _instance;

    /// <summary>
    /// Public accessor for the singleton instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="tryCount"></param>
    /// <returns></returns>
    public static PersistentConnection Instance(ConnectionFactory connectionFactory, int tryCount = 5)
    {
        if (_instance != null) return _instance;
        lock (LockObject)
        {
            _instance ??= new PersistentConnection(connectionFactory, tryCount);
        }
        return _instance;
    }
    
    private PersistentConnection(ConnectionFactory connectionFactory, int tryCount)
    {
        _connectionFactory = connectionFactory;
        _tryCount = tryCount;
        
        // Initialize the timer to check the connection every 30 seconds (30000 milliseconds)
        _connectionCheckTimer = new Timer(CheckConnection, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }
    
    /// <summary>
    /// Check Every Time Connection
    /// </summary>
    /// <param name="state"></param>
    private void CheckConnection(object? state)
    {
        if (IsConnection || _tryConnection) return;
        _tryConnection = true;
        TryConnect().GetAwaiter().GetResult();
        _tryConnection = false;
    }
    
    /// <summary>
    /// Connection Is Open ?
    /// </summary>
    public bool IsConnection => _connection != null && _connection.IsOpen && !_disposed;

    /// <summary>
    /// Create Chanel
    /// </summary>
    /// <returns></returns>
    public async Task<IChannel> CreateChanel()
        => await _connection.CreateChannelAsync();

    public async Task<bool> TryConnect()
    {
        if (IsConnection) return true;
        lock (LockObject)
        {
            // var policy = Policy.Handle<SocketException>()
            //     .Or<BrokerUnreachableException>()
            //     .WaitAndRetry(_tryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            //         (ex, time) =>
            //         {
            //             Console.WriteLine("Rabbit MQ ==>>> : Connection failed");
            //             Console.WriteLine("Rabbit MQ ==>>> : connection exception | Event Bus TryConnect In RabbitMQ | err :  " + ex);
            //         });
            //
            // policy.Execute(() =>
            // {
            //      if (!IsConnection) _connection = _connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
            // });
            
            if (!IsConnection) _connection = _connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
            if (!IsConnection) 
                throw new CantConnectionError("Could not establish connection to RabbitMQ");
            
            _connection.ConnectionShutdown += Connection_ConnectionShutdown; 
            _connection.CallbackException += Connection_CallbackException; 
            _connection.ConnectionBlocked += Connection_ConnectionBlocked;

            return true;
        }
    }
    
    private void Connection_ConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        if(_disposed) return;
        TryConnect().GetAwaiter().GetResult();
    }

    private void Connection_CallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        if(_disposed) return;
        TryConnect().GetAwaiter().GetResult();
    }

    private void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        if(_disposed) return;
        TryConnect().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _disposed = true;
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}