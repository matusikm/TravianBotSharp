namespace MainCore
{
    /// <summary>
    /// Where TBS keeps its own files: TBS.db, logs/, Data/ (Chrome profiles), Screenshots/ and default-templates.csv.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// The app folder when it is writable, which keeps the portable zip and tarball working as before.
        /// When the app is installed read-only (the Linux .deb puts it in /opt/travianbotsharp), the files go
        /// to the user's data folder instead: ~/.local/share/TravianBotSharp on Linux.
        /// A macOS app bundle always uses ~/Library/Application Support/TravianBotSharp, because writing inside
        /// the bundle breaks its signature and replacing the bundle on update would delete the data.
        /// </summary>
        public static string DataDirectory { get; } = ResolveDataDirectory();

        private static string ResolveDataDirectory()
        {
            var appDirectory = AppContext.BaseDirectory;
            if (!IsInsideMacAppBundle(appDirectory) && IsWritable(appDirectory)) return appDirectory;

            var userData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
            var dataDirectory = Path.Combine(userData, "TravianBotSharp");
            Directory.CreateDirectory(dataDirectory);
            return dataDirectory;
        }

        // LocalApplicationData is ~/Library/Application Support on macOS.
        private static bool IsInsideMacAppBundle(string directory) =>
            OperatingSystem.IsMacOS()
            && directory.Contains(".app/Contents/", StringComparison.Ordinal);

        private static bool IsWritable(string directory)
        {
            try
            {
                var probe = Path.Combine(directory, $".write-test-{Environment.ProcessId}");
                using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }
}
