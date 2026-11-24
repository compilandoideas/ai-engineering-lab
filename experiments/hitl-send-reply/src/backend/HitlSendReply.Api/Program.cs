using HitlSendReply.Api.Inbound;
using HitlSendReply.Api.Live;
using HitlSendReply.Api.Runs;
using HitlSendReply.Api.Scenarios;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<RunStore>();
builder.Services.AddSingleton<InboundEmailHandler>();
builder.Services.AddSingleton(_ => new LiveModels(LlmSettings.FromEnvironment()));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapCatalogEndpoints();
app.MapRunEndpoints();
app.MapInboundEmailEndpoints();

app.Run();
