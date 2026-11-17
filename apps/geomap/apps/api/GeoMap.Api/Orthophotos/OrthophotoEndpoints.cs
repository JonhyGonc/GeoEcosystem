namespace GeoMap.Api.Orthophotos;

/// <summary>Ortofoto do projeto (DEC-035): situação da camada e os tiles raster do MBTiles.</summary>
public static class OrthophotoEndpoints
{
    public const string CacheControl = "private, max-age=3600";
    public const int MaximumZoom = 24;

    public static IEndpointRouteBuilder MapOrthophotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/projects/{projectId:guid}/orthophoto", GetOrthophotoAsync)
            .WithName("GetProjectOrthophoto")
            .WithSummary("Tell whether the project has an orthophoto and how to draw it.")
            .Produces<OrthophotoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/projects/{projectId:guid}/orthophoto/tiles/{z:int}/{x:int}/{y:int}", GetTileAsync)
            .WithName("GetProjectOrthophotoTile")
            .WithSummary("Get one raster tile (XYZ) of the project orthophoto.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetOrthophotoAsync(
        Guid projectId,
        OrthophotoStore store,
        CancellationToken cancellationToken)
    {
        var lookup = await store.FindAsync(projectId, cancellationToken);
        if (lookup is null)
        {
            return Results.NotFound();
        }

        if (lookup.File is not { } file)
        {
            return Results.Ok(new OrthophotoResponse(false, lookup.ExpectedFileName, null, null, null, null, null, null));
        }

        return Results.Ok(new OrthophotoResponse(
            true,
            file.FileName,
            $"/api/projects/{projectId:D}/orthophoto/tiles/{{z}}/{{x}}/{{y}}",
            file.Format,
            file.Bounds,
            file.MinZoom,
            file.MaxZoom,
            file.Version));
    }

    private static async Task<IResult> GetTileAsync(
        Guid projectId,
        int z,
        int x,
        int y,
        HttpContext httpContext,
        OrthophotoStore store,
        CancellationToken cancellationToken)
    {
        if (z is < 0 or > MaximumZoom || x < 0 || y < 0 || x >= 1 << z || y >= 1 << z)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tile"] = [$"z must be between 0 and {MaximumZoom}, and x and y between 0 and 2^z - 1."],
            });
        }

        var lookup = await store.FindAsync(projectId, cancellationToken);
        if (lookup?.File is not { } file)
        {
            return Results.NotFound();
        }

        var tile = await OrthophotoStore.ReadTileAsync(file, z, x, y, cancellationToken);
        if (tile is null || tile.Length == 0)
        {
            return Results.NoContent();
        }

        httpContext.Response.Headers.CacheControl = CacheControl;
        return Results.Bytes(tile, OrthophotoStore.ContentType(file, tile));
    }
}

/// <summary>
/// <c>available</c> diz se o arquivo foi encontrado; <c>fileName</c> é o nome que o arquivo tem (ou deve ter) na
/// pasta das ortofotos; <c>bounds</c> = [oeste, sul, leste, norte] em graus; <c>version</c> muda quando o arquivo é
/// trocado, e o site a põe na URL dos tiles para o navegador não mostrar a ortofoto antiga guardada no cache.
/// </summary>
public sealed record OrthophotoResponse(
    bool Available,
    string FileName,
    string? TileUrl,
    string? Format,
    double[]? Bounds,
    int? MinZoom,
    int? MaxZoom,
    string? Version);
