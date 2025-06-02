using System;
using System.Collections.Generic;
using System.IO;
using backend___central.Interfaces;

namespace backend___central.Services
{
    public static class PerformanceMetricsLogger
    {
        private static readonly object BruteForceFileLock = new();
        private static readonly object DictionaryFileLock = new();
        private static readonly object DictionaryChunkFileLock = new();

        private static readonly string BruteForceMetricsPath = Path.Combine(Directory.GetCurrentDirectory(), "bruteforce_metrics.csv");
        private static readonly string DictionaryMetricsPath = Path.Combine(Directory.GetCurrentDirectory(), "dictionary_metrics.csv");
        private static readonly string DictionaryChunkMetricsPath = Path.Combine(Directory.GetCurrentDirectory(), "dictionary_chunk_metrics.csv");

                public static void LogBruteForcePackageMetrics(
            IEnumerable<ILogService> logServices, 
            string userLogin, 
            int passwordLength,
            string charPackage,
            string serverIp,
            int processingTime,
            int totalTime,
            bool passwordFound,
            int granularity)
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "bruteforce_package_metrics.csv");
                bool fileExists = File.Exists(path);
                
                lock (BruteForceFileLock)
                {
                    using StreamWriter writer = new StreamWriter(path, true);
                    if (!fileExists)
                    {
                        writer.WriteLine("Timestamp,UserLogin,PasswordLength,CharPackage,ServerIp," +
                                       "ProcessingTime,TotalTime,PasswordFound,Granularity");
                    }
        
                    writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                                   $"{userLogin}," +
                                   $"{passwordLength}," +
                                   $"{charPackage}," +
                                   $"{serverIp}," +
                                   $"{processingTime}," +
                                   $"{totalTime}," +
                                   $"{passwordFound}," +
                                   $"{granularity}");
                }
                
                ILogService.LogInfo(logServices, $"Brute force package metrics saved to {path}");
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Failed to log brute force package metrics: {ex.Message}");
            }
        }
        
        public static void LogDictionaryChunkMetrics(
            IEnumerable<ILogService> logServices, 
            int chunkStart, 
            int chunkEnd, 
            string serverIp, 
            int processingTime, 
            int totalTime, 
            bool passwordFound)
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "dictionary_chunk_metrics.csv");
                bool fileExists = File.Exists(path);
                
                lock (DictionaryFileLock)
                {
                    using StreamWriter writer = new StreamWriter(path, true);
                    if (!fileExists)
                    {
                        writer.WriteLine("Timestamp,ChunkStart,ChunkEnd,ChunkSize,ServerIp," +
                                        "ProcessingTime,TotalTime,PasswordFound,Granularity");
                    }

                    int chunkSize = chunkEnd - chunkStart;
                    writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                                   $"{chunkStart}," +
                                   $"{chunkEnd}," +
                                   $"{chunkSize}," +
                                   $"{serverIp}," +
                                   $"{processingTime}," +
                                   $"{totalTime}," +
                                   $"{passwordFound}," +
                                   $"{Startup.DictionaryGranularity}");
                }
                
                // ILogService.LogInfo(logServices, $"Dictionary chunk metrics saved to {path}");
            }
            catch (Exception ex)
            {
                // ILogService.LogError(logServices, $"Failed to log dictionary chunk metrics: {ex.Message}");
            }
        }
        
    }
}