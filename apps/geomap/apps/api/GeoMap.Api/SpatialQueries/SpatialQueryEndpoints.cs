using Microsoft.AspNetCore.Mvc;

namespace GeoMap.Api.SpatialQueries;

public static class SpatialQueryEndpoints
{
    public static IEndpointRouteBuilder MapSpatialQueryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/projects/{projectId:guid}/networks/{networkId:guid}/features",
                QueryFeaturesAsync)
            .WithName("QuerySpatialFeatures")
            .WithSummary("Query network features that intersect a required EPSG:4326 bounding box.")
            .Produces<SpatialFeatureQueryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> QueryFeaturesAsync(
        Guid projectId,
        Guid networkId,
        [AsParameters] SpatialQueryParameters parameters,
        SpatialFeatureQuery query,
        CancellationToken cancellationToken)
    {
        var validation = parameters.Validate();
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.Errors);
        }

        var response = await query.ExecuteAsync(
            projectId,
            networkId,
            validation.Request!,
            cancellationToken);

        return Results.Ok(response);
    }
}
