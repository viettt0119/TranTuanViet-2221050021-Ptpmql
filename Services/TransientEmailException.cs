namespace DemoMVC.Services;

public sealed class TransientEmailException(string message, Exception innerException)
    : Exception(message, innerException)
{
}
