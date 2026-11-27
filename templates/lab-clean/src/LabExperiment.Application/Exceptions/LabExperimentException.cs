using LabExperiment.Domain;

namespace LabExperiment.Application.Exceptions;

public sealed class LabExperimentException : Exception
{
    public LabExperimentException(string requestName, Error? error = default, Exception? innerException = default)
        : base("Application exception", innerException)
    {
        RequestName = requestName;
        Error = error;
    }

    public string RequestName { get; }

    public Error? Error { get; }
}
