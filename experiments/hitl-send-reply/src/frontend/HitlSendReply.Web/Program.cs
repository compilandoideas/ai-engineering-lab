using HitlSendReply.Web.Components;
using HitlSendReply.Web.Mailbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddHttpForwarderWithServiceDiscovery();

// The Agent API, found by service discovery ("https+http://api": the "api" resource in the AppHost).
var agentApi = builder.Configuration["AgentApi:BaseUrl"]
    ?? throw new InvalidOperationException("AgentApi:BaseUrl is not configured.");

// The simulated inbox: emails in Postgres (database "mailbox"), handed to the agent by webhook.
builder.AddNpgsqlDbContext<MailboxDbContext>("mailbox");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MailboxService>();
builder.Services.AddHttpClient<IRunStatusReader, RunStatusReader>(http => http.BaseAddress = new Uri(agentApi));
// Replaces the default resilience handler for this client only (the API is experimental in
// Microsoft.Extensions.Http.Resilience 10.x, hence the pragma).
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<IInboundEmailNotifier, WebhookInboundEmailNotifier>(http => http.BaseAddress = new Uri(agentApi))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        // Starting a run with a real model takes longer than the default 10 s attempt. One retry
        // (safe: the API is idempotent by EmailId) and then the email is kept as NotifyFailed.
        // The webhook waits for the run to start; a queue (or a 202 webhook) removes that wait.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.Retry.MaxRetryAttempts = 1;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(65);
    });
#pragma warning restore EXTEXP0001

var app = builder.Build();

// One table, migrated on start. A larger app would run migrations as their own step.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<MailboxDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.MapStaticAssets();

app.MapDefaultEndpoints();

// The browser only talks to this server. Every /api call is forwarded to the Agent API.
// BFF later: add the user's access token here (a request transform on this forwarder) and the
// browser never holds it. Nothing else in the client or the API changes.
app.MapForwarder("/api/{**catch-all}", agentApi);
app.MapMailboxEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HitlSendReply.Web.Client._Imports).Assembly);

app.Run();
