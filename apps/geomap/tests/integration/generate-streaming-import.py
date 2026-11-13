import json
import sys
from pathlib import Path


if len(sys.argv) not in (4, 5):
    raise SystemExit(
        "usage: generate-streaming-import.py OUTPUT PROFILE COUNT [INVALID_INDEX]"
    )

output = Path(sys.argv[1])
profile = sys.argv[2].lower()
count = int(sys.argv[3])
invalid_index = int(sys.argv[4]) if len(sys.argv) == 5 else None

metadata = {
    "contractVersion": "0.1",
    "sourceSystem": "GeoMap streaming integration test",
    "sourceCrs": "EPSG:4326",
    "units": "degree",
    "project": {
        "externalId": f"streaming-{profile}-project",
        "name": f"GeoMap Streaming {profile.upper()}",
    },
    "network": {
        "externalId": f"streaming-{profile}-network",
        "name": f"Drenagem Streaming {profile.upper()}",
        "type": "StormWater",
    },
    "exportedAt": "2026-09-08T17:20:00Z",
}

with output.open("w", encoding="utf-8", newline="\n") as stream:
    stream.write(json.dumps(metadata, ensure_ascii=False, separators=(",", ":")))
    stream.write("\n")
    for index in range(count):
        column = index % 1000
        row = index // 1000
        longitude = -48.90 + column * 0.00001
        latitude = -26.35 + row * 0.00001
        start = [round(longitude, 8), round(latitude, 8)]
        if index == invalid_index:
            start.append(10.0)
        feature = {
            "sourceId": f"{profile}-{index:07d}",
            "sourceType": "Polyline",
            "sourceLayer": f"STREAMING-{profile.upper()}",
            "geometry": {
                "type": "LineString",
                "coordinates": [
                    start,
                    [round(longitude + 0.000005, 8), round(latitude + 0.000005, 8)],
                ],
            },
            "properties": {
                "dataset": f"streaming-{profile}",
                "sequence": index,
                "diameter_mm": 300 + index % 4 * 100,
                "material": "PVC",
            },
        }
        stream.write(json.dumps(feature, ensure_ascii=False, separators=(",", ":")))
        stream.write("\n")
