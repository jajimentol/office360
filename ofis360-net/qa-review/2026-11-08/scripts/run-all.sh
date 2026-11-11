#!/usr/bin/env bash
# Tum inceleme paketini sirayla calistirir. Onkosul: 06-yeniden-calistirma.md §2 (perf API 5211, prod paket 4211).
# UYARI: resilience.mjs Oracle konteynerini gecici olarak durdurur.
set -e
cd "$(dirname "$0")"
node security.mjs
node functional.mjs
./run-scenario.sh S1-normal 50 180 30 3000
./run-scenario.sh S2-peak 150 300 60 2000
./run-scenario.sh S3-spike 300 120 10 2000
./run-scenario.sh S4-stress500 500 180 60 1000
./run-scenario.sh S5-capacity 60 90 10 0
./run-scenario.sh S6-soak 100 900 30 3000
node integrity.mjs
node ui.cjs
node resilience.mjs
./backup-restore.sh
node gen-matrix.mjs
