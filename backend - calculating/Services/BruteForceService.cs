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
        public async Task<IActionResult> SynchronizeBruteForce(HttpContext httpContext)
        {
            DateTime secondDateTime = DateTime.UtcNow;
            if (httpContext == null || httpContext.Request?.Body == null)
            {
                DateTime thirdDateTime = DateTime.UtcNow;
                int TotalCalculatingExecutionTime= (int)(thirdDateTime - secondDateTime).TotalMilliseconds;
                return new BadRequestObjectResult(new { Message = "HttpContext or Request.Body is null.", Time = -1 });
            }

            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();

            Console.WriteLine($"Request body content from calculating host: {bodyContent}");

            var requestData = JsonSerializer.Deserialize<BruteForceRequest>(bodyContent);
            if (requestData == null || string.IsNullOrEmpty(requestData.userLogin))
            {
                DateTime thirdDateTime = DateTime.UtcNow;
                int TotalCalculatingExecutionTime= (int)(thirdDateTime - secondDateTime).TotalMilliseconds;
                return new BadRequestObjectResult(new { Message = "Invalid request data.", Time = -1 });
            }

            string? hash = await GetHashFromDatabase(requestData.userLogin);
            if (string.IsNullOrEmpty(hash))
            {
                DateTime thirdDateTime = DateTime.UtcNow;
                int TotalCalculatingExecutionTime= (int)(thirdDateTime - secondDateTime).TotalMilliseconds;
                return new NotFoundObjectResult(new { Message = $"Hash for user login '{requestData.userLogin}' not found.", Time = -1 });
            }

            Console.WriteLine($"Retrieved hash for user '{requestData.userLogin}': {hash}");

            // Perform brute-force cracking
            string? foundPassword = PerformBruteForce(requestData.Chars, requestData.PasswordLength, hash);
            DateTime thirdDateTime = DateTime.UtcNow;
            int TotalCalculatingExecutionTime= (int)(thirdDateTime - secondDateTime).TotalMilliseconds;

            if (foundPassword != null)
            {
                Console.WriteLine($"Password found: {foundPassword}");
                return new OkObjectResult(new { Message = "Password found.", Password = foundPassword, Time = TotalCalculatingExecutionTime});
            }
            else
            {
                Console.WriteLine("Password not found in the given range.");
                return new OkObjectResult(new { Message = "Password not found.", Time = TotalCalculatingExecutionTime});
            }
        }

        private async Task<string?> GetHashFromDatabase(string userLogin)
        {
            try
            {
                string? connectionString = Environment.GetEnvironmentVariable("POSTGRES_DB_CONNECTION_STRING");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new Exception("Database connection string is not set.");
                }

                using NpgsqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                string query = "SELECT password FROM users WHERE login = @login LIMIT 1;";
                using NpgsqlCommand command = new(query, connection);
                command.Parameters.AddWithValue("login", userLogin);

                object? result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching hash from database: {ex.Message}");
                return null;
            }
        }

        private string? PerformBruteForce(string chars, int passwordLength, string targetHash)
        {
            Console.WriteLine($"Starting brute force with chars: '{chars}', length: {passwordLength}, targetHash: {targetHash}");

            IEnumerable<string> GenerateCombinations(string chars, int length)
            {
                Console.WriteLine($"GenerateCombinations called with length: {length}");
                if (length == 0)
                {
                    Console.WriteLine("Yielding empty string for length 0");
                    yield return "";
                }
                else
                {
                    foreach (var c in chars)
                    {
                        Console.WriteLine($"Processing character: {c}");
                        foreach (var combination in GenerateCombinations(chars, length - 1))
                        {
                            string result = c + combination;
                            Console.WriteLine($"Yielding combination: {result}");
                            yield return result;
                        }
                    }
                }
            }

            int combinationCount = 0;
            foreach (var combination in GenerateCombinations(chars, passwordLength))
            {
                combinationCount++;
                Console.WriteLine($"Checking combination #{combinationCount}: '{combination}'");
                using var md5 = System.Security.Cryptography.MD5.Create();
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(combination);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                if (computedHash == targetHash)
                {
                    Console.WriteLine($"Match found! Password: {combination}, Hash: {computedHash}");
                    return combination;
                }
                // Limit logging to avoid flooding, but allow enough to diagnose
                if (combinationCount >= 10000)
                {
                    Console.WriteLine("Stopping after 10 000 combinations for debugging");
                    break;
                }
            }

            Console.WriteLine($"No match found after checking {combinationCount} combinations");
            return null;
        }

        private class BruteForceRequest
        {
            [JsonPropertyName("userLogin")]
            public string userLogin { get; set; }

            [JsonPropertyName("passwordLength")]
            public int PasswordLength { get; set; }

            [JsonPropertyName("chars")]
            public string Chars { get; set; }
        }
    }
}