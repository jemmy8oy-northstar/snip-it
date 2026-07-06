using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Balenthiran.Snipit.WebApi.Routes;

public static class StatusRoutes
{
    public static RouteGroupBuilder MapStatusRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/status");

        group.MapGet("", GetStatusAsync).WithName("GetStatus");

        return parentGroup;
    }

    private static async Task<Ok<SystemStatusResponse>> GetStatusAsync(IStatusService statusService)
    {
        var status = await statusService.GetSystemStatusAsync();
        return TypedResults.Ok(new SystemStatusResponse(status.Version, status.GetFriendlyStatus(), status.LastUpdated));
    }
}
