namespace LabExperiment.Domain.Notes;

// Sample feature: delete the Notes folders (Domain, Application, Infrastructure, Presentation, tests) when the experiment starts.
public sealed record Note(Guid Id, string Text, DateTimeOffset CreatedAt);
