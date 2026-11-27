using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

public sealed record GetRunQuery(string RunId) : IQuery<RunDto>;

internal sealed class GetRunQueryHandler(RunStore runs) : IQueryHandler<GetRunQuery, RunDto>
{
    public Task<Result<RunDto>> Handle(GetRunQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(runs.Find(query.RunId) is { } entry
            ? Result.Success(RunMapper.ToDto(entry))
            : Result.Failure<RunDto>(RunErrors.NotFound(query.RunId)));
}
