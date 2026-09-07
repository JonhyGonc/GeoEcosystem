namespace GeoMap.Api.VectorTiles;

public static class VectorTileEndpoints
{
    public const string ContentType = "application/vnd.mapbox-vector-tile";
    public const string CacheControl = "public, max-age=60";

    public static IEndpointRouteBuilder MapVectorTileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/projects/{projectId:guid}/networks/{networkId:guid}/tiles/{z:int}/{x:int}/{y:int}.mvt",
                GetTileAsync)
            .WithName("GetNetworkVectorTile")
            .WithSummary("Get a bounded Mapbox Vector Tile for one project and network.")
            .Produces(StatusCodes.Status200OK, contentType: ContentType)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetTileAsync(
        Guid projectId,
        Guid networkId,
        int z,
        int x,
        int y,
        HttpContext httpContext,
        VectorTileQuery query,
        CancellationToken cancellationToken)
    {
        var validation = VectorTileCoordinates.Validate(z, x, y);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.Errors);
        }

        var tile = await query.ExecuteAsync(
            projectId,
            networkId,
            validation.Coordinates!,
            cancellationToken);

        httpContext.Response.Headers.CacheControl = CacheControl;
        return tile.Length == 0
            ? Results.NoContent()
            : Results.File(tile, ContentType);
    }
}
