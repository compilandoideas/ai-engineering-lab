using LabExperiment.Application;
using LabExperiment.Infrastructure;
using LabExperiment.Presentation.Endpoints;
using LabExperiment.Presentation.Exceptions;

// Composition only: the use cases (Application), their adapters (Infrastructure) and the endpoints (Presentation).
var builder = WebApplication.CreateBuilder(args);

#if (aspire)
builder.AddServiceDefaults();

#endif
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication([typeof(ApplicationConfiguration).Assembly]);
builder.Services.AddInfrastructure();
builder.Services.AddEndpoints(typeof(IEndpoint).Assembly);

var app = builder.Build();

app.UseExceptionHandler();
#if (aspire)
app.MapDefaultEndpoints();
#endif
app.MapOpenApi();
app.MapEndpoints();

app.Run();
