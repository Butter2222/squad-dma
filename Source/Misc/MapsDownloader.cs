using squad_dma.Source.Misc;
using System.Diagnostics;
using System.IO.Compression;

namespace squad_dma
{
    public static class MapsDownloader
    {
        private const string MAPS_DOWNLOAD_URL = "https://github.com/Butter2222/squad-dma/releases/download/Maps/Maps.zip";
        private const string MAPS_FOLDER = "Maps";

        // One map pack folder per map: map.json, the base tiles and the height data
        private static readonly string[] RequiredMaps = new string[]
        {
            "AlBasrah", "Anvil", "Belaya", "BlackCoast", "Chora", "Fallujah",
            "FoolsRoad", "GooseBay", "Gorodok", "Harju", "Jensen",
            "Kamdesh", "Kohat", "Kokan", "Lashkar", "Logar",
            "Manicouagan", "Mestia", "Mutaha", "Narva", "Narva_f", "Pacific",
            "Sanxian", "Skorpo", "Sumari", "Tallil", "Yehorivka"
        };

        // Single-image maps from before map packs, still accepted until a Maps folder with packs replaces them
        private static readonly string[] LegacyMaps = new string[]
        {
            "Al_Basrah", "Anvil", "Belaya", "Black_Coast", "Chora", "Fallujah",
            "Fools_Road", "GooseBay", "Gorodok", "Harju", "Jensens_Range",
            "Kamdesh", "Kohat", "Kokan", "Lashkar", "Logar_Valley",
            "Manicouagan", "Mestia", "Mutaha", "Narva", "PacificProvingGrounds",
            "Sanxian_Islands", "Skorpo", "Sumari", "Tallil_Outskirts", "Yehorivka",
            "Al_Basrah_Old"
        };

        public static bool ValidateMapsFolder()
        {
            if (!Directory.Exists(MAPS_FOLDER))
                return false;

            var missingFiles = GetMissingMapFiles();
            return missingFiles.Count == 0;
        }

        public static List<string> GetMissingMapFiles()
        {
            var missing = new List<string>();

            if (!Directory.Exists(MAPS_FOLDER))
            {
                missing.Add("Maps folder does not exist");
                return missing;
            }

            // Only a folder with no map packs at all is judged as the old single-image layout
            if (!MapPack.FindPacks(MAPS_FOLDER).Any())
            {
                foreach (var mapName in LegacyMaps)
                {
                    if (!File.Exists(Path.Combine(MAPS_FOLDER, $"{mapName}.png")))
                        missing.Add($"{mapName}.png");
                }

                if (missing.Count == 0)
                    return missing;

                // Not a complete old set either: report what the new layout is missing
                missing.Clear();
            }

            foreach (var mapName in RequiredMaps)
            {
                string packDirectory = Path.Combine(MAPS_FOLDER, mapName);
                string problem = Directory.Exists(packDirectory) ? MapPack.Validate(packDirectory) : "folder missing";
                if (problem != null)
                    missing.Add($"{mapName}: {problem}");
            }

            return missing;
        }

        public static async Task<bool> DownloadMapsAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken = default)
        {
            const int MAX_RETRIES = 5;
            const int BUFFER_SIZE = 81920; // 80KB buffer for better performance
            string tempFile = Path.Combine(Path.GetTempPath(), "maps.zip");

            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    Logger.Info($"Download attempt {attempt}/{MAX_RETRIES}...");

                    // Configure HttpClient with longer timeout and keep-alive
                    using (var handler = new HttpClientHandler())
                    {
                        handler.MaxConnectionsPerServer = 1;
                        
                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromMinutes(60); // Increased timeout
                            client.DefaultRequestHeaders.ConnectionClose = false; // Keep connection alive

                            using (var response = await client.GetAsync(MAPS_DOWNLOAD_URL, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                            {
                                response.EnsureSuccessStatusCode();

                                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                                var stopwatch = Stopwatch.StartNew();
                                var overallStopwatch = Stopwatch.StartNew();
                                long lastBytesRead = 0;
                                long bytesRead = 0;

                                using (var contentStream = await response.Content.ReadAsStreamAsync())
                                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, true))
                                {
                                    var buffer = new byte[BUFFER_SIZE];
                                    int read;

                                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                    {
                                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                                        bytesRead += read;

                                        // Update progress every 100ms
                                        if (stopwatch.ElapsedMilliseconds >= 100)
                                        {
                                            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                            var bytesInInterval = bytesRead - lastBytesRead;
                                            var speed = elapsedSeconds > 0 ? bytesInInterval / elapsedSeconds : 0;

                                            // Calculate average speed over entire download for more accurate ETA
                                            var overallSpeed = overallStopwatch.Elapsed.TotalSeconds > 0 
                                                ? bytesRead / overallStopwatch.Elapsed.TotalSeconds 
                                                : speed;

                                            progress?.Report(new DownloadProgress
                                            {
                                                BytesDownloaded = bytesRead,
                                                TotalBytes = totalBytes,
                                                SpeedBytesPerSec = overallSpeed // Use average speed for more stable display
                                            });

                                            lastBytesRead = bytesRead;
                                            stopwatch.Restart();
                                        }
                                    }

                                    // Final progress update
                                    progress?.Report(new DownloadProgress
                                    {
                                        BytesDownloaded = bytesRead,
                                        TotalBytes = totalBytes,
                                        SpeedBytesPerSec = 0
                                    });
                                }
                            }
                        }
                    }

                    Logger.Info("Download complete - extracting archive...");

                    // Extract the archive
                    if (!ExtractMapsArchive(tempFile))
                    {
                        Logger.Error("Extraction failed");
                        throw new Exception("Failed to extract maps archive");
                    }

                    // Verify extraction
                    if (ValidateMapsFolder())
                    {
                        Logger.Info("Maps validated successfully after download");
                        return true;
                    }
                    else
                    {
                        Logger.Error("Maps validation failed after extraction");
                        throw new Exception("Downloaded maps validation failed");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Download attempt {attempt} failed: {ex.Message}");

                    // Clean up partial download
                    try
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                    catch { }

                    // If this was the last attempt, return false
                    if (attempt >= MAX_RETRIES)
                    {
                        Logger.Error($"All {MAX_RETRIES} download attempts failed");
                        return false;
                    }

                    // Exponential backoff: 2^attempt seconds (2s, 4s, 8s, 16s)
                    var delaySeconds = Math.Pow(2, attempt);
                    Logger.Info($"Retrying in {delaySeconds} seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }

            return false;
        }

        public static bool ExtractMapsArchive(string archivePath)
        {
            try
            {
                // Create Maps directory if it doesn't exist
                if (!Directory.Exists(MAPS_FOLDER))
                    Directory.CreateDirectory(MAPS_FOLDER);

                string root = Path.GetFullPath(MAPS_FOLDER) + Path.DirectorySeparatorChar;

                // Extract all files, keeping each map's folder structure
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        // The archive may or may not have a leading "Maps/" folder
                        string relative = entry.FullName.Replace('\\', '/');
                        if (relative.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                            relative = relative.Substring("Maps/".Length);

                        string destinationPath = Path.GetFullPath(Path.Combine(MAPS_FOLDER, relative));
                        if (!destinationPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException($"Archive entry outside the Maps folder: {entry.FullName}");

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                        // Overwrite existing files
                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Extraction error: {ex.Message}");
                return false;
            }
        }
    }
}
