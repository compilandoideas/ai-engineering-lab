namespace LabExperiment.Core.Results;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    Problem = 2,
    NotFound = 3,
    Conflict = 4,

    // Something the caller may see but not change. Answers 403.
    Forbidden = 5,

    // Well formed, but it breaks a rule. Answers 422.
    Unprocessable = 6
}
