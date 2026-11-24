using HitlSendReply.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddHttpForwarder();

var app = builder.Build();

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

// The browser only talks to this server. Every /api call is forwarded to the Agent API.
// BFF later: add the user's access token here (a request transform on this forwarder) and the
// browser never holds it. Nothing else in the client or the API changes.
var agentApi = app.Configuration["AgentApi:BaseUrl"]
    ?? throw new InvalidOperationException("AgentApi:BaseUrl is not configured.");
app.MapForwarder("/api/{**catch-all}", agentApi);

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HitlSendReply.Web.Client._Imports).Assembly);

app.Run();
