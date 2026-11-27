using HitlSendReply.Application;
using HitlSendReply.Infrastructure;
using HitlSendReply.Presentation.Exceptions;

// Composition only: the use cases (Application), their adapters (Infrastructure) and the endpoints (Presentation).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication([typeof(ApplicationConfiguration).Assembly]);
builder.Services.AddInfrastructure();
builder.Services.AddEndpoints(typeof(RunEndpoints).Assembly);

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapEndpoints();

app.Run();
