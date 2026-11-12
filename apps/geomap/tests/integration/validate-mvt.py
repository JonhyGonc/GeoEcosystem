#!/usr/bin/env python3
import argparse
import json
import sys
import uuid
from pathlib import Path


def read_varint(data: bytes, offset: int) -> tuple[int, int]:
    value = 0
    shift = 0
    while offset < len(data) and shift < 70:
        byte = data[offset]
        offset += 1
        value |= (byte & 0x7F) << shift
        if byte < 0x80:
            return value, offset
        shift += 7
    raise ValueError("invalid or truncated protobuf varint")


def read_fields(data: bytes):
    offset = 0
    while offset < len(data):
        key, offset = read_varint(data, offset)
        field_number = key >> 3
        wire_type = key & 0x07
        if field_number == 0:
            raise ValueError("protobuf field number cannot be zero")

        if wire_type == 0:
            value, offset = read_varint(data, offset)
        elif wire_type == 1:
            end = offset + 8
            if end > len(data):
                raise ValueError("truncated fixed64 field")
            value = data[offset:end]
            offset = end
        elif wire_type == 2:
            length, offset = read_varint(data, offset)
            end = offset + length
            if end > len(data):
                raise ValueError("truncated length-delimited field")
            value = data[offset:end]
            offset = end
        elif wire_type == 5:
            end = offset + 4
            if end > len(data):
                raise ValueError("truncated fixed32 field")
            value = data[offset:end]
            offset = end
        else:
            raise ValueError(f"unsupported protobuf wire type {wire_type}")

        yield field_number, wire_type, value


def read_packed_varints(data: bytes) -> list[int]:
    values = []
    offset = 0
    while offset < len(data):
        value, offset = read_varint(data, offset)
        values.append(value)
    return values


def parse_value(data: bytes):
    fields = list(read_fields(data))
    if len(fields) != 1:
        raise ValueError("MVT value must contain exactly one typed field")
    field_number, wire_type, value = fields[0]
    if field_number == 1 and wire_type == 2:
        return value.decode("utf-8")
    if field_number in (4, 5, 6, 7) and wire_type == 0:
        return value
    raise ValueError(f"unsupported MVT value field {field_number}")


def parse_feature(data: bytes, keys: list[str], values: list[object]):
    tags = []
    geometry_type = None
    geometry = []
    for field_number, wire_type, value in read_fields(data):
        if field_number == 2 and wire_type == 2:
            tags.extend(read_packed_varints(value))
        elif field_number == 3 and wire_type == 0:
            geometry_type = value
        elif field_number == 4 and wire_type == 2:
            geometry.extend(read_packed_varints(value))

    if len(tags) % 2 != 0:
        raise ValueError("MVT feature tags must contain key/value pairs")
    properties = {}
    for index in range(0, len(tags), 2):
        key_index, value_index = tags[index:index + 2]
        if key_index >= len(keys) or value_index >= len(values):
            raise ValueError("MVT feature tag index is out of bounds")
        properties[keys[key_index]] = values[value_index]

    if geometry_type != 2:
        raise ValueError(f"expected LineString geometry type 2, received {geometry_type}")
    if not geometry:
        raise ValueError("MVT feature geometry is empty")
    command_ids = []
    offset = 0
    while offset < len(geometry):
        command = geometry[offset]
        offset += 1
        command_id = command & 0x07
        count = command >> 3
        if count == 0 or command_id not in (1, 2, 7):
            raise ValueError("MVT line geometry contains an invalid command")
        parameter_count = 0 if command_id == 7 else count * 2
        offset += parameter_count
        if offset > len(geometry):
            raise ValueError("MVT line geometry command is truncated")
        command_ids.append(command_id)

    if not command_ids or command_ids[0] != 1 or 2 not in command_ids:
        raise ValueError("MVT line geometry is missing MoveTo or LineTo commands")

    feature_id = properties.get("id")
    if not isinstance(feature_id, str):
        raise ValueError("MVT feature is missing the string id property")
    uuid.UUID(feature_id)
    return feature_id


def parse_layer(data: bytes):
    name = None
    raw_features = []
    keys = []
    values = []
    extent = None
    version = None
    for field_number, wire_type, value in read_fields(data):
        if field_number == 1 and wire_type == 2:
            name = value.decode("utf-8")
        elif field_number == 2 and wire_type == 2:
            raw_features.append(value)
        elif field_number == 3 and wire_type == 2:
            keys.append(value.decode("utf-8"))
        elif field_number == 4 and wire_type == 2:
            values.append(parse_value(value))
        elif field_number == 5 and wire_type == 0:
            extent = value
        elif field_number == 15 and wire_type == 0:
            version = value

    if name != "network_features":
        raise ValueError(f"unexpected MVT layer name {name!r}")
    if extent != 4096 or version != 2:
        raise ValueError(f"unexpected MVT metadata: extent={extent}, version={version}")

    ids = [parse_feature(feature, keys, values) for feature in raw_features]
    if not ids:
        raise ValueError("MVT layer contains no features")
    return {"layer": name, "extent": extent, "version": version, "feature_count": len(ids), "ids": ids}


def parse_tile(data: bytes):
    layers = [value for number, wire, value in read_fields(data) if number == 3 and wire == 2]
    if len(layers) != 1:
        raise ValueError(f"expected exactly one MVT layer, received {len(layers)}")
    return parse_layer(layers[0])


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate the minimal GeoMap MVT contract.")
    parser.add_argument("tile", type=Path)
    parser.add_argument("--expected-id", action="append", default=[])
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    try:
        summary = parse_tile(args.tile.read_bytes())
        expected = sorted(args.expected_id)
        if expected and sorted(summary["ids"]) != expected:
            raise ValueError(f"expected feature ids {expected}, received {sorted(summary['ids'])}")
    except (OSError, UnicodeDecodeError, ValueError) as error:
        print(f"Invalid MVT: {error}", file=sys.stderr)
        return 1

    if args.json:
        print(json.dumps(summary, separators=(",", ":")))
    else:
        print(
            f"MVT valid: layer={summary['layer']} features={summary['feature_count']} "
            f"extent={summary['extent']} version={summary['version']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
