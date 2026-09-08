import json
import sys
from pathlib import Path


if len(sys.argv) != 3:
    raise SystemExit("usage: generate-small-import.py VALID_PATH INVALID_PATH")


def feature(index: int) -> dict:
    column = index % 50
    row = index // 50
    x = 688000.0 + column * 8.0
    y = 7086400.0 + row * 8.0
    return {
        "sourceId": f"small-{index:04d}",
        "sourceType": "Polyline",
        "sourceLayer": "INTEGRATION-SMALL",
        "geometry": {
            "type": "LineString",
            "coordinates": [[x, y], [x + 5.0, y + 5.0]],
        },
        "properties": {
            "dataset": "import-small",
            "sequence": index,
            "diameter_mm": 300 + index % 4 * 100,
            "material": "PVC",
        },
    }


document = {
    "contractVersion": "0.1",
    "sourceSystem": "GeoMap integration test",
    "sourceCrs": "EPSG:31982",
    "units": "metre",
    "project": {
        "externalId": "issue-15-project",
        "name": "GeoMap Issue 15 SMALL",
    },
    "network": {
        "externalId": "issue-15-network",
        "name": "Drenagem Issue 15 SMALL",
        "type": "StormWater",
    },
    "exportedAt": "2026-09-08T16:45:00Z",
    "features": [feature(index) for index in range(1000)],
}

Path(sys.argv[1]).write_text(
    json.dumps(document, ensure_ascii=False, separators=(",", ":")),
    encoding="utf-8",
)

invalid = dict(document)
invalid["features"] = list(document["features"])
invalid["features"][0] = feature(0)
invalid["features"][0]["geometry"]["coordinates"][0].append(10.0)
Path(sys.argv[2]).write_text(
    json.dumps(invalid, ensure_ascii=False, separators=(",", ":")),
    encoding="utf-8",
)
