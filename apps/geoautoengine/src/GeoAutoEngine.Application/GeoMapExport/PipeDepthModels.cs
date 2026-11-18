namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>
/// Ponto do levantamento lido de um bloco da layer <c>_TG</c> (normalmente <c>SIMB_PONTO</c>, com os atributos
/// PONTO, DESC e COTA). <paramref name="Node"/> é a posição onde o OSNAP NODE prende: o ponto (DBPoint) de dentro
/// do bloco, já na posição do desenho; sem ele, a inserção do bloco.
/// </summary>
/// <param name="NodeSource">De onde veio <paramref name="Node"/>: <see cref="SurveyNodeSources"/>.</param>
public sealed record SurveyPointInput(
    string Handle,
    string Name,
    string Description,
    string Elevation,
    GeoMapCoordinate Node,
    string NodeSource);

/// <summary>De onde veio a posição do ponto do levantamento.</summary>
public static class SurveyNodeSources
{
    /// <summary>O ponto (DBPoint) de dentro do bloco, onde o OSNAP NODE prende.</summary>
    public const string BlockPoint = "PONTO_DO_BLOCO";

    /// <summary>O bloco tem mais de um ponto (DBPoint); foi usado o primeiro.</summary>
    public const string FirstOfManyBlockPoints = "PRIMEIRO_PONTO_DO_BLOCO";

    /// <summary>O bloco não tem ponto (DBPoint); foi usada a inserção.</summary>
    public const string Insertion = "INSERCAO_DO_BLOCO";
}

/// <summary>
/// Limites da associação. As distâncias estão em metros; <see cref="DrawingUnitsPerMetre"/> converte para a
/// unidade do desenho. Os valores padrão são propostas (ver o marco das profundidades) até o usuário aprovar.
/// </summary>
/// <param name="ToleranceMetres">Distância máxima entre o ponto e a linha para associar (vértice ou trecho).</param>
/// <param name="NearRadiusMetres">Até essa distância o ponto é relatado como "próximo", mas não é associado.</param>
/// <param name="MaximumDepthMetres">Profundidade acima disso é tratada como inválida.</param>
public sealed record PipeDepthOptions(
    double ToleranceMetres = PipeDepthOptions.DefaultToleranceMetres,
    double NearRadiusMetres = PipeDepthOptions.DefaultNearRadiusMetres,
    double MaximumDepthMetres = PipeDepthOptions.DefaultMaximumDepthMetres,
    double DrawingUnitsPerMetre = 1)
{
    public const double DefaultToleranceMetres = 0.001;
    public const double DefaultNearRadiusMetres = 0.10;
    public const double DefaultMaximumDepthMetres = 10;

    private const double MetresPerFoot = 0.3048;
    private const double MetresPerUsSurveyFoot = 1200d / 3937d;

    /// <summary>Opções para a unidade do contrato GeoMap. Graus não servem: as distâncias precisam de metros.</summary>
    public static PipeDepthOptions ForUnits(string units) => units switch
    {
        "metre" => new PipeDepthOptions(),
        "foot" => new PipeDepthOptions(DrawingUnitsPerMetre: 1 / MetresPerFoot),
        "us-survey-foot" => new PipeDepthOptions(DrawingUnitsPerMetre: 1 / MetresPerUsSurveyFoot),
        _ => throw new InvalidOperationException(
            $"As profundidades precisam de um desenho em metros ou pés; a unidade \"{units}\" não serve."),
    };

    internal double Tolerance => ToleranceMetres * DrawingUnitsPerMetre;

    internal double NearRadius => NearRadiusMetres * DrawingUnitsPerMetre;

    internal void Validate()
    {
        if (!(ToleranceMetres > 0) || !(NearRadiusMetres >= ToleranceMetres) ||
            !(MaximumDepthMetres > 0) || !(DrawingUnitsPerMetre > 0) ||
            double.IsInfinity(NearRadiusMetres) || double.IsInfinity(DrawingUnitsPerMetre))
        {
            throw new InvalidOperationException("Opções de associação das profundidades inválidas.");
        }
    }
}

/// <summary>O que a descrição (DESC) de um ponto diz, segundo o interpretador de códigos do levantamento.</summary>
public enum SurveyDepthKind
{
    /// <summary>Informação da rede com profundidade (ex.: <c>AP1.20D300</c>): é a profundidade da tubulação.</summary>
    PipeDepth,

    /// <summary>Profundidade de uma caixa, PV ou outro objeto (ex.: <c>CXAP1.50</c>): não é da tubulação.</summary>
    ObjectDepth,

    /// <summary>Código conhecido, sem profundidade (ex.: <c>GUIA</c>, <c>CXAP</c>).</summary>
    NoDepth,

    /// <summary>Código que o interpretador não conhece.</summary>
    UnknownCode,

    /// <summary>Profundidade zero ou acima do máximo.</summary>
    InvalidDepth,

    /// <summary>DESC vazio.</summary>
    NoCode,

    /// <summary>Código que a análise ignora de propósito (ex.: <c>G</c>, <c>PT</c>): fora das contagens e do CSV.</summary>
    Ignored,
}

/// <param name="DepthMetres">Profundidade como está no código, em metros, sem conversão.</param>
/// <param name="Network">Rede segundo o interpretador (ex.: "Águas pluviais").</param>
public sealed record SurveyDepthReading(
    SurveyDepthKind Kind,
    double? DepthMetres,
    string? Network,
    double? DiameterMm,
    string? Material,
    string Rule,
    string NormalizedCode);

/// <summary>Estado de cada ponto no diagnóstico.</summary>
public static class PipeDepthPointStatus
{
    public const string Associated = "ASSOCIADO";
    public const string Shared = "ASSOCIADO_COMPARTILHADO";
    public const string NoPipe = "SEM_TUBULACAO";
    public const string NearNotAssociated = "PROXIMO_NAO_ASSOCIADO";

    /// <summary>
    /// Segundo ponto do par que a equipe de campo marca para sinalizar o caminho da rede: está ao lado de um ponto
    /// já associado à mesma linha, com o mesmo valor. Não é problema e não entra na média.
    /// </summary>
    public const string PairedSignal = "PAR_SINALIZACAO";
    public const string Ambiguous = "AMBIGUO";
    public const string NetworkConflict = "CONFLITO_REDE";
    public const string DiameterConflict = "CONFLITO_DIAMETRO";
    public const string PointConflict = "CONFLITO_PONTOS";
    public const string Duplicate = "DUPLICADO";
    public const string NoDepth = "SEM_PROFUNDIDADE";
    public const string ObjectDepth = "PROFUNDIDADE_DE_OBJETO";
    public const string UnknownCode = "CODIGO_DESCONHECIDO";
    public const string InvalidDepth = "PROFUNDIDADE_INVALIDA";
    public const string NoCode = "SEM_CODIGO";
    public const string Ignored = "IGNORADO";
}

/// <summary>Como a medição foi associada à linha.</summary>
public static class PipeDepthMethods
{
    /// <summary>O ponto está num vértice da linha (desenhada com o OSNAP NODE).</summary>
    public const string Vertex = "VERTICE";

    /// <summary>O ponto está sobre um trecho da linha, fora dos vértices.</summary>
    public const string Segment = "TRECHO";
}

/// <summary>Uma profundidade medida num ponto e associada a uma linha.</summary>
/// <param name="VertexIndex">Índice do vértice (a partir de 0) quando o ponto está num vértice.</param>
/// <param name="SegmentIndex">Trecho da linha (a partir de 0) onde o ponto está.</param>
/// <param name="Station">Distância ao longo da linha, desde o primeiro vértice, na unidade do desenho.</param>
/// <param name="Distance">Distância entre o ponto e a linha, na unidade do desenho.</param>
/// <param name="Shared">O ponto está na ponta de duas linhas que se continuam e vale para as duas.</param>
public sealed record PipeDepthMeasurement(
    string PointHandle,
    string PointName,
    double DepthMetres,
    int? VertexIndex,
    int SegmentIndex,
    double Station,
    double Distance,
    string OriginalCode,
    string Method,
    bool Shared);

/// <summary>As medições de uma linha, em ordem ao longo dela (no sentido em que foi desenhada).</summary>
public sealed record PipeDepthPipeResult(GeoMapExportFeature Pipe, IReadOnlyList<PipeDepthMeasurement> Measurements)
{
    /// <summary>
    /// A primeira profundidade registrada ao longo da linha, esteja ela em qualquer vértice ou trecho
    /// (pedido do usuário, 18/11/2026). Sem medição, <c>null</c>.
    /// </summary>
    public double? StartDepthMetres => Measurements.Count == 0 ? null : Measurements[0].DepthMetres;

    /// <summary>A última profundidade registrada ao longo da linha; com um ponto só, igual à inicial.</summary>
    public double? EndDepthMetres => Measurements.Count == 0 ? null : Measurements[Measurements.Count - 1].DepthMetres;
}

/// <summary>O resultado de um ponto, para o resumo e o diagnóstico.</summary>
/// <param name="PipeHandles">Linhas envolvidas: as associadas, as candidatas (ambíguo/conflito) ou a mais próxima.</param>
/// <param name="Distance">Distância até a linha mais próxima, na unidade do desenho, quando calculada.</param>
public sealed record PipeDepthPointResult(
    SurveyPointInput Point,
    SurveyDepthReading Reading,
    string Status,
    IReadOnlyList<string> PipeHandles,
    double? Distance,
    int? VertexIndex,
    string Observation);

public sealed record PipeDepthAnalysis(
    IReadOnlyList<PipeDepthPipeResult> Pipes,
    IReadOnlyList<PipeDepthPointResult> Points,
    PipeDepthOptions Options)
{
    private Dictionary<string, PipeDepthPipeResult>? _byHandle;

    public IReadOnlyDictionary<string, PipeDepthPipeResult> ByHandle =>
        _byHandle ??= Pipes.ToDictionary(pipe => pipe.Pipe.CadHandle, StringComparer.Ordinal);

    public int CountPoints(string status) => Points.Count(point => point.Status == status);

    public int PipeDepthPoints => Points.Count(point => point.Reading.Kind == SurveyDepthKind.PipeDepth);

    public int PipesWithMeasurements => Pipes.Count(pipe => pipe.Measurements.Count > 0);
}
