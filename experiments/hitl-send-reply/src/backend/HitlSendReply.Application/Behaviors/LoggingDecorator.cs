using System.Diagnostics;
using HitlSendReply.Application.Messaging;
using HitlSendReply.Domain;
using Microsoft.Extensions.Logging;

namespace HitlSendReply.Application.Behaviors;

internal static class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string moduleName = GetModuleName(typeof(TCommand).FullName!);
            string commandName = typeof(TCommand).Name;

            Activity.Current?.SetTag("request.module", moduleName);
            Activity.Current?.SetTag("request.name", commandName);

            logger.LogInformation("Processing command {Command}", commandName);

            Result result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Completed command {Command}", commandName);
            }
            else
            {
                using (logger.BeginScope(new Dictionary<string, object> { ["Error"] = result.Error }))
                {
                    logger.LogError("Completed command {Command} with error", commandName);
                }
            }

            return result;
        }
    }

    internal sealed class CommandHandlerT<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandlerT<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string moduleName = GetModuleName(typeof(TCommand).FullName!);
            string commandName = typeof(TCommand).Name;

            Activity.Current?.SetTag("request.module", moduleName);
            Activity.Current?.SetTag("request.name", commandName);

            logger.LogInformation("Processing command {Command}", commandName);

            Result<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Completed command {Command}", commandName);
            }
            else
            {
                using (logger.BeginScope(new Dictionary<string, object> { ["Error"] = result.Error }))
                {
                    logger.LogError("Completed command {Command} with error", commandName);
                }
            }

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            string moduleName = GetModuleName(typeof(TQuery).FullName!);
            string queryName = typeof(TQuery).Name;

            Activity.Current?.SetTag("request.module", moduleName);
            Activity.Current?.SetTag("request.name", queryName);

            logger.LogInformation("Processing query {Query}", queryName);

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Completed query {Query}", queryName);
            }
            else
            {
                using (logger.BeginScope(new Dictionary<string, object> { ["Error"] = result.Error }))
                {
                    logger.LogError("Completed query {Query} with error", queryName);
                }
            }

            return result;
        }
    }

    // HitlSendReply.Application.<Area>.<Request>: the area (Runs, Tickets) is the tag.
    private static string GetModuleName(string requestName) =>
        requestName.Split('.') is { Length: > 3 } parts ? parts[2] : requestName;
}
