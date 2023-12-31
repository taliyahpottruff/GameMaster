namespace GameMaster.Bot.Services;

/// <summary>
/// Represents a result from executing a service method
/// </summary>
/// <typeparam name="T">The type of payload to be sent back</typeparam>
public struct ServiceResult<T>
{
    public bool Success { get; }
    public T Payload { get; }

    public ServiceResult(bool success, T payload)
    {
        Success = success;
        Payload = payload;
    }
}