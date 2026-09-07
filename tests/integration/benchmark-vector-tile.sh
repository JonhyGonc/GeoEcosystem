#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 4 ]]; then
  echo "Usage: benchmark-vector-tile.sh <base-url> <dataset> <project-id> <network-id>" >&2
  exit 2
fi

base_url="$1"
dataset="$2"
project_id="$3"
network_id="$4"
url="$base_url/api/projects/$project_id/networks/$network_id/tiles/15/11938/18866.mvt"

tile_file="$(mktemp)"
timings_file="$(mktemp)"
sorted_timings_file="$(mktemp)"
trap 'rm -f "$tile_file" "$timings_file" "$sorted_timings_file"' EXIT

curl --fail --silent --show-error --max-time 10 "$url" > "$tile_file"
summary="$(python3 tests/integration/validate-mvt.py "$tile_file" --json)"
feature_count="$(jq --raw-output '.feature_count' <<< "$summary")"
tile_bytes="$(wc --bytes < "$tile_file")"

for _ in $(seq 1 30); do
  curl --fail --silent --show-error --max-time 10 \
    --output /dev/null \
    --write-out '%{time_total}\n' \
    "$url" >> "$timings_file"
done

LC_ALL=C sort --numeric-sort "$timings_file" > "$sorted_timings_file"

awk \
  -v dataset="$dataset" \
  -v feature_count="$feature_count" \
  -v tile_bytes="$tile_bytes" '
  { values[NR] = $1 }
  END {
    lower = values[int((NR + 1) / 2)]
    upper = values[int((NR + 2) / 2)]
    median = (lower + upper) / 2
    p95_index = int((NR * 95 + 99) / 100)
    printf "VECTOR_TILE_BENCHMARK dataset=%s samples=%d features=%s tile_bytes=%s median_ms=%.3f p95_ms=%.3f max_ms=%.3f\n", dataset, NR, feature_count, tile_bytes, median * 1000, values[p95_index] * 1000, values[NR] * 1000
    if (dataset == "large" && tile_bytes > 524288) {
      print "LARGE vector tile exceeded the 512 KiB regression guard" > "/dev/stderr"
      exit 1
    }
    if (dataset == "large" && values[p95_index] > 0.100) {
      print "LARGE vector tile exceeded the 100 ms p95 regression guard" > "/dev/stderr"
      exit 1
    }
  }
' "$sorted_timings_file"
