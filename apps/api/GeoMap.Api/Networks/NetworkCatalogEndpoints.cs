namespace GeoMap.Api.Networks;

public static class NetworkCatalogEndpoints
{
    public static IEndpointRouteBuilder MapNetworkCatalogEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/projects/{projectId:guid}/networks",
                GetNetworksAsync)
            .WithName("GetProjectNetworks")
            .WithSummary("List the networks available to the selected project.")
            .Produces<NetworkCatalogResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetNetworksAsync(
        Guid projectId,
        NetworkCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var response = await query.ExecuteAsync(projectId, cancellationToken);
        return Results.Ok(response);
    }
}
