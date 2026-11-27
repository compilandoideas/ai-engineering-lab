using Microsoft.AspNetCore.Routing;

namespace HitlSendReply.Presentation.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
