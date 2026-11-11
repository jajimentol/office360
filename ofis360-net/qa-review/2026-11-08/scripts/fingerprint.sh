#!/usr/bin/env bash
# Test edilen kaynak agacinin parmak izi (git deposu olmadigi icin). Depo kokunden calistirin:
#   bash qa-review/2026-11-08/scripts/fingerprint.sh
find src web/src tests docker -type f \( -name "*.cs" -o -name "*.ts" -o -name "*.html" -o -name "*.css" -o -name "*.sql" -o -name "*.json" -o -name "*.yml" -o -name "*.csproj" \) \
  ! -path "*/bin/*" ! -path "*/obj/*" ! -name "*.Designer.cs" | LC_ALL=C sort | xargs sha256sum | sha256sum | cut -c1-16
