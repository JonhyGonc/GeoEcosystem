namespace GeoMap.Api.Projects;

public static class ProjectCatalogEndpoints
{
    public static IEndpointRouteBuilder MapProjectCatalogEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/projects",
                GetProjectsAsync)
            .WithName("GetProjects")
            .WithSummary("List the projects available to open in the viewer.")
            .Produces<ProjectCatalogResponse>(StatusCodes.Status200OK);

        endpoints.MapGet(
                "/api/projects/{projectId:guid}/extent",
                GetProjectExtentAsync)
            .WithName("GetProjectExtent")
            .WithSummary("Bounding box (EPSG:4326) of every network feature in the project.")
            .Produces<ProjectExtentResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetProjectExtentAsync(
        Guid projectId,
        ProjectExtentQuery query,
        CancellationToken cancellationToken)
    {
        var response = await query.ExecuteAsync(projectId, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetProjectsAsync(
        ProjectCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var response = await query.ExecuteAsync(cancellationToken);
        return Results.Ok(response);
    }
}
