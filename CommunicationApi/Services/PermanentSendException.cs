namespace CommunicationApi.Services;

public class PermanentSendException(string message, Exception? innerException = null) : Exception(message, innerException);
