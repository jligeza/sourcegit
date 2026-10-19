#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
publish_dir=""
if [[ $# -gt 0 ]]; then
    if [[ $# != 2 || $1 != --from-publish ]]; then
        printf 'Usage: bash %s [--from-publish DIRECTORY]\n' "$0" >&2
        exit 2
    fi
    publish_dir="$(cd -- "$2" && pwd)"
fi

if [[ $(uname -s) != Linux ]]; then
    printf 'This installer is for Linux and WSL. Use the Windows archive on Windows.\n' >&2
    exit 1
fi

if [[ -z $publish_dir ]]; then
    case "$(uname -m)" in
        x86_64) runtime=linux-x64 ;;
        aarch64|arm64) runtime=linux-arm64 ;;
        *) printf 'Unsupported architecture.\n' >&2; exit 1 ;;
    esac
    dotnet_command="${DOTNET:-dotnet}"
    command -v "$dotnet_command" >/dev/null || { printf 'Install a .NET 10 SDK, or use --from-publish with a downloaded build.\n' >&2; exit 1; }
    [[ -f $repo_root/src/SourceGit.csproj ]] || { printf 'Run this installer from a source checkout, or provide --from-publish.\n' >&2; exit 1; }
    publish_dir="$(mktemp -d "${TMPDIR:-/tmp}/sourcegit-focused-publish.XXXXXX")"
    "$dotnet_command" publish "$repo_root/src/SourceGit.csproj" -c Release -r "$runtime" \
        --self-contained true -p:DisableAOT=true -p:DisableUpdateDetection=true -o "$publish_dir" --nologo
fi

[[ -x $publish_dir/SourceGit && -f $publish_dir/SourceGit.runtimeconfig.json ]] || {
    printf 'The publish directory must contain the self-contained Linux SourceGit build.\n' >&2
    exit 1
}

icon_source="$publish_dir/sourcegit.png"
if [[ ! -f $icon_source ]]; then
    icon_source="$repo_root/build/resources/_common/icons/sourcegit.png"
fi
[[ -f $icon_source ]] || { printf 'The SourceGit application icon is missing.\n' >&2; exit 1; }

install_root="$HOME/.local/opt/sourcegit-focused"
command_dir="$HOME/.local/bin"
desktop_dir="$HOME/.local/share/applications"
icon_dir="$HOME/.local/share/icons"
backup_root="$HOME/.local/state/sourcegit-focused/backups"
mkdir -p "$install_root/releases" "$command_dir" "$desktop_dir" "$icon_dir" "$backup_root"
if [[ -e $install_root/current && ! -L $install_root/current ]]; then
    printf 'Refusing to replace a non-symlink current installation.\n' >&2
    exit 1
fi

backup_dir="$(mktemp -d "$backup_root/install.XXXXXX")"
for entry in "$command_dir/sourcegit" "$desktop_dir/sourcegit.desktop" "$icon_dir/sourcegit-focused.png"; do
    if [[ -e $entry || -L $entry ]]; then
        cp -a -- "$entry" "$backup_dir/$(basename -- "$entry")"
    fi
done
preference_file="${XDG_CONFIG_HOME:-$HOME/.config}/SourceGit/preference.json"
if [[ -f $preference_file ]]; then
    cp -p -- "$preference_file" "$backup_dir/preference.json"
fi

# Install into a fresh directory: a running older build is never overwritten.
release_dir="$(mktemp -d "$install_root/releases/build.XXXXXX")"
cp -a -- "$publish_dir/." "$release_dir/"
install -m 644 -- "$icon_source" "$icon_dir/sourcegit-focused.png"
ln -s -- "$release_dir" "$install_root/.current-$$"
mv -Tf -- "$install_root/.current-$$" "$install_root/current"

launcher_temp="$(mktemp "$command_dir/.sourcegit.XXXXXX")"
cat > "$launcher_temp" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail
# Override this environment variable when another display setup needs a different scale.
export AVALONIA_SCREEN_SCALE_FACTORS="${AVALONIA_SCREEN_SCALE_FACTORS:-XWAYLAND0=1.75;XWAYLAND1=1.75}"
exec "$HOME/.local/opt/sourcegit-focused/current/SourceGit" "$@"
LAUNCHER
chmod 755 "$launcher_temp"
mv -f -- "$launcher_temp" "$command_dir/sourcegit"

desktop_temp="$(mktemp "$desktop_dir/.sourcegit.XXXXXX")"
{
    printf '[Desktop Entry]\nName=SourceGit\nComment=SourceGit fork with focused history layout\n'
    printf 'Exec="%s/sourcegit" %%u\n' "$command_dir"
    printf 'Icon=%s/sourcegit-focused.png\n' "$icon_dir"
    printf 'Terminal=false\nType=Application\nCategories=Development;\nMimeType=inode/directory;\n'
} > "$desktop_temp"
chmod 644 "$desktop_temp"
mv -f -- "$desktop_temp" "$desktop_dir/sourcegit.desktop"

if command -v update-desktop-database >/dev/null; then
    update-desktop-database "$desktop_dir"
fi

printf 'Installed: %s\nLauncher: %s/sourcegit\nLauncher backup: %s\n' "$release_dir" "$command_dir" "$backup_dir"
printf 'Existing SourceGit preferences and repositories are retained. Restart SourceGit to use this build.\n'
