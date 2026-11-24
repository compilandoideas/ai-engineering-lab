using HitlSendReply.Web.Client.Api;
using HitlSendReply.Web.Client.Mailbox;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();

// Relative base address: the browser calls its own server, which forwards /api to the Agent API.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<AgentApiClient>();
builder.Services.AddScoped<MailboxClient>();

await builder.Build().RunAsync();
