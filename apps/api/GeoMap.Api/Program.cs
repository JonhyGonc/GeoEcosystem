using GeoMap.Api;
using GeoMap.Api.FeatureDetails;
using GeoMap.Api.Networks;
using GeoMap.Api.Projects;
using GeoMap.Api.SpatialQueries;
using GeoMap.Api.VectorTiles;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Database:ConnectionString must be provided through configuration.")
    .ValidateOnStart();

builder.Services.AddSingleton<NpgsqlDataSource>(serviceProvider =>
{
    var databaseOptions = serviceProvider
        .GetRequiredService<IOptions<DatabaseOptions>>()
        .Value;

    return NpgsqlDataSource.Create(databaseOptions.ConnectionString);
});
builder.Services.AddScoped<SpatialFeatureQuery>();
builder.Services.AddScoped<VectorTileQuery>();
builder.Services.AddScoped<ProjectCatalogQuery>();
builder.Services.AddScoped<NetworkCatalogQuery>();
builder.Services.AddScoped<FeatureDetailsQuery>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "GeoMap API",
    status = "ready"
}));

app.MapHealthChecks("/health");
app.MapSpatialQueryEndpoints();
app.MapVectorTileEndpoints();
app.MapProjectCatalogEndpoints();
app.MapNetworkCatalogEndpoints();
app.MapFeatureDetailsEndpoints();

app.Run();

public partial class Program
{
}
