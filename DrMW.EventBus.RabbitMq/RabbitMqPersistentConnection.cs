using System.Net.Sockets;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace DrMW.EventBus.RabbitMq;

public class RabbitMqPersistentConnection : IDisposable
{
    private IConnection _connection;
    private readonly IConnectionFactory _connectionFactory;
    private readonly int _tryCount;
    private static readonly object _lockObject = new object();
    private bool _isDisposed = false;
    private bool _isLog = false;
    private Timer _connectionCheckTimer;
    public event EventHandler<string>? ConnectionStatusChanged;

    // Singleton instance
    private static RabbitMqPersistentConnection? _instance;

    // Public accessor for the singleton instance
    public static RabbitMqPersistentConnection Instance(IConnectionFactory connectionFactory, int tryCount = 5, bool isLog = false)
    {
        if (_instance != null) return _instance;
        lock (_lockObject)
        {
            if (_instance == null)
            {
                _instance = new RabbitMqPersistentConnection(connectionFactory, tryCount, isLog);
            }
        }
        return _instance;
    }

    // Private constructor to prevent direct instantiation
    private RabbitMqPersistentConnection(IConnectionFactory connectionFactory, int tryCount = 5, bool isLog = false)
    {
        _connectionFactory = connectionFactory;
        _tryCount = tryCount;
        _isLog = isLog;
        
        // Initialize the timer to check the connection every 10 seconds (10000 milliseconds)
        _connectionCheckTimer = new Timer(CheckConnection, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }
    
    // Check Connection method that will be called by the timer
    private void CheckConnection(object? state)
    {
        if (!IsConnection)
        {
            Log("Connection lost. Trying to reconnect...");
            TryConnect();
        }
    }
    
    // Connection Status 
    public bool IsConnection => _connection != null && _connection.IsOpen;
    
    

    public IModel CreateModel()
    {
        return _connection.CreateModel();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _connection.Dispose();
    }

    public bool TryConnect()
    {
        Log("TryConnect Stared");
        if (IsConnection)
        {
            
            Log("TryConnect ended","IsConnection success ");
            return true;
        }

        lock (_lockObject)
        {
            _connection?.Dispose();
            var policy = Policy.Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .WaitAndRetry(_tryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time) =>
                    {
                        Log("Connection failed", $"Exception: {ex.Message}");
                        Console.WriteLine("Rabbit MQ ==>>> : Connection failed");
                        Console.WriteLine("Rabbit MQ ==>>> : connection exception | Event Bus TryConnect In RabbitMQ | err :  " + ex);
                        ConnectionStatusChanged?.Invoke(this, "Connection failed");
                    });

            policy.Execute(() =>
            {
                _connection = _connectionFactory.CreateConnection();
            });
            if (!IsConnection)
            {
                Log("TryConnect : Connection failed");
                return false;
            }

            Console.WriteLine("Rabbit MQ ==>>> : Connection succeeded..."); 
            ConnectionStatusChanged?.Invoke(this, "Connection succeeded");
            _connection.ConnectionShutdown += Connection_ConnectionShutdown; 
            _connection.CallbackException += Connection_CallbackException; 
            _connection.ConnectionBlocked += Connection_ConnectionBlocked; 

            return true;
        }
    }

    // Remaining methods...

    private void Connection_ConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        Log("Connection Tried.Connection_ConnectionBlocked");
        if(_isDisposed) return;
        ConnectionStatusChanged?.Invoke(this, "Connection blocked");
        TryConnect();
    }

    private void Connection_CallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        Log("Connection Tried.Connection_CallbackException");
        if(_isDisposed) return;
        ConnectionStatusChanged?.Invoke(this, "Callback exception occurred");
        TryConnect();
    }

    private void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        Log("Connection Tried.Connection_ConnectionShutdown");
        if(_isDisposed) return;
        ConnectionStatusChanged?.Invoke(this, "Connection shutdown");
        TryConnect();
    }

    private void Log(params string[] logs)
    {
        if (_isLog)
        {
            Console.WriteLine("RabbitMqPersistentConnection >>>>>>>>>>>>> : " +string.Join(" | ",logs));
        }
    }
}