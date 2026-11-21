#!/usr/bin/env bash
# Installs the release packages on a clean Ubuntu (run it as root inside an ubuntu:26.04
# container) and checks that the app starts, as a normal user, from both of them.
#
# Usage: smoke-test.sh <dist-dir>
# Optional env: CHROME_DEB (path to a google-chrome-stable .deb; downloaded from Google if unset).
set -euo pipefail

dist=$(realpath "$1")
deb=$(ls "$dist"/travianbotsharp_*_amd64.deb)
tarball=$(ls "$dist"/TravianBotSharp-*-linux-x64.tar.gz)

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq ca-certificates wget xvfb > /dev/null

chrome_deb=${CHROME_DEB:-}
if [[ -z $chrome_deb ]]; then
  chrome_deb=/tmp/google-chrome-stable_current_amd64.deb
  wget -q -O "$chrome_deb" https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
fi

# Installing both together is how the release notes tell people to do it; apt must be
# able to resolve every dependency of our package from a stock Ubuntu.
apt-get install -y -qq "$chrome_deb" "$deb" > /dev/null
dpkg -s travianbotsharp | sed -n '/^Version/p;/^Depends/p'

useradd -m tester
Xvfb :99 -screen 0 1280x800x24 -nolisten tcp > /dev/null 2>&1 &
xvfb_pid=$!
trap 'kill $xvfb_pid 2>/dev/null || true' EXIT

# Starts the app as "tester", waits for TBS.db to appear in the expected folder,
# then checks the app is still running ten seconds later.
run_app() {
  local name=$1 exe=$2 data_dir=$3
  local log=/tmp/$name.out
  echo "--- $name: $exe (expecting data in $data_dir)"
  setpriv --reuid=tester --regid=tester --init-groups \
    env -i HOME=/home/tester USER=tester PATH=/usr/bin:/bin DISPLAY=:99 "$exe" > "$log" 2>&1 &
  local pid=$!
  local ok=1
  for _ in $(seq 1 90); do
    [[ -f $data_dir/TBS.db ]] && break
    if ! kill -0 "$pid" 2>/dev/null; then ok=0; break; fi
    sleep 1
  done
  if [[ $ok == 1 && -f $data_dir/TBS.db ]]; then
    sleep 10
    kill -0 "$pid" 2>/dev/null || ok=0
  else
    ok=0
  fi
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true

  echo "files in $data_dir:"; ls -A "$data_dir" 2>/dev/null || true
  if [[ $ok != 1 ]]; then
    echo "FAILED: $name did not start and stay up"
    echo "--- output"; cat "$log"
    cat "$data_dir"/logs/*.txt 2>/dev/null || true
    return 1
  fi
  echo "OK: $name started and created $data_dir/TBS.db"
}

# .deb: the app lives in read-only /opt, so its data must go to the user's folder.
run_app deb /usr/bin/travianbotsharp /home/tester/.local/share/TravianBotSharp
if [[ -e /opt/travianbotsharp/TBS.db ]]; then
  echo "FAILED: the .deb install wrote TBS.db into /opt/travianbotsharp"
  exit 1
fi

# Tarball: portable, the data stays next to the executable.
setpriv --reuid=tester --regid=tester --init-groups tar -xzf "$tarball" -C /home/tester
run_app tarball /home/tester/TravianBotSharp/TravianBotSharp /home/tester/TravianBotSharp

echo "Smoke test passed"
