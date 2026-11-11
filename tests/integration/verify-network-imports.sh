#!/usr/bin/env bash
# Confere o histórico de importações de uma rede (Issue #64): quantidade de
# linhas, metadados do arquivo e SHA-256 dos bytes importados.
# Uso: verify-network-imports.sh <rede> <versão> <sistema> <srid> <unidade> <arquivo> <importações> <feições>
set -euo pipefail

if [ "$#" -ne 8 ]; then
  echo "usage: $0 <network-name> <contract-version> <source-system> <source-srid> <units> <file> <imports> <features>" >&2
  exit 2
fi

sha256="$(sha256sum "$6" | cut -d' ' -f1)"
script_dir="$(cd "$(dirname "$0")" && pwd)"

docker compose exec -T database sh -ec \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v network_name="$1" -v contract_version="$2" -v source_system="$3" -v source_srid="$4" -v units="$5" -v sha256="$6" -v imports="$7" -v features="$8"' \
  sh "$1" "$2" "$3" "$4" "$5" "$sha256" "$7" "$8" \
  < "$script_dir/verify-network-imports.sql"
