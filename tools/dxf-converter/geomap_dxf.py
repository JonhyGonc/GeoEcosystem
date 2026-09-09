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


def convert(options: ConversionOptions) -> tuple[dict[str, object], dict[str, object]]:
    if not EPSG_PATTERN.fullmatch(options.source_crs):
        raise DxfConversionError("--source-crs must use the form EPSG:<code>.")
    if options.units not in SUPPORTED_UNITS:
        raise DxfConversionError(f"Unsupported unit {options.units!r}.")
    if not options.exported_at.endswith("Z"):
        raise DxfConversionError("--exported-at must be an ISO 8601 UTC value ending in Z.")
    if options.input_path.resolve() == options.output_path.resolve():
        raise DxfConversionError("Input and output paths must differ.")

    pairs = read_pairs(options.input_path)
    features: list[dict[str, object]] = []
    ignored: Counter[str] = Counter()
    seen_handles: set[str] = set()
    skipped_paper_space = 0

    for entity in entities_section(pairs):
        if _entity_layer(entity) != options.layer:
            continue
        if not _is_modelspace(entity):
            skipped_paper_space += 1
            continue
        if entity.entity_type == "LWPOLYLINE":
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
        raise DxfConversionError(f"Layer {options.layer!r} has no supported linear geometry.")
    if len(features) > MAX_JSON_FEATURES:
        raise DxfConversionError(
            f"Selected layer contains {len(features)} features; the JSON contract limit is "
            f"{MAX_JSON_FEATURES}. Use the definitive exporter with NDJSON."
        )

    document: dict[str, object] = {
        "contractVersion": "0.1",
        "sourceSystem": "Civil 3D DXF",
        "sourceCrs": options.source_crs,
        "units": options.units,
        "project": {
            "externalId": options.project_external_id,
            "name": options.project_name,
        },
        "network": {
            "externalId": options.network_external_id,
            "name": options.network_name,
            "type": options.network_type,
        },
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
