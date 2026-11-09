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

        return endpoints;
    }

    private static async Task<IResult> GetProjectsAsync(
        ProjectCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var response = await query.ExecuteAsync(cancellationToken);
        return Results.Ok(response);
    }
}
