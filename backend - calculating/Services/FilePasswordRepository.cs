using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using backend___calculating.Interfaces;
using System.Linq;
using System.Collections.Concurrent;

namespace backend___calculating.Services
{
    public class FilePasswordRepository : IPasswordRepository, IHostedService
    {
        private readonly IEnumerable<ILogService> _logServices;
        private readonly string _passwordFilePath;
        private readonly Dictionary<string, string> _userPasswords = new(StringComparer.OrdinalIgnoreCase);

        public FilePasswordRepository(IEnumerable<ILogService> logServices)
        {
            _logServices = logServices;
            _passwordFilePath = Environment.GetEnvironmentVariable("PASSWORD_FILE_PATH") ?? "./data/users_passwords.txt";
            Task.Run(async () =>
            {
                try
                {
                    await Initialize();
                }
                catch (Exception ex)
                {
                    ILogService.LogError(_logServices, $"Failed to initialize repository: {ex.Message}");
                }
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Initialize();
            return;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task Initialize()
        {
            try
            {
                ILogService.LogInfo(_logServices, $"Initializing password repository from: {_passwordFilePath}");
                if (!File.Exists(_passwordFilePath))
                {
                    ILogService.LogError(_logServices, $"Password file not found at: {_passwordFilePath}");
                    throw new FileNotFoundException($"Password file not found: {_passwordFilePath}");
                }

                var fileInfo = new FileInfo(_passwordFilePath);
                ILogService.LogInfo(_logServices, $"Password file size: {fileInfo.Length} bytes");

                _userPasswords.Clear();
                int parsedCount = 0;

                // Read line by line instead of loading the entire file at once
                using (var streamReader = new StreamReader(_passwordFilePath))
                {
                    string? line;
                    while ((line = await streamReader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (!line.Contains(':'))
                        {
                            ILogService.LogInfo(_logServices, $"Skipping invalid line (no colon): '{line}'");
                            continue;
                        }

                        string[] parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            string username = parts[0].Trim();
                            string hash = parts[1].Trim();
                            if (parsedCount < 10)
                            {
                                ILogService.LogInfo(_logServices, $"Adding user: '{username}' with hash: '{hash}'");
                            }

                            _userPasswords[username] = hash;
                            parsedCount++;

                            // Log progress for large files
                            if (parsedCount % 1000000 == 0)
                            {
                                ILogService.LogInfo(_logServices, $"Processed {parsedCount} records so far...");
                            }
                        }
                    }
                }

                // Check if user1 exists
                if (_userPasswords.TryGetValue("user1", out string? user1Hash))
                {
                    ILogService.LogInfo(_logServices, $"user1 hash found: {user1Hash}");
                }
                else
                {
                    ILogService.LogError(_logServices, "user1 not found in the loaded dictionary!");
                }

                ILogService.LogInfo(_logServices, $"Loaded {parsedCount} valid user records from file");
            }
            catch (Exception ex)
            {
                ILogService.LogError(_logServices, $"Error initializing password repository: {ex.Message}");
                ILogService.LogError(_logServices, $"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        public Task<string?> GetPasswordHash(string username)
        {
            ILogService.LogInfo(_logServices, $"GetPasswordHash called for username: '{username}'");
            ILogService.LogInfo(_logServices, $"Dictionary contains {_userPasswords.Count} entries");

            if (_userPasswords.TryGetValue(username, out string? hash))
            {
                ILogService.LogInfo(_logServices, $"Found hash for '{username}': {hash}");
                return Task.FromResult<string?>(hash);
            }

            ILogService.LogError(_logServices, $"Hash not found for username: '{username}'");

            // Debug: List first few keys in dictionary
            var keys = _userPasswords.Keys.Take(10).ToList();
            ILogService.LogInfo(_logServices, $"First few keys in dictionary: {string.Join(", ", keys)}");

            return Task.FromResult<string?>(null);
        }

        public Task<bool> CheckPassword(string username, string hashedPassword)
        {
            ILogService.LogInfo(_logServices, $"CheckPassword called for '{username}' with hash '{hashedPassword}'");

            if (_userPasswords.TryGetValue(username, out string? storedHash))
            {
                bool isMatch = string.Equals(storedHash, hashedPassword, StringComparison.OrdinalIgnoreCase);
                ILogService.LogInfo(_logServices, $"Password check for '{username}': {isMatch} (stored: {storedHash})");
                return Task.FromResult(isMatch);
            }

            ILogService.LogError(_logServices, $"No stored hash found for '{username}' during password check");
            return Task.FromResult(false);
        }
    }
}