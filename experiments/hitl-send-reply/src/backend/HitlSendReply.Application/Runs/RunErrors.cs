namespace HitlSendReply.Application.Runs;

public static class RunErrors
{
    public static Error NotFound(string runId) => Error.NotFound("Runs.NotFound", $"Run '{runId}' does not exist.");

    public static Error Starting(string? startError) =>
        Error.Conflict("Runs.Starting", startError ?? "The run is still starting: nothing to review yet.");

    public static Error NoPendingReview(string callId) =>
        Error.Conflict("Runs.NoPendingReview", $"No pending review '{callId}' on this run.");

    // Nothing was sent and the review is still pending: the reviewer fixes the text and tries again.
    public static Error EditRefused(PolicyDecision decision) =>
        new($"Runs.EditRefused.{decision.Stage}", decision.Reason, ErrorType.Unprocessable);
}
