using LabExperiment.Core.Echo;
using LabExperiment.Core.Results;
using LabExperiment.Core.Scripted;

// Composition only. Scripted model by default: runs with no key.
var assistant = new EchoAssistant(new ScriptedChatClient(["The scripted model answered."]));

Result<string> result = await assistant.AskAsync(args.Length > 0 ? string.Join(' ', args) : "Is the template wired?");

if (result.IsFailure)
{
    Console.Error.WriteLine($"{result.Error.Code}: {result.Error.Description}");
    return 1;
}

Console.WriteLine(result.Value);
return 0;
