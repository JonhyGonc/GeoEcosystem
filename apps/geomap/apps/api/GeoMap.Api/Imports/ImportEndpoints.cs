using GeoMap.Contract;
using GeoMap.Importer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace GeoMap.Api.Imports;

/// <summary>
/// Importação de um <c>.geomap.json</c> pelo site (M4.5, DEC-033). Usa o mesmo caminho do importador do
/// terminal (<see cref="GeoMapDocumentImporter"/>): mesma validação, mesma reimportação sem duplicar.
/// A rota fica sempre ligada por decisão do usuário; a API não tem login, então ela não pode ir para um
/// servidor público sem antes ganhar autenticação.
/// </summary>
public static class ImportEndpoints
{
    public const string Route = "/api/imports";

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, ImportAsync)
            .WithName("ImportGeoMapDocument")
            .WithSummary("Import one .geomap.json document (the request body) and replace the network snapshot.")
            .Produces<ImportSummary>(StatusCodes.Status200OK)
            .Produces<ImportSummary>(StatusCodes.Status400BadRequest)
            .Produces<ImportSummary>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ImportSummary>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> ImportAsync(
        HttpContext context,
        IGeoMapImportStore store,
        IOptions<DatabaseOptions> databaseOptions,
        CancellationToken cancellationToken)
    {
        // O Kestrel limita o corpo a 30 MB por padrão; o contrato aceita até 50 MiB. Um byte a mais
        // permite responder com o mesmo erro do importador do terminal.
        var maximum = GeoMapContractValidator.MaximumDocumentBytes;
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeFeature)
        {
            sizeFeature.MaxRequestBodySize = maximum + 1L;
        }

        if (context.Request.ContentLength > maximum)
        {
            return TooLarge();
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximum)
            {
                return TooLarge();
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            return Results.BadRequest(Summary(
                new(ImportErrorCodes.FileInvalid, "$", "The request body is empty; send the .geomap.json file.")));
        }

        var outcome = await GeoMapDocumentImporter.ImportAsync(
            store,
            databaseOptions.Value.ConnectionString,
            buffer.ToArray(),
            cancellationToken);

        if (outcome.ExitCode == 0)
        {
            return Results.Ok(outcome.Summary);
        }

        // Problema no arquivo (contrato, CRS desconhecido no PostGIS) é erro de quem enviou; o resto é do servidor.
        return outcome.Summary.Errors.All(IsFileProblem)
            ? Results.BadRequest(outcome.Summary)
            : Results.Json(outcome.Summary, statusCode: StatusCodes.Status500InternalServerError);
    }

    private static bool IsFileProblem(ContractValidationError error) =>
        error.Code.StartsWith("GMC_", StringComparison.Ordinal) ||
        error.Code is ImportErrorCodes.CrsUnavailable or ImportErrorCodes.FileInvalid;

    private static IResult TooLarge() =>
        Results.Json(
            Summary(new(ContractErrorCodes.LimitExceeded, "$", "Document exceeds 50 MiB.")),
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static ImportSummary Summary(ContractValidationError error) =>
        new(false, 0, 0, 0, 0, null, null, 0, [error]);
}
