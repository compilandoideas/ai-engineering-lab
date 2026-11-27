using HitlSendReply.Domain;

namespace HitlSendReply.Application.Exceptions;

public sealed class HitlSendReplyException : Exception
{
    public HitlSendReplyException(string requestName, Error? error = default, Exception? innerException = default)
        : base("Application exception", innerException)
    {
        RequestName = requestName;
        Error = error;
    }

    public string RequestName { get; }

    public Error? Error { get; }
}
