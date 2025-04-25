using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;
using Npgsql;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace backend___calculating.Services
{
    public class BruteForceService : IBruteForceService
    {
        private readonly IEnumerable<ILogService> logServices;

        public BruteForceService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
        }

        public async Task<IActionResult> SynchronizeBruteForce(HttpContext httpContext)
        {
            DateTime startTime = DateTime.UtcNow;
            ILogService.LogInfo(logServices, "Starting brute force cracking");

            if (httpContext == null || httpContext.Request?.Body == null)
            {
                ILogService.LogError(logServices, "HttpContext or Request.Body is null");
                return new BadRequestObjectResult(new { Message = "HttpContext or Request.Body is null.", Time = -1 });
            }

            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();

            Console.WriteLine($"Request body content from calculating host: {bodyContent}");
            ILogService.LogInfo(logServices, $"Request body content: {bodyContent}");

            try
            {
                var requestData = JsonSerializer.Deserialize<BruteForceRequest>(bodyContent);
                if (requestData == null || string.IsNullOrEmpty(requestData.userLogin))
                {
                    ILogService.LogError(logServices, "Invalid request data");
                    return new BadRequestObjectResult(new { Message = "Invalid request data.", Time = -1 });
                }

                string? hash = await GetHashFromDatabase(requestData.userLogin);
                if (string.IsNullOrEmpty(hash))
                {
                    ILogService.LogError(logServices, $"Hash for user login '{requestData.userLogin}' not found.");
                    return new NotFoundObjectResult(new { Message = $"Hash for user login '{requestData.userLogin}' not found.", Time = -1 });
                }

                ILogService.LogInfo(logServices, $"Retrieved hash for user '{requestData.userLogin}': {hash}");
                Console.WriteLine($"Retrieved hash for user '{requestData.userLogin}': {hash}");

                // Record the time just before starting the actual brute force calculation
                DateTime calculationStartTime = DateTime.UtcNow;
                int setupTime = (int)(calculationStartTime - startTime).TotalMilliseconds;
                ILogService.LogInfo(logServices, $"Setup completed in {setupTime}ms");

                // Perform brute-force cracking
                string? foundPassword = PerformBruteForce(requestData.Chars, requestData.PasswordLength, hash);

                DateTime endTime = DateTime.UtcNow;
                int totalTime = (int)(endTime - startTime).TotalMilliseconds;
                int calculationTime = (int)(endTime - calculationStartTime).TotalMilliseconds;
                int communicationTime = totalTime - calculationTime;

                ILogService.LogInfo(logServices, $"[BruteForce] Total = {totalTime} ms | " +  $"Calculation = {calculationTime} ms | " +  $"Communication time = {communicationTime} ms");

                if (foundPassword != null)
                {
                    ILogService.LogInfo(logServices, $"Password found: {foundPassword}");
                    Console.WriteLine($"Password found: {foundPassword}");
                    return new OkObjectResult(new { 
                        Message = "Password found.", 
                        Password = foundPassword, 
                        Time = totalTime,
                        CalculationTime = calculationTime
                    });
                }
                else
                {
                    ILogService.LogInfo(logServices, "Password not found in the given range.");
                    Console.WriteLine("Password not found in the given range.");
                    return new OkObjectResult(new
                    {
                        Message = "Password not found.",
                        Time = totalTime,
                        CalculationTime = calculationTime
                    });
                }
            }
            catch (Exception ex)
            {
                DateTime endTime = DateTime.UtcNow;
                int totalTime = (int)(endTime - startTime).TotalMilliseconds;

                ILogService.LogError(logServices, $"Error during brute force: {ex.Message}");
                Console.WriteLine($"Error during brute force: {ex.Message}");

                return new ObjectResult(new
                {
                    Message = $"An error occurred during brute force: {ex.Message}",
                    Time = totalTime
                })
                {
                    StatusCode = 500
                };
            }
        }

        private async Task<string?> GetHashFromDatabase(string userLogin)
        {
            try
            {
                string? connectionString = Environment.GetEnvironmentVariable("POSTGRES_DB_CONNECTION_STRING");
                if (string.IsNullOrEmpty(connectionString))
                {
                    ILogService.LogError(logServices, "Database connection string is not set.");
                    throw new Exception("Database connection string is not set.");
                }

                using NpgsqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                string query = "SELECT password FROM users WHERE login = @login LIMIT 1;";
                using NpgsqlCommand command = new(query, connection);
                command.Parameters.AddWithValue("login", userLogin);

                ILogService.LogInfo(logServices, $"Executing query for user: {userLogin}");
                object? result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error fetching hash from database: {ex.Message}");
                Console.WriteLine($"Error fetching hash from database: {ex.Message}");
                return null;
            }
        }

        private string? PerformBruteForce(string chars, int passwordLength, string targetHash)
        {
            DateTime bruteForceStartTime = DateTime.UtcNow;
            ILogService.LogInfo(logServices, $"Starting brute force with chars: '{chars}', length: {passwordLength}, targetHash: {targetHash}");
            Console.WriteLine($"Starting brute force with chars: '{chars}', length: {passwordLength}, targetHash: {targetHash}");

            IEnumerable<string> GenerateCombinations(string chars, int length)
            {
                if (length == 0)
                {
                    yield return "";
                }
                else
                {
                    foreach (var c in chars)
                    {
                        foreach (var combination in GenerateCombinations(chars, length - 1))
                        {
                            string result = c + combination;
                            if (result.Length > 12)
                            {
                                result = result.Substring(0, 12);
                            }
                            yield return result;
                        }
                    }
                }
            }

            int combinationCount = 0;
            int logInterval = 1000; // Log every 1000 combinations

            foreach (var combination in GenerateCombinations(chars, passwordLength))
            {
                combinationCount++;

                using var md5 = System.Security.Cryptography.MD5.Create();
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(combination);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                if (computedHash == targetHash)
                {
                    DateTime bruteForceEndTime = DateTime.UtcNow;
                    int bruteForceTime = (int)(bruteForceEndTime - bruteForceStartTime).TotalMilliseconds;
                    ILogService.LogInfo(logServices, $"Match found after {combinationCount} combinations in {bruteForceTime}ms! Password: {combination}");
                    return combination;
                }

                // Log progress periodically
                if (combinationCount % logInterval == 0)
                {
                    DateTime currentTime = DateTime.UtcNow;
                    int elapsedTime = (int)(currentTime - bruteForceStartTime).TotalMilliseconds;
                    ILogService.LogInfo(logServices, $"Checked {combinationCount} combinations in {elapsedTime}ms");
                }

            }

            DateTime endTime = DateTime.UtcNow;
            int totalBruteForceTime = (int)(endTime - bruteForceStartTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"No match found after checking {combinationCount} combinations in {totalBruteForceTime}ms");
            Console.WriteLine($"No match found after checking {combinationCount} combinations");
            return null;
        }

        private class BruteForceRequest
        {
            [JsonPropertyName("userLogin")]
            public string userLogin { get; set; } = string.Empty;

            [JsonPropertyName("passwordLength")]
            public int PasswordLength { get; set; }

            [JsonPropertyName("chars")]
            public string Chars { get; set; } = string.Empty;
        }
    }
}