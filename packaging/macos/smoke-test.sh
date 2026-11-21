#!/usr/bin/env bash
# Unzips the macOS release, starts the app from the bundle and checks that it keeps its data
# in ~/Library/Application Support/TravianBotSharp (not inside the bundle) and stays up.
#
# Usage: smoke-test.sh <zip>
set -euo pipefail

zip_file=$1
work=$(mktemp -d)
data_dir="$HOME/Library/Application Support/TravianBotSharp"
rm -rf "$data_dir"

ditto -x -k "$zip_file" "$work"
app="$work/TravianBotSharp.app"
codesign --verify --deep --strict "$app"

log="$work/app.out"
"$app/Contents/MacOS/TravianBotSharp" > "$log" 2>&1 &
pid=$!
ok=1
for _ in $(seq 1 90); do
  [[ -f "$data_dir/TBS.db" ]] && break
  if ! kill -0 "$pid" 2>/dev/null; then ok=0; break; fi
  sleep 1
done
if [[ $ok == 1 && -f "$data_dir/TBS.db" ]]; then
  sleep 10
  kill -0 "$pid" 2>/dev/null || ok=0
else
  ok=0
fi
kill "$pid" 2>/dev/null || true
wait "$pid" 2>/dev/null || true

echo "files in $data_dir:"; ls -A "$data_dir" 2>/dev/null || true
if [[ -e "$app/Contents/MacOS/TBS.db" ]]; then
  echo "FAILED: the app wrote TBS.db inside the bundle"
  exit 1
fi
if [[ $ok != 1 ]]; then
  echo "FAILED: the app did not start and stay up"
  echo "--- output"; cat "$log"
  cat "$data_dir"/logs/*.txt 2>/dev/null || true
  exit 1
fi
echo "Smoke test passed"
