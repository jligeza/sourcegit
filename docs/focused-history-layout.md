# Focused history layout

Focused is an optional history layout. Choose **Focused** from the history
toolbar's advanced options under **Layout**. Existing stacked and side-by-side
layouts remain available, and the default is unchanged. Existing preferences migrate automatically.

## Navigation

- Select a commit to inspect its subject, author, SHA, and changed files on the
  right. Expand **Information** for the full commit information.
- Click a changed file to replace the central graph with its diff. The existing
  file-browser display switch supports tree, list, and grid views, including
  compact folders. The right panel is resizable; its width is saved per repository.
- Click **← Graph**, or press **Esc**, to return to the same graph and commit
  selection. File selection is retained; clicking that file again reopens its diff.
  The Graph button is at the right edge of the central diff header, next to the
  inspector, to keep it close to file navigation.
- The inspector's separate action row contains the standalone-window button.
  The commit subject is below it. Use the copy button beside the short SHA to
  copy the full hash without expanding Information. Expanded information uses
  vertically stacked labels and wrapping values so it fits narrow panels.
- In the file browser, **Enter**, **Space**, and navigation keys activate a single
  selected file. Folders and multiple selections do not activate diffs.
- An active search or menu handles **Esc** before history navigation does.
- Selecting two commits uses the same workflow for a revision comparison. Swap
  the endpoints from the inspector to reverse the comparison.

Automatic first-file selection, repository refreshes, and restoring selections
do not open the central diff. Changing the inspected commit or comparison range
returns to the graph. Repository tabs retain their history view models, so each
tab keeps its current graph/diff navigation state during the app session.

## Implementation

`Histories` owns the central presentation state. Explicit activation is a routed
event from `ChangeCollectionView`, separate from its two-way selection binding.
The existing `CommitDetail`, `RevisionCompare`, `ChangeCollectionView`, and
`DiffView` components are reused. `ChangeListLayout` presents the file browser
alone when the diff is hosted centrally. `FocusedCommitDetail` is the narrow
commit inspector. The central diff covers the graph without unloading it, so the
graph keeps its scroll position.

Preferences store an explicit layout enum. The former
`UseTwoColumnsLayoutInHistories` setting is still accepted when loading older
configuration files, with the explicit layout taking precedence. Focused panel
widths are independent of the existing layouts.

New strings use the existing localization resources and English fallback.
Working-copy staging is outside this change.

## Validation

With a .NET 10 SDK and initialized submodules:

```sh
dotnet build src/SourceGit.csproj
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj
dotnet format --verify-no-changes src/SourceGit.csproj
```

Tests run against temporary repositories and isolated preferences using
Avalonia's headless platform. They cover configuration migration, panel widths,
file activation in all three display modes, folder clicks, repeat activation,
graph return and keyboard focus, search dismissal, layout switching, revision
comparisons, rapid target changes, and renamed/deleted/binary files.

Set `SOURCEGIT_TEST_SCREENSHOTS` to an output directory to capture the actual
graph, diff, and comparison views during tests.
