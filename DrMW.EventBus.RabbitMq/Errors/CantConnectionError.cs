namespace DrMW.EventBus.RabbitMq.Errors;

public class CantConnectionError : Exception
{
    public CantConnectionError(string message) : base(message)
    {
        
    }
}