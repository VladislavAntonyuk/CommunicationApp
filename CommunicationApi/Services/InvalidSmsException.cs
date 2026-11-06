namespace CommunicationApi.Services;

public sealed class InvalidSmsException(string message) : PermanentSendException(message);
