using System;
using System.IO;

using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(SourceGit.Tests.TestApp))]

namespace SourceGit.Tests
{
    public static class TestApp
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            // Tests must never read or overwrite the user's SourceGit preferences.
            var root = Path.Combine(Path.GetTempPath(), "sourcegit-tests-" + Guid.NewGuid().ToString("N"));
            Native.OS.BasicDirectories.ConfigDir = Path.Combine(root, "config");
            Native.OS.BasicDirectories.CacheDir = Path.Combine(root, "cache");
            Directory.CreateDirectory(Native.OS.BasicDirectories.ConfigDir);
            Directory.CreateDirectory(Native.OS.BasicDirectories.CacheDir);
            return App.BuildAvaloniaApp().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
    }
}
