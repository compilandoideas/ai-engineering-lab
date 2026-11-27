using Microsoft.AspNetCore.Routing;

namespace LabExperiment.Presentation.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
