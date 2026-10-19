# Install this SourceGit fork

This branch adds the optional Focused history layout. It preserves the existing
SourceGit settings and repository list. The Linux launcher uses 175% scaling for
the WSL displays `XWAYLAND0` and `XWAYLAND1`; an explicitly supplied
`AVALONIA_SCREEN_SCALE_FACTORS` overrides that default.

## Linux / WSL: install from source

Install a .NET 10 SDK and Git, then run:

```sh
git clone --branch feature/focused-history-layout --recurse-submodules https://github.com/jligeza/sourcegit.git
cd sourcegit
bash tools/install-focused-linux.sh
```

The installer publishes a self-contained app, so subsequent launches do not need
the SDK. Run `~/.local/bin/sourcegit`, or use the SourceGit application shortcut.
The app is stored under `~/.local/opt/sourcegit-focused`, independently of the
checkout. The previous command and desktop shortcut are backed up under
`~/.local/state/sourcegit-focused/backups`. Each install uses a fresh release
directory, preserving a running older binary.

If a distribution package for SourceGit is already installed, it can be removed
after verifying the new launcher. The user shortcut keeps the same desktop ID,
`sourcegit.desktop`, so existing WSLg / Windows shortcuts to
`~/.local/bin/sourcegit` continue working. Existing configuration is not deleted.

## Download a build without a local SDK

Open the repository's **Actions → Focused fork builds** page and select a
successful run for this branch. Download the artifact for the target system.
Artifacts are kept for 90 days; run the workflow again when a fresh build is needed.

For Linux / WSL, extract the artifact ZIP, then the included `.tar.gz`. From the
extracted directory run:

```sh
bash install-focused-linux.sh --from-publish ./SourceGit
```

For Windows, extract the artifact ZIP and the included build ZIP, then run
`SourceGit/SourceGit.exe`. Windows uses its normal display scaling.

On a new installation choose **Focused** in the history layout menu. The other
layouts remain available.

## Update an existing source installation

```sh
git pull --ff-only
git submodule update --init --recursive
bash tools/install-focused-linux.sh
```

Close and reopen SourceGit after updating. Fork builds disable upstream update
notifications so they are not mistaken for newer builds of this fork.
