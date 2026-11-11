#!/usr/bin/env python3
"""Provisional ASCII DXF to GeoMap Contract v0.1 converter.

This bridge is intentionally small and independent from Autodesk. It exists to
validate a real CAD network before the Civil 3D exporter is implemented.
"""

from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import sys
from typing import Iterable, Sequence


MAX_DXF_BYTES = 512 * 1024 * 1024
MAX_JSON_FEATURES = 10_000
EPSG_PATTERN = re.compile(r"^EPSG:[1-9][0-9]{2,5}$")
SUPPORTED_UNITS = {"metre", "foot", "us-survey-foot", "degree"}
GEOMETRY_ENTITY_TYPES = {"LINE", "LWPOLYLINE", "POLYLINE", "ARC", "SPLINE"}
IGNORED_ENTITY_TYPES = {
    "3DFACE",
    "ATTDEF",
    "ATTRIB",
    "CIRCLE",
    "DIMENSION",
    "ELLIPSE",
    "HATCH",
    "IMAGE",
    "INSERT",
    "LEADER",
    "MLEADER",
    "MTEXT",
    "POINT",
    "SOLID",
    "TEXT",
    "TRACE",
    "VIEWPORT",
}


class DxfConversionError(ValueError):
    """A safe, user-facing conversion failure."""


@dataclass(frozen=True)
class DxfEntity:
    entity_type: str
    pairs: tuple[tuple[int, str], ...]

    def first(self, code: int, default: str | None = None) -> str | None:
        for pair_code, value in self.pairs:
            if pair_code == code:
                return value
        return default


@dataclass(frozen=True)
class ConversionOptions:
    input_path: Path
    output_path: Path
    layer: str
    source_crs: str
    units: str
    project_external_id: str
    project_name: str
    network_external_id: str
    network_name: str
    network_type: str
    exported_at: str
    project_to_2d: bool
    blocks: bool = False


def _finite_number(value: str, field: str) -> float:
    try:
        number = float(value)
    except ValueError as exc:
        raise DxfConversionError(f"Invalid numeric value for {field}: {value!r}.") from exc
    if not math.isfinite(number):
        raise DxfConversionError(f"Non-finite numeric value for {field}.")
    return number


def _decode_dxf(data: bytes) -> str:
    if data.startswith((b"AutoCAD Binary DXF", b"AC10")):
        raise DxfConversionError(
            "The provisional bridge requires an ASCII DXF, not DWG or binary DXF."
        )
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return data.decode("cp1252")


def read_pairs(path: Path) -> list[tuple[int, str]]:
    size = path.stat().st_size
    if size > MAX_DXF_BYTES:
        raise DxfConversionError(
            f"DXF exceeds the provisional {MAX_DXF_BYTES // (1024 * 1024)} MiB limit."
        )
    lines = _decode_dxf(path.read_bytes()).splitlines()
    if len(lines) % 2:
        raise DxfConversionError("DXF must contain complete group-code/value pairs.")

    pairs: list[tuple[int, str]] = []
    for index in range(0, len(lines), 2):
        try:
            code = int(lines[index].strip())
        except ValueError as exc:
            raise DxfConversionError(
                f"Invalid DXF group code at line {index + 1}."
            ) from exc
        pairs.append((code, lines[index + 1].rstrip("\r")))
    return pairs


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def entities_section(pairs: Sequence[tuple[int, str]]) -> Iterable[DxfEntity]:
    in_entities = False
    index = 0
    found_entities = False
    while index < len(pairs):
        code, value = pairs[index]
        if code == 0 and value == "SECTION":
            if index + 1 >= len(pairs) or pairs[index + 1][0] != 2:
                raise DxfConversionError("Malformed DXF SECTION header.")
            in_entities = pairs[index + 1][1] == "ENTITIES"
            found_entities = found_entities or in_entities
            index += 2
            continue
        if code == 0 and value == "ENDSEC":
            in_entities = False
            index += 1
            continue
        if not in_entities or code != 0:
            index += 1
            continue

        entity_type = value.upper()
        start = index + 1
        index = start
        while index < len(pairs) and pairs[index][0] != 0:
            index += 1
        yield DxfEntity(entity_type, tuple(pairs[start:index]))

    if not found_entities:
        raise DxfConversionError("DXF does not contain an ENTITIES section.")


# Índices fixos da paleta ACI do AutoCAD. O ACI 7 é branco sobre fundo escuro
# e preto sobre fundo claro; como o mapa base é claro, ele vira preto.
_ACI_FIXED = {
    1: (255, 0, 0),
    2: (255, 255, 0),
    3: (0, 255, 0),
    4: (0, 255, 255),
    5: (0, 0, 255),
    6: (255, 0, 255),
    7: (0, 0, 0),
    8: (128, 128, 128),
    9: (192, 192, 192),
    250: (51, 51, 51),
    251: (80, 80, 80),
    252: (105, 105, 105),
    253: (130, 130, 130),
    254: (190, 190, 190),
    255: (255, 255, 255),
}
_ACI_LEVELS = (255, 255, 204, 204, 153, 153, 127, 127, 76, 76)


def aci_to_hex(index: int) -> str | None:
    """Converte um índice ACI (1 a 255) em #RRGGBB.

    Os índices 10 a 249 seguem a estrutura da paleta do AutoCAD: 24 matizes de
    15° e, para cada matiz, 5 níveis de brilho, cada um em versão pura (par) e
    com 50% de saturação (ímpar).
    """
    if index in _ACI_FIXED:
        red, green, blue = _ACI_FIXED[index]
        return f"#{red:02X}{green:02X}{blue:02X}"
    if not 10 <= index <= 249:
        return None

    hue = ((index - 10) // 10) * 15
    step = (index - 10) % 10
    level = _ACI_LEVELS[step]
    sector, offset = divmod(hue, 60)
    rising = 255 * offset / 60
    falling = 255 - rising
    pure = (
        (255, rising, 0),
        (falling, 255, 0),
        (0, 255, rising),
        (0, falling, 255),
        (rising, 0, 255),
        (255, 0, falling),
    )[sector]
    channels = []
    for channel in pure:
        value = channel * level / 255
        if step % 2:
            value += (level - value) / 2
        channels.append(int(value))
    return "#" + "".join(f"{channel:02X}" for channel in channels)


def layer_color(pairs: Sequence[tuple[int, str]], layer: str) -> str | None:
    """Lê a cor de uma layer na tabela LAYER: true color (420) ou ACI (62)."""
    in_tables = False
    index = 0
    while index < len(pairs):
        code, value = pairs[index]
        if code == 0 and value == "SECTION" and index + 1 < len(pairs):
            in_tables = pairs[index + 1] == (2, "TABLES")
            index += 2
            continue
        if code == 0 and value == "ENDSEC":
            if in_tables:
                return None
            index += 1
            continue
        if not (in_tables and code == 0 and value == "LAYER"):
            index += 1
            continue

        start = index + 1
        index = start
        while index < len(pairs) and pairs[index][0] != 0:
            index += 1
        record = pairs[start:index]
        if next((v for c, v in record if c == 2), None) != layer:
            continue

        true_color = next((v for c, v in record if c == 420), None)
        if true_color is not None:
            rgb = int(true_color.strip()) & 0xFFFFFF
            return f"#{rgb:06X}"
        aci = next((v for c, v in record if c == 62), None)
        # Um índice negativo apenas indica layer desligada; a cor é o módulo.
        return aci_to_hex(abs(int(aci.strip()))) if aci is not None else None
    return None


def _entity_layer(entity: DxfEntity) -> str:
    return entity.first(8, "0") or "0"


def _is_modelspace(entity: DxfEntity) -> bool:
    paper_space_flag = entity.first(67, "0") or "0"
    try:
        return int(paper_space_flag) == 0
    except ValueError as exc:
        raise DxfConversionError(
            f"Entity {_entity_handle(entity)} has an invalid paper-space flag."
        ) from exc


def _entity_handle(entity: DxfEntity) -> str:
    handle = (entity.first(5) or "").strip().upper()
    if not handle:
        raise DxfConversionError(
            f"{entity.entity_type} on layer {_entity_layer(entity)!r} has no CAD handle."
        )
    return handle


def _check_extrusion(entity: DxfEntity) -> None:
    values = {
        code: _finite_number(value, f"extrusion {code}")
        for code, value in entity.pairs
        if code in {210, 220, 230}
    }
    extrusion = (values.get(210, 0.0), values.get(220, 0.0), values.get(230, 1.0))
    if any(abs(actual - expected) > 1e-12 for actual, expected in zip(extrusion, (0, 0, 1))):
        raise DxfConversionError(
            f"Entity {_entity_handle(entity)} uses a non-default extrusion and cannot be "
            "flattened safely by the provisional bridge."
        )


def _projection_required(z_values: Iterable[float], options: ConversionOptions) -> bool:
    has_z = any(abs(value) > 1e-12 for value in z_values)
    if has_z and not options.project_to_2d:
        raise DxfConversionError(
            "Selected geometry has a non-zero elevation. Re-run with --project-to-2d "
            "to explicitly keep XY and preserve elevations as properties."
        )
    return has_z


def _feature(
    entity: DxfEntity,
    coordinates: list[list[float]],
    properties: dict[str, object],
) -> dict[str, object]:
    return {
        "sourceId": _entity_handle(entity),
        "sourceType": "Polyline" if entity.entity_type == "LWPOLYLINE" else "Line",
        "sourceLayer": _entity_layer(entity),
        "geometry": {"type": "LineString", "coordinates": coordinates},
        "properties": properties,
    }


def _convert_lwpolyline(entity: DxfEntity, options: ConversionOptions) -> dict[str, object]:
    _check_extrusion(entity)
    elevation = _finite_number(entity.first(38, "0") or "0", "elevation")
    vertices: list[list[float]] = []
    bulges: list[float] = []
    current: list[float] | None = None
    current_bulge = 0.0

    for code, value in entity.pairs:
        if code == 10:
            if current is not None:
                if len(current) != 2:
                    raise DxfConversionError(f"Entity {_entity_handle(entity)} has incomplete XY.")
                vertices.append(current)
                bulges.append(current_bulge)
            current = [_finite_number(value, "vertex X")]
            current_bulge = 0.0
        elif code == 20 and current is not None:
            if len(current) != 1:
                raise DxfConversionError(f"Entity {_entity_handle(entity)} repeats vertex Y.")
            current.append(_finite_number(value, "vertex Y"))
        elif code == 42 and current is not None:
            current_bulge = _finite_number(value, "vertex bulge")

    if current is not None:
        if len(current) != 2:
            raise DxfConversionError(f"Entity {_entity_handle(entity)} has incomplete XY.")
        vertices.append(current)
        bulges.append(current_bulge)

    declared = int(entity.first(90, str(len(vertices))) or len(vertices))
    if declared != len(vertices):
        raise DxfConversionError(
            f"Entity {_entity_handle(entity)} declares {declared} vertices but contains "
            f"{len(vertices)}."
        )
    if len(vertices) < 2:
        raise DxfConversionError(f"Entity {_entity_handle(entity)} has fewer than two vertices.")
    if any(abs(bulge) > 1e-12 for bulge in bulges):
        raise DxfConversionError(
            f"Entity {_entity_handle(entity)} contains curved bulge segments. "
            "Tessellation belongs to the definitive exporter."
        )

    flags = int(entity.first(70, "0") or "0")
    closed = bool(flags & 1)
    if closed and vertices[0] != vertices[-1]:
        vertices.append(vertices[0].copy())
    projected = _projection_required([elevation], options)
    properties: dict[str, object] = {
        "cadHandle": _entity_handle(entity),
        "cadEntityType": entity.entity_type,
        "vertexCount": declared,
        "closed": closed,
        "cadElevation": elevation,
        "projectedTo2D": projected,
    }
    return _feature(entity, vertices, properties)


def _convert_line(entity: DxfEntity, options: ConversionOptions) -> dict[str, object]:
    _check_extrusion(entity)
    required = {10: "start X", 20: "start Y", 11: "end X", 21: "end Y"}
    values: dict[int, float] = {}
    for code, field in required.items():
        raw = entity.first(code)
        if raw is None:
            raise DxfConversionError(f"Entity {_entity_handle(entity)} is missing {field}.")
        values[code] = _finite_number(raw, field)
    start_z = _finite_number(entity.first(30, "0") or "0", "start Z")
    end_z = _finite_number(entity.first(31, "0") or "0", "end Z")
    projected = _projection_required([start_z, end_z], options)
    return _feature(
        entity,
        [[values[10], values[20]], [values[11], values[21]]],
        {
            "cadHandle": _entity_handle(entity),
            "cadEntityType": entity.entity_type,
            "vertexCount": 2,
            "cadStartElevation": start_z,
            "cadEndElevation": end_z,
            "projectedTo2D": projected,
        },
    )


# Propriedades geradas pela ponte; um atributo de bloco com o mesmo nome recebe
# o prefixo "atributo_" para não sobrescrevê-las.
_RESERVED_BLOCK_PROPERTIES = {
    "cadHandle",
    "cadEntityType",
    "bloco",
    "rotacao",
    "escalaX",
    "escalaY",
    "cadElevation",
    "projectedTo2D",
    "posicaoPeloSimbolo",
}


# Levantamento topográfico fica sempre fora do GeoMap (DEC-029): as layers
# _TG* inteiras e o símbolo de ponto levantado, que também aparece misturado
# nas layers de rede.
SURVEY_LAYER_PREFIX = "_TG"
SURVEY_BLOCK_NAMES = frozenset({"SIMB_PONTO"})
# Blocos dinâmicos são gravados no DXF com nome anônimo (*U123), que não diz o
# que o elemento é e muda entre exportações; ficam fora do GeoMap (DEC-029).
ANONYMOUS_BLOCK_PREFIX = "*U"


def is_survey_layer(layer: str) -> bool:
    return layer.strip().upper().startswith(SURVEY_LAYER_PREFIX)


@dataclass(frozen=True)
class BlockDefinition:
    """Ponto base e extensão do desenho de uma definição de bloco."""

    base_x: float
    base_y: float
    min_x: float
    min_y: float
    max_x: float
    max_y: float

    @property
    def center(self) -> tuple[float, float]:
        return ((self.min_x + self.max_x) / 2, (self.min_y + self.max_y) / 2)

    @property
    def size(self) -> float:
        return math.hypot(self.max_x - self.min_x, self.max_y - self.min_y)

    def symbol_far_from_base(self) -> bool:
        """Indica um bloco desenhado longe do próprio ponto base.

        Alguns blocos são criados com o desenho já nas coordenadas reais e o
        ponto base em 0,0. O AutoCAD mostra o símbolo no lugar certo, mas o
        ponto de inserção fica a centenas de quilômetros. Blocos comuns têm o
        desenho em volta do ponto base, a no máximo algumas vezes o próprio
        tamanho, e mantêm o ponto de inserção.
        """
        center_x, center_y = self.center
        offset = math.hypot(center_x - self.base_x, center_y - self.base_y)
        return offset > _FAR_SYMBOL_FACTOR * max(self.size, _MIN_SYMBOL_SIZE)


# Um símbolo a mais de 50 vezes o próprio tamanho do ponto base é tratado como
# desenhado fora do ponto base.
_FAR_SYMBOL_FACTOR = 50.0
_MIN_SYMBOL_SIZE = 1e-6
_BLOCK_GEOMETRY_X = {10, 11}
_BLOCK_GEOMETRY_Y = {20, 21}


def block_definitions(pairs: Sequence[tuple[int, str]]) -> dict[str, BlockDefinition]:
    """Lê o ponto base e a extensão do desenho de cada bloco da seção BLOCKS."""
    definitions: dict[str, BlockDefinition] = {}
    in_blocks = False
    name: str | None = None
    base = (0.0, 0.0)
    xs: list[float] = []
    ys: list[float] = []
    index = 0
    while index < len(pairs):
        code, value = pairs[index]
        if code == 0 and value == "SECTION":
            in_blocks = index + 1 < len(pairs) and pairs[index + 1][1] == "BLOCKS"
            index += 2
            continue
        if not in_blocks or code != 0:
            index += 1
            continue
        if value == "ENDSEC":
            in_blocks = False
            index += 1
            continue
        end = index + 1
        while end < len(pairs) and pairs[end][0] != 0:
            end += 1
        body = pairs[index + 1 : end]
        if value == "BLOCK":
            fields = {group: text for group, text in body}
            name = fields.get(2, "").strip()
            base = (
                _optional_number(fields.get(10), 0.0),
                _optional_number(fields.get(20), 0.0),
            )
            xs, ys = [], []
        elif value == "ENDBLK":
            if name and xs and ys:
                definitions[name] = BlockDefinition(
                    base[0], base[1], min(xs), min(ys), max(xs), max(ys)
                )
            name = None
        elif name is not None and value not in {"ATTDEF", "TEXT", "MTEXT"}:
            _collect_block_geometry(value, body, xs, ys)
        index = end
    return definitions


def _optional_number(value: str | None, default: float) -> float:
    try:
        number = float(value) if value is not None else default
    except ValueError:
        return default
    return number if math.isfinite(number) else default


def _collect_block_geometry(
    entity_type: str,
    body: Sequence[tuple[int, str]],
    xs: list[float],
    ys: list[float],
) -> None:
    radius = 0.0
    if entity_type in {"CIRCLE", "ARC"}:
        radius = abs(_optional_number(next((v for c, v in body if c == 40), None), 0.0))
    for code, text in body:
        number = _optional_number(text, math.nan)
        if not math.isfinite(number):
            continue
        if code in _BLOCK_GEOMETRY_X:
            xs.extend((number - radius, number + radius))
        elif code in _BLOCK_GEOMETRY_Y:
            ys.extend((number - radius, number + radius))


def _symbol_position(
    insertion: tuple[float, float],
    definition: BlockDefinition,
    rotation_degrees: float,
    scale_x: float,
    scale_y: float,
) -> tuple[float, float]:
    """Posição do centro do símbolo no desenho, como o AutoCAD o mostra."""
    center_x, center_y = definition.center
    local_x = (center_x - definition.base_x) * scale_x
    local_y = (center_y - definition.base_y) * scale_y
    angle = math.radians(rotation_degrees)
    cos_a, sin_a = math.cos(angle), math.sin(angle)
    return (
        insertion[0] + local_x * cos_a - local_y * sin_a,
        insertion[1] + local_x * sin_a + local_y * cos_a,
    )


def _convert_insert(
    entity: DxfEntity,
    options: ConversionOptions,
    definitions: dict[str, BlockDefinition] | None = None,
) -> dict[str, object]:
    _check_extrusion(entity)
    block_name = (entity.first(2) or "").strip()
    if not block_name:
        raise DxfConversionError(f"Block reference {_entity_handle(entity)} has no block name.")
    x = _finite_number(entity.first(10) or "", "insertion X") if entity.first(10) else None
    y = _finite_number(entity.first(20) or "", "insertion Y") if entity.first(20) else None
    if x is None or y is None:
        raise DxfConversionError(f"Block reference {_entity_handle(entity)} has no insertion point.")
    elevation = _finite_number(entity.first(30, "0") or "0", "insertion Z")
    projected = _projection_required([elevation], options)
    rotation = _finite_number(entity.first(50, "0") or "0", "rotation")
    scale_x = _finite_number(entity.first(41, "1") or "1", "scale X")
    scale_y = _finite_number(entity.first(42, "1") or "1", "scale Y")
    definition = (definitions or {}).get(block_name)
    from_symbol = definition is not None and definition.symbol_far_from_base()
    if from_symbol:
        assert definition is not None
        x, y = _symbol_position((x, y), definition, rotation, scale_x, scale_y)
    return {
        "sourceId": _entity_handle(entity),
        "sourceType": "BlockReference",
        "sourceLayer": _entity_layer(entity),
        "geometry": {"type": "Point", "coordinates": [x, y]},
        "properties": {
            "cadHandle": _entity_handle(entity),
            "cadEntityType": entity.entity_type,
            "bloco": block_name,
            "rotacao": rotation,
            "escalaX": scale_x,
            "escalaY": scale_y,
            "cadElevation": elevation,
            "projectedTo2D": projected,
            "posicaoPeloSimbolo": from_symbol,
        },
    }


def _attach_attribute(feature: dict[str, object], attribute: DxfEntity) -> None:
    tag = (attribute.first(2) or "").strip()
    if not tag:
        return
    properties = feature["properties"]
    assert isinstance(properties, dict)
    name = f"atributo_{tag}" if tag in _RESERVED_BLOCK_PROPERTIES else tag
    properties[name] = (attribute.first(1) or "").strip()


def convert(options: ConversionOptions) -> tuple[dict[str, object], dict[str, object]]:
    if not EPSG_PATTERN.fullmatch(options.source_crs):
        raise DxfConversionError("--source-crs must use the form EPSG:<code>.")
    if options.units not in SUPPORTED_UNITS:
        raise DxfConversionError(f"Unsupported unit {options.units!r}.")
    if not options.exported_at.endswith("Z"):
        raise DxfConversionError("--exported-at must be an ISO 8601 UTC value ending in Z.")
    if options.input_path.resolve() == options.output_path.resolve():
        raise DxfConversionError("Input and output paths must differ.")

    if is_survey_layer(options.layer):
        raise DxfConversionError(
            f"Layer {options.layer!r} is a topographic survey layer (_TG*); survey points "
            "are always excluded from GeoMap (DEC-029)."
        )

    pairs = read_pairs(options.input_path)
    features: list[dict[str, object]] = []
    ignored: Counter[str] = Counter()
    seen_handles: set[str] = set()
    skipped_paper_space = 0

    # INSERT com atributos é seguido por entidades ATTRIB e um SEQEND; os
    # atributos pertencem ao bloco anterior, mesmo que estejam em outra layer.
    open_block: dict[str, object] | None = None
    definitions = block_definitions(pairs) if options.blocks else {}

    for entity in entities_section(pairs):
        if entity.entity_type in {"ATTRIB", "SEQEND"}:
            if entity.entity_type == "ATTRIB" and open_block is not None:
                _attach_attribute(open_block, entity)
            elif entity.entity_type == "SEQEND":
                open_block = None
            if _entity_layer(entity) == options.layer and not options.blocks:
                ignored[entity.entity_type] += 1
            continue
        open_block = None
        if _entity_layer(entity) != options.layer:
            continue
        if not _is_modelspace(entity):
            skipped_paper_space += 1
            continue
        if options.blocks:
            if entity.entity_type != "INSERT":
                ignored[entity.entity_type] += 1
                continue
            block_name = (entity.first(2) or "").strip().upper()
            if block_name in SURVEY_BLOCK_NAMES:
                ignored["INSERT survey point"] += 1
                continue
            if block_name.startswith(ANONYMOUS_BLOCK_PREFIX):
                ignored["INSERT anonymous block"] += 1
                continue
            feature = _convert_insert(entity, options, definitions)
            if (entity.first(66, "0") or "0").strip() == "1":
                open_block = feature
        elif entity.entity_type == "LWPOLYLINE":
            feature = _convert_lwpolyline(entity, options)
        elif entity.entity_type == "LINE":
            feature = _convert_line(entity, options)
        elif entity.entity_type in GEOMETRY_ENTITY_TYPES:
            raise DxfConversionError(
                f"Layer {options.layer!r} contains unsupported geometry "
                f"{entity.entity_type}; conversion stopped to prevent shape loss."
            )
        else:
            ignored[entity.entity_type] += 1
            continue

        source_id = str(feature["sourceId"])
        if source_id in seen_handles:
            raise DxfConversionError(f"Duplicate CAD handle {source_id!r} in selected layer.")
        seen_handles.add(source_id)
        features.append(feature)

    if not features:
        kind = "block references" if options.blocks else "supported linear geometry"
        raise DxfConversionError(f"Layer {options.layer!r} has no {kind}.")
    if len(features) > MAX_JSON_FEATURES:
        raise DxfConversionError(
            f"Selected layer contains {len(features)} features; the JSON contract limit is "
            f"{MAX_JSON_FEATURES}. Use the definitive exporter with NDJSON."
        )

    color = layer_color(pairs, options.layer)
    network: dict[str, object] = {
        "externalId": options.network_external_id,
        "name": options.network_name,
        "type": options.network_type,
    }
    if color:
        network["color"] = color

    # Pontos exigem a 0.3; a 0.2 só é necessária quando há cor da layer.
    contract_version = "0.3" if options.blocks else ("0.2" if color else "0.1")
    document: dict[str, object] = {
        "contractVersion": contract_version,
        "sourceSystem": "Civil 3D DXF",
        "sourceCrs": options.source_crs,
        "units": options.units,
        "project": {
            "externalId": options.project_external_id,
            "name": options.project_name,
        },
        "network": network,
        "exportedAt": options.exported_at,
        "features": features,
    }
    summary: dict[str, object] = {
        "status": "converted",
        "features": len(features),
        "ignoredEntities": dict(sorted(ignored.items())),
        "skippedPaperSpaceEntities": skipped_paper_space,
        "projectedFeatures": sum(
            bool(feature["properties"]["projectedTo2D"]) for feature in features  # type: ignore[index]
        ),
        "networkColor": color,
        "symbolPositionedBlocks": sum(
            bool(feature["properties"].get("posicaoPeloSimbolo"))  # type: ignore[union-attr]
            for feature in features
        ),
        "inputSha256": sha256_file(options.input_path),
    }
    return document, summary


def _now_utc() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def parse_args(argv: Sequence[str] | None = None) -> ConversionOptions:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--layer", required=True)
    parser.add_argument("--source-crs", required=True)
    parser.add_argument("--units", required=True, choices=sorted(SUPPORTED_UNITS))
    parser.add_argument("--project-external-id", required=True)
    parser.add_argument("--project-name", required=True)
    parser.add_argument("--network-external-id", required=True)
    parser.add_argument("--network-name", required=True)
    parser.add_argument("--network-type", required=True)
    parser.add_argument("--exported-at", default=_now_utc())
    parser.add_argument(
        "--project-to-2d",
        action="store_true",
        help="Explicitly discard Z/elevation from geometry and retain it in properties.",
    )
    parser.add_argument(
        "--blocks",
        action="store_true",
        help="Convert the layer block references (INSERT) to Point features with their "
        "block name and attributes (contract 0.3) instead of linear geometry.",
    )
    args = parser.parse_args(argv)
    return ConversionOptions(
        input_path=args.input,
        output_path=args.output,
        layer=args.layer,
        source_crs=args.source_crs,
        units=args.units,
        project_external_id=args.project_external_id,
        project_name=args.project_name,
        network_external_id=args.network_external_id,
        network_name=args.network_name,
        network_type=args.network_type,
        exported_at=args.exported_at,
        project_to_2d=args.project_to_2d,
        blocks=args.blocks,
    )


def main(argv: Sequence[str] | None = None) -> int:
    try:
        options = parse_args(argv)
        document, summary = convert(options)
        options.output_path.parent.mkdir(parents=True, exist_ok=True)
        options.output_path.write_text(
            json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        print(json.dumps(summary, separators=(",", ":")), file=sys.stderr)
        return 0
    except (DxfConversionError, OSError) as exc:
        print(json.dumps({"status": "failed", "error": str(exc)}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
