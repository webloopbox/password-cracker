using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace backend___central.Services
{
    public class ChunkManagerService
    {
        private readonly IEnumerable<ILogService> logServices;

        public ChunkManagerService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
        }

        public Chunk CreateChunk(int currentLine, int totalLines, DateTime firstDateTime)
        {
            return new Chunk(currentLine, Math.Min(currentLine + Startup.Granularity - 1, totalLines), firstDateTime);
        }

        public async Task<int> GetDictionaryTotalLines()
        {
            try
            {
                int lineCount = 0;
                string dictionaryPath = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                DirectoryInfo directory = new(dictionaryPath);
                if (!directory.Exists)
                {
                    throw new DirectoryNotFoundException($"Dictionary directory not found at: {dictionaryPath}");
                }
                FileInfo latestFile = directory.GetFiles()
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault() ?? throw new FileNotFoundException("No dictionary files found");
                using (StreamReader reader = new(latestFile.FullName))
                {
                    while (await reader.ReadLineAsync() != null)
                    {
                        lineCount++;
                    }
                }
                ILogService.LogInfo(logServices, $"Dictionary contains {lineCount} lines");
                return lineCount;
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error counting dictionary lines: {ex.Message}");
                throw new Exception($"Failed to get dictionary total lines: {ex.Message}", ex);
            }
        }
    }
}