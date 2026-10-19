#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/sourcegit-install-test.XXXXXX")"
test_home="$test_root/home"
publish_dir="$test_root/publish"
# Keep the fixture independent of the caller's XDG configuration directory.
export XDG_CONFIG_HOME="$test_home/.config"
mkdir -p "$test_home/.local/bin" "$publish_dir"
mkdir -p "$test_home/.config/SourceGit"
printf '{"HistoryLayout":2}' > "$test_home/.config/SourceGit/preference.json"
printf '#!/bin/sh\nprintf "original launcher\\n"\n' > "$test_home/.local/bin/sourcegit"
chmod 755 "$test_home/.local/bin/sourcegit"
cp "$repo_root/build/resources/_common/icons/sourcegit.png" "$publish_dir/sourcegit.png"
printf '{}' > "$publish_dir/SourceGit.runtimeconfig.json"
cat > "$publish_dir/SourceGit" <<'APP'
#!/usr/bin/env bash
printf '%s\n' "$AVALONIA_SCREEN_SCALE_FACTORS" "$@"
APP
chmod 755 "$publish_dir/SourceGit"

HOME="$test_home" bash "$repo_root/tools/install-focused-linux.sh" --from-publish "$publish_dir"
test -x "$test_home/.local/opt/sourcegit-focused/current/SourceGit"
test -f "$test_home/.local/share/icons/sourcegit-focused.png"
grep -q 'Exec=' "$test_home/.local/share/applications/sourcegit.desktop"
result="$(HOME="$test_home" env -u AVALONIA_SCREEN_SCALE_FACTORS "$test_home/.local/bin/sourcegit" 'path with spaces')"
expected=$'XWAYLAND0=1.75;XWAYLAND1=1.75\npath with spaces'
[[ $result == "$expected" ]]
result="$(HOME="$test_home" AVALONIA_SCREEN_SCALE_FACTORS=2 "$test_home/.local/bin/sourcegit" repository)"
[[ $result == $'2\nrepository' ]]
backup="$(find "$test_home/.local/state/sourcegit-focused/backups" -name sourcegit -type f -print -quit)"
grep -q 'original launcher' "$backup"
cmp "$test_home/.config/SourceGit/preference.json" "$(dirname -- "$backup")/preference.json"

previous="$(readlink -f "$test_home/.local/opt/sourcegit-focused/current")"
HOME="$test_home" bash "$repo_root/tools/install-focused-linux.sh" --from-publish "$publish_dir"
[[ $(readlink -f "$test_home/.local/opt/sourcegit-focused/current") != "$previous" ]]
test -x "$previous/SourceGit"
printf 'Installer checks passed: launcher, scaling, arguments, backup, desktop entry, and safe replacement.\n'
