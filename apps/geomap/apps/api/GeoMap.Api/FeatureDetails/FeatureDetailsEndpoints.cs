namespace GeoMap.Api.FeatureDetails;

public static class FeatureDetailsEndpoints
{
    public static IEndpointRouteBuilder MapFeatureDetailsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/projects/{projectId:guid}/networks/{networkId:guid}/features/{featureId:guid}",
                GetFeatureDetailsAsync)
            .WithName("GetNetworkFeatureDetails")
            .WithSummary("Get the basic metadata and properties of one network feature.")
            .Produces<FeatureDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetFeatureDetailsAsync(
        Guid projectId,
        Guid networkId,
        Guid featureId,
        FeatureDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var response = await query.ExecuteAsync(
            projectId,
            networkId,
            featureId,
            cancellationToken);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }
}
