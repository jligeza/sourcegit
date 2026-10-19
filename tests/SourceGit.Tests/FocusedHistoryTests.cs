using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using SourceGit.Models;
using SourceGit.ViewModels;

using Xunit;

namespace SourceGit.Tests
{
    public class FocusedHistoryTests
    {
        [Theory]
        [InlineData(false, HistoryLayout.Stacked)]
        [InlineData(true, HistoryLayout.SideBySide)]
        public void LegacyPreferencesKeepTheirLayout(bool horizontal, HistoryLayout expected)
        {
            var pref = JsonSerializer.Deserialize<Preferences>("{\"UseTwoColumnsLayoutInHistories\":" + horizontal.ToString().ToLowerInvariant() + "}");
            Assert.Equal(expected, pref.HistoryLayout);
        }

        [Theory]
        [InlineData("{\"HistoryLayout\":2,\"UseTwoColumnsLayoutInHistories\":false}")]
        [InlineData("{\"UseTwoColumnsLayoutInHistories\":true,\"HistoryLayout\":2}")]
        public void ExplicitLayoutWinsRegardlessOfJsonPropertyOrder(string json)
        {
            var pref = JsonSerializer.Deserialize<Preferences>(json);
            Assert.Equal(HistoryLayout.Focused, pref.HistoryLayout);
            Assert.True(pref.IsHorizontalHistoryLayout);
        }

        [Fact]
        public void InvalidLayoutFallsBackToLegacyPreference()
        {
            var pref = JsonSerializer.Deserialize<Preferences>("{\"HistoryLayout\":99,\"UseTwoColumnsLayoutInHistories\":true}");
            Assert.Equal(HistoryLayout.SideBySide, pref.HistoryLayout);
        }

        [AvaloniaFact]
        public void FileOnlyLayoutDoesNotOverwriteEmbeddedPanelWidth()
        {
            var layout = new Views.ChangeListLayout { ListWidth = new GridLength(290) };
            layout.IsFileListOnly = true;
            Assert.True(layout.ColumnDefinitions[0].Width.IsStar);
            Assert.Equal(0, layout.ColumnDefinitions[2].Width.Value);
            Assert.Equal(290, layout.ListWidth.Value);
            layout.IsFileListOnly = false;
            Assert.Equal(290, layout.ColumnDefinitions[0].Width.Value);
            layout.ColumnDefinitions[0].Width = new GridLength(310);
            Assert.Equal(310, layout.ListWidth.Value);
        }

        [AvaloniaFact]
        public async Task AutomaticSelectionDoesNotOpenDiffAndSameFileCanBeReopened()
        {
            using var fixture = new RepositoryFixture();
            var histories = fixture.CreateHistory();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            histories.SelectedCommits = [fixture.Tip];
            var detail = Assert.IsType<CommitDetail>(histories.DetailContext);
            await WaitUntil(() => detail.ChangeSelection.Count == 1);
            Assert.False(histories.IsFocusedDiffVisible);
            histories.OpenFocusedDiff();
            Assert.True(histories.IsFocusedDiffVisible);
            var selection = detail.ChangeSelection;
            histories.ShowGraph();
            Assert.Same(selection, detail.ChangeSelection);
            histories.OpenFocusedDiff();
            Assert.True(histories.IsFocusedDiffVisible);
            histories.SelectedCommits = [fixture.Base];
            Assert.False(histories.IsFocusedDiffVisible);
        }

        [AvaloniaFact]
        public async Task TwoCommitComparisonAndSwapReturnToGraph()
        {
            using var fixture = new RepositoryFixture();
            var histories = fixture.CreateHistory();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            histories.SelectedCommits = [fixture.Tip, fixture.Base];
            var compare = Assert.IsType<RevisionCompare>(histories.DetailContext);
            await WaitUntil(() => !compare.IsLoading);
            Assert.False(histories.IsFocusedDiffVisible);
            histories.OpenFocusedDiff();
            Assert.True(histories.IsFocusedDiffVisible);
            compare.Swap();
            Assert.False(histories.IsFocusedDiffVisible);
            await WaitUntil(() => !compare.IsLoading);
            histories.OpenFocusedDiff();
            Assert.True(histories.IsFocusedDiffVisible);
        }

        [AvaloniaFact]
        public void WidthIsSeparateForFocusedLayoutAndSavedPerRepository()
        {
            using var fixture = new RepositoryFixture();
            var histories = fixture.CreateHistory();
            Preferences.Instance.HistoryLayout = HistoryLayout.SideBySide;
            histories.RightArea = new GridLength(450);
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            histories.RightArea = new GridLength(320);
            Assert.Equal(320, fixture.Repository.UIStates.FocusedHistoryDetailsWidth);
            Preferences.Instance.HistoryLayout = HistoryLayout.SideBySide;
            Assert.Equal(450, histories.RightArea.Value);
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            Assert.Equal(320, histories.RightArea.Value);
        }

        [AvaloniaTheory]
        [InlineData(ChangeViewMode.List)]
        [InlineData(ChangeViewMode.Tree)]
        [InlineData(ChangeViewMode.Grid)]
        public void FileActivationComesFromInputIncludingRepeatedClicks(ChangeViewMode mode)
        {
            var change = new Change { Path = "src/example.cs", Index = ChangeState.Modified };
            var view = new Views.ChangeCollectionView { ViewMode = mode, Changes = [change] };
            var activations = 0;
            view.ChangeActivated += (_, _) => activations++;
            var window = new Window { Width = 500, Height = 350, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                view.Selection = new ChangeSelection(new Change[] { change });
                Assert.Equal(0, activations);
                var row = window.GetVisualDescendants().OfType<ListBoxItem>().First(x =>
                    x.DataContext is Change || x.DataContext is ChangeTreeNode { IsFolder: false });
                var point = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window).Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.Equal(2, activations);
                if (mode == ChangeViewMode.Tree)
                {
                    var folder = window.GetVisualDescendants().OfType<ListBoxItem>().First(x => x.DataContext is ChangeTreeNode { IsFolder: true });
                    var folderPoint = folder.TranslatePoint(new Point(8, folder.Bounds.Height / 2), window).Value;
                    window.MouseDown(folderPoint, MouseButton.Left);
                    window.MouseUp(folderPoint, MouseButton.Left);
                    Assert.Equal(2, activations);
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public async Task GraphDiffAndEscapePreserveSelectionAndGraphInstance()
        {
            using var fixture = new RepositoryFixture();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            Preferences.Instance.ChangeViewMode = ChangeViewMode.Tree;
            var histories = fixture.CreateHistory();
            histories.SelectedCommits = [fixture.Tip];
            var view = new Views.Histories { DataContext = histories };
            var window = new Window { Width = 1100, Height = 700, Content = view };
            try
            {
                window.Show();
                var detail = Assert.IsType<CommitDetail>(histories.DetailContext);
                await WaitUntil(() => detail.ChangeSelection.Count == 1);
                Dispatcher.UIThread.RunJobs();
                var graph = window.GetVisualDescendants().OfType<Views.HistoriesCommitList>().Single();
                SaveScreenshot(window, "focused-graph.png");
                var file = window.GetVisualDescendants().OfType<ListBoxItem>().Single(x =>
                    x.DataContext is ChangeTreeNode { IsFolder: false } node && node.Change.Path.EndsWith(".cs", StringComparison.Ordinal));
                var point = file.TranslatePoint(new Point(file.Bounds.Width / 2, file.Bounds.Height / 2), window).Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.True(histories.IsFocusedDiffVisible);
                await WaitUntil(() => histories.FocusedDiff.Content != null);
                Dispatcher.UIThread.RunJobs();
                SaveScreenshot(window, "focused-diff.png");
                var back = window.GetVisualDescendants().OfType<Button>().Single(x => x.Content as string == "← Graph");
                back.Focus();
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Assert.False(histories.IsFocusedDiffVisible);
                Assert.Same(graph, window.GetVisualDescendants().OfType<Views.HistoriesCommitList>().Single());
                Assert.True(graph.IsKeyboardFocusWithin);
                Assert.Equal(fixture.Tip.SHA, histories.SelectedCommits.Single().SHA);
                // A click on the same file must open it again without changing selection.
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.True(histories.IsFocusedDiffVisible);

                // Switching layouts shows exactly one of the two inspectors.
                Preferences.Instance.HistoryLayout = HistoryLayout.SideBySide;
                Dispatcher.UIThread.RunJobs();
                Assert.True(window.GetVisualDescendants().OfType<Views.CommitDetail>().Single().IsEffectivelyVisible);
                Assert.False(window.GetVisualDescendants().OfType<Views.FocusedCommitDetail>().Single().IsEffectivelyVisible);
                Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
                Dispatcher.UIThread.RunJobs();
                Assert.False(window.GetVisualDescendants().OfType<Views.CommitDetail>().Single().IsEffectivelyVisible);
                Assert.True(window.GetVisualDescendants().OfType<Views.FocusedCommitDetail>().Single().IsEffectivelyVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public async Task FocusedInspectorKeepsActionsSeparateAndCopiesTheFullHash()
        {
            await CheckFocusedInspector(340, 1);
        }

        [AvaloniaTheory]
        [InlineData(260, 1.75)]
        [InlineData(340, 1.75)]
        public async Task FocusedInspectorFitsNarrowPanelsAt175PercentScale(double width, double scale)
        {
            await CheckFocusedInspector(width, scale);
        }

        private static async Task CheckFocusedInspector(double width, double scale)
        {
            using var fixture = new RepositoryFixture();
            App.SetTheme("Dark", string.Empty);
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            Preferences.Instance.ChangeViewMode = ChangeViewMode.Tree;
            fixture.Tip.Subject = "Remove the unused hidden-ref maintenance counter and explain the background cleanup behavior";
            fixture.Tip.Author = new User(string.Empty) { Name = "Test Author", Email = "test.author.with.a.long.email.address@example.com" };
            var histories = fixture.CreateHistory();
            histories.RightArea = new GridLength(width);
            histories.SelectedCommits = [fixture.Tip];
            var view = new Views.Histories { DataContext = histories };
            var window = new Window
            {
                Width = 1100 * scale,
                Height = 700 * scale,
                Content = new LayoutTransformControl { Child = view, LayoutTransform = new ScaleTransform(scale, scale) },
            };
            try
            {
                window.Show();
                var detail = Assert.IsType<CommitDetail>(histories.DetailContext);
                await WaitUntil(() => detail.ChangeSelection.Count == 1 && detail.FullMessage != null);
                Dispatcher.UIThread.RunJobs();
                var inspector = window.GetVisualDescendants().OfType<Views.FocusedCommitDetail>().Single();
                var controls = inspector.GetVisualDescendants().OfType<Control>().ToList();
                var subject = controls.OfType<TextBlock>().First(x => x.Text == fixture.Tip.Subject);
                var open = window.GetVisualDescendants().OfType<Button>().Single(x => x.HotKey?.Key == Key.N);
                Assert.True(WindowBounds(open, window).Bottom <= WindowBounds(subject, window).Top);

                var copy = controls.OfType<Views.CopyButton>().Single();
                var authorBounds = WindowBounds(controls.OfType<TextBlock>().First(x => x.Text == "Test Author"), window);
                var shaBounds = WindowBounds(controls.OfType<SelectableTextBlock>().Single(x => x.Text == fixture.Tip.SHA[..10]), window);
                Assert.InRange(Math.Abs(authorBounds.Center.Y - shaBounds.Center.Y), 0, scale);
                Assert.InRange(Math.Abs(WindowBounds(copy, window).Center.Y - shaBounds.Center.Y), 0, scale);
                Assert.True(copy.IsEffectivelyVisible);
                Assert.Equal(fixture.Tip.SHA, copy.CopyText);
                var copyPoint = WindowBounds(copy, window).Center;
                window.MouseDown(copyPoint, MouseButton.Left);
                window.MouseUp(copyPoint, MouseButton.Left);
                await WaitUntil(() => copy.IsCopied);
                Assert.Equal(fixture.Tip.SHA, await window.Clipboard.TryGetTextAsync());

                var expander = controls.OfType<Expander>().Single();
                expander.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                var info = inspector.GetVisualDescendants().OfType<Views.CommitBaseInfo>().Single();
                Assert.True(info.IsCompact);
                var hash = info.GetVisualDescendants().OfType<SelectableTextBlock>().Single(x => x.Text == fixture.Tip.SHA);
                var message = info.GetVisualDescendants().OfType<Views.CommitMessagePresenter>().Single();
                Assert.Same(detail.FullMessage, message.FullMessage);
                Assert.Equal(fixture.Tip.SHA, hash.Text);
                Assert.True(hash.Bounds.Width <= width);
                var email = info.GetVisualDescendants().OfType<SelectableTextBlock>().Single(x => x.Text == fixture.Tip.Author.Email);
                Assert.True(email.Bounds.Height > 20);
                Assert.True(WindowBounds(hash, window).Right <= WindowBounds(info, window).Right + 1);
                var name = $"focused-information-{width}-{scale * 100}.png";
                SaveScreenshot(window, name);

                view.OpenFocusedDiff();
                await WaitUntil(() => histories.FocusedDiff.Content != null);
                Dispatcher.UIThread.RunJobs();
                var back = window.GetVisualDescendants().OfType<Button>().Single(x => x.Content as string == "← Graph");
                var gap = WindowBounds(inspector, window).Left - WindowBounds(back, window).Right;
                Assert.InRange(gap, 0, 24 * scale);
                Assert.True(WindowBounds(back, window).Center.X > window.Width / 2);
                SaveScreenshot(window, $"focused-diff-information-{width}-{scale * 100}.png");

                histories.SelectedCommits = [fixture.Base];
                Assert.Equal(fixture.Base.SHA, copy.CopyText);
            }
            finally
            {
                window.Close();
            }
        }

        private static Rect WindowBounds(Control control, Window window)
        {
            var topLeft = control.TranslatePoint(default, window).Value;
            var bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), window).Value;
            return new Rect(topLeft, bottomRight);
        }

        [AvaloniaFact]
        public async Task ReturningFromDiffPreservesAScrolledGraphAndRefreshDoesNotOpenDiff()
        {
            using var fixture = new RepositoryFixture();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            var histories = fixture.CreateHistory();
            var commits = Enumerable.Range(0, 70).Select(i => new Commit
            {
                SHA = (1000 + i).ToString("x40", System.Globalization.CultureInfo.InvariantCulture),
                Subject = "Earlier change " + i,
                Parents = [i == 69 ? fixture.Tip.SHA : (1001 + i).ToString("x40", System.Globalization.CultureInfo.InvariantCulture)],
            }).ToList();
            commits.Add(fixture.Tip);
            commits.Add(fixture.Base);
            histories.Commits = commits;
            histories.SelectedCommits = [fixture.Tip];
            var view = new Views.Histories { DataContext = histories };
            var window = new Window { Width = 1100, Height = 700, Content = view };
            try
            {
                window.Show();
                var detail = Assert.IsType<CommitDetail>(histories.DetailContext);
                await WaitUntil(() => detail.ChangeSelection.Count == 1);
                Dispatcher.UIThread.RunJobs();
                var graph = window.GetVisualDescendants().OfType<Views.HistoriesCommitList>().Single();
                var scroll = graph.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().First(x => x.Orientation == Avalonia.Layout.Orientation.Vertical);
                var offset = scroll.Value;
                Assert.True(offset > 0);
                view.OpenFocusedDiff();
                Dispatcher.UIThread.RunJobs();
                var back = window.GetVisualDescendants().OfType<Button>().Single(x => x.Content as string == "← Graph");
                back.Focus();
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(offset, scroll.Value);

                view.OpenFocusedDiff();
                histories.Commits = commits.ToList();
                Assert.True(histories.IsFocusedDiffVisible);
                histories.ShowGraph();
                histories.Commits = commits.ToList();
                Assert.False(histories.IsFocusedDiffVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public async Task ComparisonInspectorOpensTheCentralDiffFromTheFileTree()
        {
            using var fixture = new RepositoryFixture();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            Preferences.Instance.ChangeViewMode = ChangeViewMode.Tree;
            var histories = fixture.CreateHistory();
            histories.SelectedCommits = [fixture.Tip, fixture.Base];
            var compare = Assert.IsType<RevisionCompare>(histories.DetailContext);
            var view = new Views.Histories { DataContext = histories };
            var window = new Window { Width = 1100, Height = 700, Content = view };
            try
            {
                window.Show();
                await WaitUntil(() => !compare.IsLoading);
                Dispatcher.UIThread.RunJobs();
                Assert.True(window.GetVisualDescendants().OfType<Views.ChangeListLayout>().Single().Bounds.Height > 500);
                var row = window.GetVisualDescendants().OfType<ListBoxItem>().First(x => x.DataContext is ChangeTreeNode { IsFolder: false });
                var point = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window).Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.True(histories.IsFocusedDiffVisible);
                await WaitUntil(() => histories.FocusedDiff.Content != null);
                SaveScreenshot(window, "focused-comparison.png");
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public async Task RenamedDeletedAndBinaryFilesReuseTheExistingDiffEngine()
        {
            using var fixture = new RepositoryFixture();
            var commit = fixture.AddEdgeCaseCommit();
            var histories = fixture.CreateHistory();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            histories.SelectedCommits = [commit];
            var detail = Assert.IsType<CommitDetail>(histories.DetailContext);
            await WaitUntil(() => detail.Changes?.Count == 3);
            Assert.Contains(detail.Changes, x => x.Index == ChangeState.Renamed);
            Assert.Contains(detail.Changes, x => x.Index == ChangeState.Deleted);
            foreach (var change in detail.Changes)
            {
                detail.ChangeSelection = new ChangeSelection(new Change[] { change });
                histories.OpenFocusedDiff();
                Assert.True(histories.IsFocusedDiffVisible);
                await WaitUntil(() => histories.FocusedDiff.Content != null);
                if (change.Path.EndsWith(".bin", StringComparison.Ordinal))
                    await WaitUntil(() => histories.FocusedDiff.Content is BinaryDiff);
                histories.ShowGraph();
            }
        }

        [AvaloniaFact]
        public async Task ComparisonUsesTheLatestTargetsAfterRapidSelectionChanges()
        {
            using var fixture = new RepositoryFixture();
            var newest = fixture.AddEdgeCaseCommit();
            var histories = fixture.CreateHistory();
            Preferences.Instance.HistoryLayout = HistoryLayout.Focused;
            histories.SelectedCommits = [fixture.Tip, fixture.Base];
            var compare = Assert.IsType<RevisionCompare>(histories.DetailContext);
            histories.SelectedCommits = [newest, fixture.Tip];
            compare.SearchFilter = "data.bin";
            await WaitUntil(() => !compare.IsLoading);
            Assert.Equal(newest.SHA, Assert.IsType<Commit>(compare.EndPoint).SHA);
            Assert.Contains(compare.VisibleChanges, x => x.Path == "data.bin");
            Assert.Single(compare.VisibleChanges);
            Assert.False(histories.IsFocusedDiffVisible);
            histories.OpenFocusedDiff();
            Assert.True(histories.IsFocusedDiffVisible);
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(10))
                await Task.Delay(20);
            Assert.True(condition(), "Timed out waiting for repository data");
        }

        private static void SaveScreenshot(Window window, string name)
        {
            var directory = Environment.GetEnvironmentVariable("SOURCEGIT_TEST_SCREENSHOTS");
            if (string.IsNullOrEmpty(directory))
                return;
            Directory.CreateDirectory(directory);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame();
            Assert.NotNull(bitmap);
            bitmap.Save(Path.Combine(directory, name));
        }

        private sealed class RepositoryFixture : IDisposable
        {
            public readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sourcegit-repository-test-" + Guid.NewGuid().ToString("N"));
            public Repository Repository { get; }
            public Commit Base { get; }
            public Commit Tip { get; }

            public RepositoryFixture()
            {
                Directory.CreateDirectory(Path);
                Git("init", "-b", "main");
                Directory.CreateDirectory(System.IO.Path.Combine(Path, "src"));
                File.WriteAllText(System.IO.Path.Combine(Path, "src/example.cs"), "class Example\n{\n    public int Value => 1;\n}\n");
                Git("add", ".");
                Git("-c", "user.name=Test Author", "-c", "user.email=test@example.com", "commit", "-m", "Add an example");
                var first = Git("rev-parse", "HEAD");
                File.WriteAllText(System.IO.Path.Combine(Path, "src/example.cs"), "class Example\n{\n    public int Value => 2;\n}\n");
                File.WriteAllText(System.IO.Path.Combine(Path, "README.md"), "Example repository\n");
                Git("add", ".");
                Git("-c", "user.name=Test Author", "-c", "user.email=test@example.com", "commit", "-m", "Update the example and documentation");
                var last = Git("rev-parse", "HEAD");
                Repository = new Repository(false, Path, System.IO.Path.Combine(Path, ".git"));
                var author = new User(string.Empty) { Name = "Test Author" };
                var time = (ulong)DateTimeOffset.Parse("2026-10-18T10:42:00+02:00", System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeSeconds();
                Base = new Commit { SHA = first, Subject = "Add an example", Author = author, Committer = author, AuthorTime = time, CommitterTime = time };
                Tip = new Commit { SHA = last, Subject = "Update the example and documentation", Parents = [first], Author = author, Committer = author, AuthorTime = time, CommitterTime = time };
            }

            public Commit AddEdgeCaseCommit()
            {
                Git("mv", "src/example.cs", "src/renamed.cs");
                Git("rm", "README.md");
                File.WriteAllBytes(System.IO.Path.Combine(Path, "data.bin"), new byte[] { 0, 1, 0, 2 });
                Git("add", ".");
                Git("-c", "user.name=Test Author", "-c", "user.email=test@example.com", "commit", "-m", "Rename a file and add binary data");
                return new Commit { SHA = Git("rev-parse", "HEAD"), Parents = [Tip.SHA] };
            }

            public Histories CreateHistory() => new(Repository) { Commits = [Tip, Base], IsLoading = false };

            private string Git(params string[] args)
            {
                var start = new ProcessStartInfo("git") { WorkingDirectory = Path, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in args)
                    start.ArgumentList.Add(arg);
                using var process = Process.Start(start);
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, error);
                return output.Trim();
            }

            public void Dispose()
            {
                // Async diff jobs may still be reading this isolated temporary repository.
                // Leave cleanup to the operating system rather than race those readers.
            }
        }
    }
}
