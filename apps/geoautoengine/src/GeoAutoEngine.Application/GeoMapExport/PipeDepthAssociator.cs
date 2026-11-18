using System.Globalization;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>
/// Associa as profundidades dos pontos do levantamento (<c>_TG</c>) às linhas das tubulações, sem a Autodesk.
/// Um ponto vale para uma linha quando está num vértice (desenhado com o OSNAP NODE) ou sobre um trecho dela, até a
/// tolerância. Nada é inventado: sem média, sem interpolação; cada medição guarda o ponto de origem.
/// Os casos duvidosos não são associados e vão para o diagnóstico (<see cref="PipeDepthPointStatus"/>).
/// Desempenho: os pontos ficam numa grade espacial; cada trecho só consulta as células por onde passa, então
/// o custo cresce com o comprimento das linhas e o número de pontos, e não com o produto dos dois.
/// </summary>
public static class PipeDepthAssociator
{
    // Pontos no mesmo lugar com profundidades que diferem até isto são o mesmo ponto repetido.
    private const double SameDepthMetres = 0.0005;
    private const double SameDiameterMm = 0.5;

    public static PipeDepthAnalysis Analyze(
        IReadOnlyList<GeoMapExportFeature> pipes,
        IReadOnlyList<SurveyPointInput> points,
        PipeDepthOptions options)
    {
        if (pipes is null)
        {
            throw new ArgumentNullException(nameof(pipes));
        }

        if (points is null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate();
        var lines = pipes.Where(pipe => pipe.Block is null && pipe.Vertices.Count >= 2).ToArray();
        var geometries = lines.Select(PipeGeometry.Create).ToArray();

        var classifier = new SurveyDepthClassifier();
        var readings = points.Select(point => classifier.Classify(point.Description, options.MaximumDepthMetres)).ToArray();
        var outcomes = new PointOutcome[points.Count];
        var depthPoints = new List<int>();
        for (var index = 0; index < points.Count; index++)
        {
            switch (readings[index].Kind)
            {
                case SurveyDepthKind.PipeDepth:
                    depthPoints.Add(index);
                    break;
                case SurveyDepthKind.ObjectDepth:
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.ObjectDepth,
                        "Profundidade de caixa, PV ou outro objeto; não é a profundidade da tubulação.");
                    break;
                case SurveyDepthKind.NoDepth:
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.NoDepth, string.Empty);
                    break;
                case SurveyDepthKind.UnknownCode:
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.UnknownCode,
                        "Código que o interpretador do levantamento não conhece.");
                    break;
                case SurveyDepthKind.Ignored:
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.Ignored, string.Empty);
                    break;
                case SurveyDepthKind.InvalidDepth:
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.InvalidDepth, string.Create(
                        CultureInfo.InvariantCulture,
                        $"Profundidade fora do intervalo aceito (maior que 0 e até {options.MaximumDepthMetres} m)."));
                    break;
                default:
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.NoCode, string.Empty);
                    break;
            }
        }

        var cellSize = Math.Max(2 * options.DrawingUnitsPerMetre, 4 * options.NearRadius);
        var grid = new PointGrid(cellSize);
        foreach (var index in depthPoints)
        {
            grid.Add(index, points[index].Node);
        }

        var active = ResolveSamePlacePoints(points, readings, depthPoints, grid, options, outcomes);

        // Para cada linha, só os pontos das células por onde ela passa são medidos.
        var hits = new Dictionary<int, List<PipeHit>>();
        var candidates = new HashSet<int>();
        for (var pipeIndex = 0; pipeIndex < geometries.Length; pipeIndex++)
        {
            candidates.Clear();
            geometries[pipeIndex].VisitCells(grid, candidates);
            foreach (var pointIndex in candidates)
            {
                if (!active[pointIndex])
                {
                    continue;
                }

                var hit = geometries[pipeIndex].Locate(points[pointIndex].Node, options.Tolerance);
                if (hit.Distance > options.NearRadius)
                {
                    continue;
                }

                if (!hits.TryGetValue(pointIndex, out var list))
                {
                    list = new List<PipeHit>();
                    hits[pointIndex] = list;
                }

                list.Add(hit with { PipeIndex = pipeIndex });
            }
        }

        var measurements = new List<PipeDepthMeasurement>[lines.Length];
        for (var index = 0; index < lines.Length; index++)
        {
            measurements[index] = new List<PipeDepthMeasurement>();
        }

        foreach (var pointIndex in depthPoints)
        {
            if (!active[pointIndex])
            {
                continue;
            }

            hits.TryGetValue(pointIndex, out var pointHits);
            outcomes[pointIndex] = Decide(
                points[pointIndex], readings[pointIndex], pointHits ?? new List<PipeHit>(), lines, options, measurements);
        }

        MarkSignalPairs(points, readings, depthPoints, grid, options, outcomes);

        var pipeResults = lines
            .Select((line, index) => new PipeDepthPipeResult(
                line,
                measurements[index]
                    .OrderBy(measurement => measurement.Station)
                    .ThenBy(measurement => measurement.PointName, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
        var pointResults = points
            .Select((point, index) => new PipeDepthPointResult(
                point,
                readings[index],
                outcomes[index].Status,
                outcomes[index].PipeHandles ?? Array.Empty<string>(),
                outcomes[index].Distance,
                outcomes[index].VertexIndex,
                outcomes[index].Observation))
            .ToArray();
        return new PipeDepthAnalysis(pipeResults, pointResults, options);
    }

    // Pontos com profundidade no mesmo lugar (até a tolerância): repetidos iguais viram um só; diferentes são
    // conflito e nenhum é associado. Retorna quais pontos seguem para a associação.
    private static bool[] ResolveSamePlacePoints(
        IReadOnlyList<SurveyPointInput> points,
        SurveyDepthReading[] readings,
        List<int> depthPoints,
        PointGrid grid,
        PipeDepthOptions options,
        PointOutcome[] outcomes)
    {
        var active = new bool[points.Count];
        var parent = new Dictionary<int, int>();
        foreach (var index in depthPoints)
        {
            active[index] = true;
            parent[index] = index;
        }

        foreach (var index in depthPoints)
        {
            foreach (var other in grid.Near(points[index].Node))
            {
                if (other > index && Distance(points[index].Node, points[other].Node) <= options.Tolerance)
                {
                    Union(parent, index, other);
                }
            }
        }

        var groups = depthPoints
            .GroupBy(index => Find(parent, index))
            .Where(group => group.Count() > 1);
        foreach (var group in groups)
        {
            var members = group.OrderBy(index => index).ToArray();
            var first = readings[members[0]];
            var same = members.All(index =>
                Math.Abs(readings[index].DepthMetres!.Value - first.DepthMetres!.Value) <= SameDepthMetres &&
                readings[index].Network == first.Network &&
                SameDiameter(readings[index].DiameterMm, first.DiameterMm));
            if (same)
            {
                foreach (var index in members.Skip(1))
                {
                    active[index] = false;
                    outcomes[index] = new PointOutcome(PipeDepthPointStatus.Duplicate,
                        $"Mesmo lugar e mesma profundidade do ponto {points[members[0]].Name}; vale uma vez só.");
                }

                continue;
            }

            var description = string.Join(", ", members.Select(index =>
                $"{points[index].Name} ({points[index].Description})"));
            foreach (var index in members)
            {
                active[index] = false;
                outcomes[index] = new PointOutcome(PipeDepthPointStatus.PointConflict,
                    $"Pontos diferentes no mesmo lugar: {description}. Nenhum foi associado.");
            }
        }

        return active;
    }

    // A equipe de campo marca dois pontos lado a lado (cerca de 5 cm) para sinalizar o caminho da rede, e o
    // desenho passa só por um deles (pedido do usuário, 18/11/2026). O ponto que ficou "perto" e tem, a até 10 cm,
    // um irmão já associado à mesma linha com o mesmo valor é o par: não é problema. Com outro valor, segue para
    // revisão, dizendo qual é o irmão.
    private static void MarkSignalPairs(
        IReadOnlyList<SurveyPointInput> points,
        SurveyDepthReading[] readings,
        List<int> depthPoints,
        PointGrid grid,
        PipeDepthOptions options,
        PointOutcome[] outcomes)
    {
        foreach (var index in depthPoints)
        {
            var outcome = outcomes[index];
            if (outcome.Status != PipeDepthPointStatus.NearNotAssociated || outcome.PipeHandles is not { Count: > 0 } near)
            {
                continue;
            }

            var twin = grid.Near(points[index].Node)
                .Where(other => other != index &&
                    outcomes[other].Status is PipeDepthPointStatus.Associated or PipeDepthPointStatus.Shared &&
                    outcomes[other].PipeHandles is { } handles && handles.Contains(near[0], StringComparer.Ordinal))
                .Select(other => (Index: other, Distance: Distance(points[index].Node, points[other].Node)))
                .Where(candidate => candidate.Distance <= options.NearRadius)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => (int?)candidate.Index)
                .FirstOrDefault();
            if (twin is not { } twinIndex)
            {
                continue;
            }

            var centimetres = Distance(points[index].Node, points[twinIndex].Node) / options.DrawingUnitsPerMetre * 100;
            var sameValue =
                Math.Abs(readings[index].DepthMetres!.Value - readings[twinIndex].DepthMetres!.Value) <= SameDepthMetres &&
                readings[index].Network == readings[twinIndex].Network;
            outcomes[index] = sameValue
                ? outcome with
                {
                    Status = PipeDepthPointStatus.PairedSignal,
                    Observation = string.Format(
                        CultureInfo.InvariantCulture,
                        "Par do ponto {0} ({1:0} cm ao lado); a profundidade já está na linha.",
                        points[twinIndex].Name,
                        centimetres),
                }
                : outcome with
                {
                    Observation = outcome.Observation + string.Format(
                        CultureInfo.InvariantCulture,
                        " Ao lado ({0:0} cm) do ponto {1}, associado com outro valor ({2}).",
                        centimetres,
                        points[twinIndex].Name,
                        points[twinIndex].Description),
                };
        }
    }

    private static PointOutcome Decide(
        SurveyPointInput point,
        SurveyDepthReading reading,
        List<PipeHit> hits,
        GeoMapExportFeature[] lines,
        PipeDepthOptions options,
        List<PipeDepthMeasurement>[] measurements)
    {
        var onLine = hits.Where(hit => hit.Distance <= options.Tolerance).ToList();
        if (onLine.Count == 0)
        {
            if (hits.Count == 0)
            {
                return new PointOutcome(PipeDepthPointStatus.NoPipe,
                    "Nenhuma linha selecionada passa pelo ponto.");
            }

            var nearest = hits.OrderBy(hit => hit.Distance).First();
            return new PointOutcome(
                PipeDepthPointStatus.NearNotAssociated,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "A linha mais próxima passa a {0:0.###} m; fora da tolerância, o ponto não foi associado.",
                    nearest.Distance / options.DrawingUnitsPerMetre),
                new[] { lines[nearest.PipeIndex].CadHandle },
                nearest.Distance,
                nearest.VertexIndex);
        }

        var handles = onLine.Select(hit => lines[hit.PipeIndex].CadHandle).ToArray();
        var compatible = onLine
            .Where(hit => SameNetwork(reading.Network, LayerNetwork(lines[hit.PipeIndex].SourceLayer)))
            .ToList();
        if (compatible.Count == 0)
        {
            return new PointOutcome(PipeDepthPointStatus.NetworkConflict,
                $"O ponto é de {reading.Network}, e a layer da linha é de outra rede.",
                handles, onLine.Min(hit => hit.Distance), null);
        }

        if (reading.DiameterMm is { } diameter)
        {
            var sameDiameter = compatible
                .Where(hit => LayerDiameter(lines[hit.PipeIndex]) is { } layerDiameter &&
                    Math.Abs(layerDiameter - diameter) <= SameDiameterMm)
                .ToList();
            var unknownDiameter = compatible.Where(hit => LayerDiameter(lines[hit.PipeIndex]) is null).ToList();
            if (sameDiameter.Count > 0)
            {
                compatible = sameDiameter;
            }
            else if (unknownDiameter.Count > 0)
            {
                compatible = unknownDiameter;
            }
            else
            {
                return new PointOutcome(PipeDepthPointStatus.DiameterConflict,
                    string.Create(CultureInfo.InvariantCulture,
                        $"O ponto diz D{diameter}, e a layer da linha tem outro diâmetro."),
                    handles, onLine.Min(hit => hit.Distance), null);
            }
        }

        if (compatible.Count == 1)
        {
            var hit = compatible[0];
            measurements[hit.PipeIndex].Add(Measurement(point, reading, hit, shared: false));
            return new PointOutcome(PipeDepthPointStatus.Associated, string.Empty,
                new[] { lines[hit.PipeIndex].CadHandle }, hit.Distance, hit.VertexIndex);
        }

        // Duas linhas que se continuam (uma termina onde a outra começa): o ponto vale para as duas.
        if (compatible.Count == 2 && compatible.All(hit => IsEndpoint(lines[hit.PipeIndex], hit)))
        {
            foreach (var hit in compatible)
            {
                measurements[hit.PipeIndex].Add(Measurement(point, reading, hit, shared: true));
            }

            return new PointOutcome(PipeDepthPointStatus.Shared,
                "Ponto na ponta de duas linhas que se continuam; vale para as duas.",
                compatible.Select(hit => lines[hit.PipeIndex].CadHandle).ToArray(), 0, null);
        }

        return new PointOutcome(PipeDepthPointStatus.Ambiguous,
            $"{compatible.Count} linhas passam pelo ponto; não dá para saber de qual é a profundidade.",
            compatible.Select(hit => lines[hit.PipeIndex].CadHandle).ToArray(),
            compatible.Min(hit => hit.Distance),
            null);
    }

    private static PipeDepthMeasurement Measurement(
        SurveyPointInput point,
        SurveyDepthReading reading,
        PipeHit hit,
        bool shared) => new(
        point.Handle,
        point.Name,
        reading.DepthMetres!.Value,
        hit.VertexIndex,
        hit.SegmentIndex,
        hit.Station,
        hit.Distance,
        point.Description.Trim(),
        hit.VertexIndex is null ? PipeDepthMethods.Segment : PipeDepthMethods.Vertex,
        shared);

    private static bool IsEndpoint(GeoMapExportFeature line, PipeHit hit) =>
        !line.IsClosed && hit.VertexIndex is { } vertex && (vertex == 0 || vertex == line.Vertices.Count - 1);

    private static string? LayerNetwork(string layer)
    {
        var definition = NetworkCadCatalog.All.FirstOrDefault(item =>
            item.CurveLayers.Contains(layer, StringComparer.OrdinalIgnoreCase));
        return definition is null || definition.NetworkId is "nao_identificada" or "outra"
            ? null
            : definition.DisplayName;
    }

    private static bool SameNetwork(string? pointNetwork, string? layerNetwork) =>
        pointNetwork is null or "Não identificada" or "Outra" ||
        layerNetwork is null ||
        string.Equals(pointNetwork, layerNetwork, StringComparison.Ordinal);

    private static double? LayerDiameter(GeoMapExportFeature line) =>
        GeoMapLayerNetworks.Attributes(line.SourceLayer).DiameterMm;

    private static bool SameDiameter(double? left, double? right) =>
        left is null ? right is null : right is not null && Math.Abs(left.Value - right.Value) <= SameDiameterMm;

    internal static double Distance(GeoMapCoordinate left, GeoMapCoordinate right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static int Find(Dictionary<int, int> parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(Dictionary<int, int> parent, int left, int right)
    {
        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);
        if (leftRoot != rightRoot)
        {
            parent[Math.Max(leftRoot, rightRoot)] = Math.Min(leftRoot, rightRoot);
        }
    }

    private sealed record PointOutcome(
        string Status,
        string Observation,
        IReadOnlyList<string>? PipeHandles = null,
        double? Distance = null,
        int? VertexIndex = null);

    internal readonly record struct PipeHit(
        int PipeIndex,
        double Distance,
        int? VertexIndex,
        int SegmentIndex,
        double Station);

    /// <summary>Grade espacial dos pontos: célula → índices dos pontos.</summary>
    internal sealed class PointGrid(double cellSize)
    {
        private readonly Dictionary<(long X, long Y), List<int>> _cells = new();

        public double CellSize { get; } = cellSize;

        public void Add(int index, GeoMapCoordinate position)
        {
            var key = Cell(position);
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<int>();
                _cells[key] = list;
            }

            list.Add(index);
        }

        /// <summary>Pontos da célula da posição e das oito vizinhas.</summary>
        public IEnumerable<int> Near(GeoMapCoordinate position)
        {
            var center = Cell(position);
            for (var dx = -1L; dx <= 1; dx++)
            {
                for (var dy = -1L; dy <= 1; dy++)
                {
                    if (_cells.TryGetValue((center.X + dx, center.Y + dy), out var list))
                    {
                        foreach (var index in list)
                        {
                            yield return index;
                        }
                    }
                }
            }
        }

        public void CollectNear(GeoMapCoordinate position, (long X, long Y)? previous, HashSet<int> into, out (long X, long Y) center)
        {
            center = Cell(position);
            if (previous == center)
            {
                return;
            }

            for (var dx = -1L; dx <= 1; dx++)
            {
                for (var dy = -1L; dy <= 1; dy++)
                {
                    if (_cells.TryGetValue((center.X + dx, center.Y + dy), out var list))
                    {
                        into.UnionWith(list);
                    }
                }
            }
        }

        private (long X, long Y) Cell(GeoMapCoordinate position) =>
            ((long)Math.Floor(position.X / CellSize), (long)Math.Floor(position.Y / CellSize));
    }

    /// <summary>Trechos de uma linha, retos ou em arco (bulge), com a distância acumulada desde o início.</summary>
    internal sealed class PipeGeometry
    {
        private readonly GeoMapCoordinate[] _vertices;
        private readonly double[] _vertexStations;
        private readonly Segment[] _segments;

        private PipeGeometry(GeoMapCoordinate[] vertices, double[] vertexStations, Segment[] segments)
        {
            _vertices = vertices;
            _vertexStations = vertexStations;
            _segments = segments;
        }

        public static PipeGeometry Create(GeoMapExportFeature line)
        {
            var vertices = line.Vertices.ToArray();
            var count = line.IsClosed ? vertices.Length : vertices.Length - 1;
            var segments = new Segment[count];
            var stations = new double[vertices.Length];
            var station = 0d;
            for (var index = 0; index < count; index++)
            {
                stations[index] = station;
                var bulge = line.Bulges is { } bulges && index < bulges.Count ? bulges[index] : 0;
                var segment = Segment.Create(index, vertices[index], vertices[(index + 1) % vertices.Length], bulge, station);
                segments[index] = segment;
                station += segment.Length;
            }

            if (!line.IsClosed)
            {
                stations[vertices.Length - 1] = station;
            }

            return new PipeGeometry(vertices, stations, segments);
        }

        /// <summary>Junta os pontos das células por onde a linha passa (com uma célula de folga para cada lado).</summary>
        public void VisitCells(PointGrid grid, HashSet<int> into)
        {
            (long X, long Y)? previous = null;
            var step = grid.CellSize / 2;
            foreach (var segment in _segments)
            {
                var samples = Math.Max(1, (int)Math.Ceiling(segment.Length / step));
                for (var sample = 0; sample <= samples; sample++)
                {
                    grid.CollectNear(segment.PointAt((double)sample / samples), previous, into, out var center);
                    previous = center;
                }
            }
        }

        /// <summary>Onde o ponto cai na linha: num vértice (até a tolerância) ou no trecho mais próximo.</summary>
        public PipeHit Locate(GeoMapCoordinate point, double tolerance)
        {
            int? vertex = null;
            var vertexDistance = double.MaxValue;
            for (var index = 0; index < _vertices.Length; index++)
            {
                var distance = Distance(point, _vertices[index]);
                if (distance <= tolerance && distance < vertexDistance)
                {
                    vertex = index;
                    vertexDistance = distance;
                }
            }

            if (vertex is { } vertexIndex)
            {
                var segmentIndex = Math.Min(vertexIndex, _segments.Length - 1);
                return new PipeHit(-1, vertexDistance, vertexIndex, segmentIndex, _vertexStations[vertexIndex]);
            }

            var best = new PipeHit(-1, double.MaxValue, null, 0, 0);
            foreach (var segment in _segments)
            {
                var (distance, station) = segment.Project(point);
                if (distance < best.Distance)
                {
                    best = new PipeHit(-1, distance, null, segment.Index, station);
                }
            }

            return best;
        }
    }

    internal sealed class Segment
    {
        private readonly GeoMapCoordinate _start;
        private readonly GeoMapCoordinate _end;
        private readonly double _startStation;

        // Arco: centro, raio, ângulo inicial e varredura com sinal (positivo = anti-horário).
        private readonly bool _isArc;
        private readonly GeoMapCoordinate _center;
        private readonly double _radius;
        private readonly double _startAngle;
        private readonly double _sweep;

        private Segment(
            int index,
            GeoMapCoordinate start,
            GeoMapCoordinate end,
            double startStation,
            double length,
            bool isArc,
            GeoMapCoordinate center,
            double radius,
            double startAngle,
            double sweep)
        {
            Index = index;
            _start = start;
            _end = end;
            _startStation = startStation;
            Length = length;
            _isArc = isArc;
            _center = center;
            _radius = radius;
            _startAngle = startAngle;
            _sweep = sweep;
        }

        public int Index { get; }

        public double Length { get; }

        public static Segment Create(int index, GeoMapCoordinate start, GeoMapCoordinate end, double bulge, double startStation)
        {
            var chord = Distance(start, end);
            if (!GeoMapArcTessellation.IsArc(bulge) || chord == 0)
            {
                return new Segment(index, start, end, startStation, chord, false, default, 0, 0, 0);
            }

            // Convenção do AutoCAD: bulge = tan(ângulo/4); o centro fica à esquerda da corda quando o bulge é positivo.
            var sweep = 4 * Math.Atan(bulge);
            var radius = chord * (1 + (bulge * bulge)) / (4 * Math.Abs(bulge));
            var offset = chord * (1 - (bulge * bulge)) / (4 * bulge);
            var middle = new GeoMapCoordinate((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var leftX = -(end.Y - start.Y) / chord;
            var leftY = (end.X - start.X) / chord;
            var center = new GeoMapCoordinate(middle.X + (leftX * offset), middle.Y + (leftY * offset));
            var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            return new Segment(
                index, start, end, startStation, radius * Math.Abs(sweep), true, center, radius, startAngle, sweep);
        }

        public GeoMapCoordinate PointAt(double fraction)
        {
            if (!_isArc)
            {
                return new GeoMapCoordinate(
                    _start.X + ((_end.X - _start.X) * fraction),
                    _start.Y + ((_end.Y - _start.Y) * fraction));
            }

            var angle = _startAngle + (_sweep * fraction);
            return new GeoMapCoordinate(_center.X + (_radius * Math.Cos(angle)), _center.Y + (_radius * Math.Sin(angle)));
        }

        /// <summary>Distância do ponto ao trecho e a posição do pé da perpendicular ao longo da linha.</summary>
        public (double Distance, double Station) Project(GeoMapCoordinate point)
        {
            if (!_isArc)
            {
                if (Length == 0)
                {
                    return (Distance(point, _start), _startStation);
                }

                var dx = _end.X - _start.X;
                var dy = _end.Y - _start.Y;
                var t = (((point.X - _start.X) * dx) + ((point.Y - _start.Y) * dy)) / (Length * Length);
                t = Math.Max(0, Math.Min(1, t));
                var foot = new GeoMapCoordinate(_start.X + (dx * t), _start.Y + (dy * t));
                return (Distance(point, foot), _startStation + (t * Length));
            }

            var angle = Math.Atan2(point.Y - _center.Y, point.X - _center.X);
            var travelled = _sweep >= 0
                ? Normalize(angle - _startAngle)
                : Normalize(_startAngle - angle);
            if (travelled <= Math.Abs(_sweep))
            {
                return (
                    Math.Abs(Distance(point, _center) - _radius),
                    _startStation + (_radius * travelled));
            }

            var toStart = Distance(point, _start);
            var toEnd = Distance(point, _end);
            return toStart <= toEnd ? (toStart, _startStation) : (toEnd, _startStation + Length);
        }

        private static double Normalize(double angle)
        {
            var full = 2 * Math.PI;
            angle %= full;
            return angle < 0 ? angle + full : angle;
        }
    }
}
