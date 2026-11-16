namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>
/// Troca os arcos por trechos retos para o contrato GeoMap, que só tem linhas retas (M4.3d).
/// A maior distância entre o arco verdadeiro e os trechos retos é de 5 mm (decisão do usuário, 16/11/2026).
/// </summary>
public static class GeoMapArcTessellation
{
    /// <summary>Maior distância entre o arco e a corda, em metros.</summary>
    public const double MaximumChordErrorMetres = 0.005;

    /// <summary>Abaixo disso o segmento é tratado como reto (o mesmo limite do leitor do AutoCAD).</summary>
    public const double StraightBulge = 1e-10;

    private const double MetresPerFoot = 0.3048;
    private const double MetresPerUsSurveyFoot = 1200.0 / 3937.0;
    private const double MetresPerDegree = 111_320.0;

    /// <summary>Os 5 mm na unidade do desenho.</summary>
    public static double ToleranceFor(string units) => units switch
    {
        "foot" => MaximumChordErrorMetres / MetresPerFoot,
        "us-survey-foot" => MaximumChordErrorMetres / MetresPerUsSurveyFoot,
        "degree" => MaximumChordErrorMetres / MetresPerDegree,
        _ => MaximumChordErrorMetres,
    };

    public static bool IsArc(double bulge) => Math.Abs(bulge) > StraightBulge;

    /// <summary>
    /// Pontos internos do arco entre <paramref name="start"/> e <paramref name="end"/>, sem as duas pontas.
    /// O bulge segue a convenção do AutoCAD: tangente de um quarto do ângulo do arco, positivo no sentido
    /// anti-horário.
    /// </summary>
    public static IReadOnlyList<GeoMapCoordinate> InteriorPoints(
        GeoMapCoordinate start,
        GeoMapCoordinate end,
        double bulge,
        double tolerance)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var chord = Math.Sqrt((dx * dx) + (dy * dy));
        if (!IsArc(bulge) || chord == 0 || tolerance <= 0)
        {
            return Array.Empty<GeoMapCoordinate>();
        }

        var sweep = 4 * Math.Atan(bulge);
        var radius = chord / (2 * Math.Sin(Math.Abs(sweep) / 2));

        // O centro fica à esquerda da corda quando o arco é anti-horário (bulge positivo).
        var offset = chord / 2 * (1 - (bulge * bulge)) / (2 * bulge);
        var centerX = ((start.X + end.X) / 2) - (dy / chord * offset);
        var centerY = ((start.Y + end.Y) / 2) + (dx / chord * offset);

        var step = tolerance >= radius ? Math.PI : 2 * Math.Acos(1 - (tolerance / radius));
        var count = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / step));
        var startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
        var points = new GeoMapCoordinate[count - 1];
        for (var index = 1; index < count; index++)
        {
            var angle = startAngle + (sweep * index / count);
            points[index - 1] = new GeoMapCoordinate(
                centerX + (radius * Math.Cos(angle)),
                centerY + (radius * Math.Sin(angle)));
        }

        return points;
    }

    /// <summary>
    /// Posições da linha com os arcos já trocados por trechos retos. O bulge do índice <c>i</c> vale para o
    /// segmento do vértice <c>i</c> ao seguinte; na linha fechada, o último segmento volta ao primeiro vértice.
    /// </summary>
    public static List<GeoMapCoordinate> Positions(
        IReadOnlyList<GeoMapCoordinate> vertices,
        IReadOnlyList<double>? bulges,
        bool closed,
        double tolerance)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        var positions = new List<GeoMapCoordinate>(vertices.Count + 1);
        var segments = closed ? vertices.Count : vertices.Count - 1;
        positions.Add(vertices[0]);
        for (var index = 0; index < segments; index++)
        {
            var end = vertices[(index + 1) % vertices.Count];
            if (bulges is not null)
            {
                positions.AddRange(InteriorPoints(vertices[index], end, bulges[index], tolerance));
            }

            if (index + 1 < vertices.Count || positions[positions.Count - 1] != end)
            {
                positions.Add(end);
            }
        }

        return positions;
    }

    /// <summary>Bulge de um ARC do AutoCAD: um quarto do ângulo varrido; negativo se a normal aponta para -Z.</summary>
    public static double ArcBulge(double startAngle, double endAngle, bool normalPointsDown)
    {
        var sweep = endAngle - startAngle;
        while (sweep <= 0)
        {
            sweep += 2 * Math.PI;
        }

        while (sweep > 2 * Math.PI)
        {
            sweep -= 2 * Math.PI;
        }

        var bulge = Math.Tan(sweep / 4);
        return normalPointsDown ? -bulge : bulge;
    }
}
