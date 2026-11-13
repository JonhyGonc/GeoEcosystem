import json
import sys
from pathlib import Path


if len(sys.argv) != 5:
    raise SystemExit(
        "usage: verify-streaming-failure.py SUMMARY PROGRESS RECEIVED STAGED"
    )

summary = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
progress = [
    json.loads(line)
    for line in Path(sys.argv[2]).read_text(encoding="utf-8").splitlines()
    if line.strip()
]
expected_received = int(sys.argv[3])
expected_staged = int(sys.argv[4])

assert summary["success"] is False, summary
assert summary["received"] == expected_received, summary
assert summary["imported"] == 0, summary
assert summary["errors"][0]["code"] == "GMC_GEOMETRY_DIMENSION_UNSUPPORTED", summary
assert progress[-1]["processed"] == expected_staged, progress
