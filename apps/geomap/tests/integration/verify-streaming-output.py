import json
import sys
from pathlib import Path


if len(sys.argv) != 6:
    raise SystemExit(
        "usage: verify-streaming-output.py SUMMARY PROGRESS COUNT BATCHES REPLACED"
    )

summary = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
progress = [
    json.loads(line)
    for line in Path(sys.argv[2]).read_text(encoding="utf-8").splitlines()
    if line.strip()
]
expected_count = int(sys.argv[3])
expected_batches = int(sys.argv[4])
expected_replaced = int(sys.argv[5])

assert summary["success"] is True, summary
assert summary["received"] == expected_count, summary
assert summary["imported"] == expected_count, summary
assert summary["rejected"] == 0, summary
assert summary["replaced"] == expected_replaced, summary
assert summary["batches"] == expected_batches, summary
assert len(progress) == expected_batches, len(progress)
assert all(event["event"] == "progress" for event in progress)
assert all(
    current["processed"] > previous["processed"]
    for previous, current in zip(progress, progress[1:])
)
assert progress[-1]["processed"] == expected_count, progress[-1]
