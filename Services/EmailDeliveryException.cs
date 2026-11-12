namespace DemoMVC.Services;

public sealed class EmailDeliveryException(string message, Exception innerException)
    : Exception(message, innerException)
{
}
