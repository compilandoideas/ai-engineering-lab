#!/usr/bin/env bash
# Fails if the README names a path (in backticks or a Markdown link) that does not exist.
set -euo pipefail
cd "$(dirname "$0")/.."

missing=0
while IFS= read -r path; do
  path="${path%%#*}"
  [[ -z "$path" ]] && continue
  if [[ ! -e "$path" ]]; then
    echo "missing: $path"
    missing=1
  fi
done < <(grep -oE '\]\([^)#]+|`(src|tests|scripts|evidence|contracts|heldout)/[^` ]+`' README.md \
  | sed -E 's/^\]\(//; s/`//g' | grep -vE '^https?:' | grep -vE '<|\*' | sort -u)

if [[ $missing -eq 0 ]]; then
  echo "README paths: OK"
fi
exit $missing
