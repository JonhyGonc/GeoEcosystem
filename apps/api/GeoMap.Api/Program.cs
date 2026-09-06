using GeoMap.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Database:ConnectionString must be provided through configuration.")
    .ValidateOnStart();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "GeoMap API",
    status = "ready"
}));

app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}
