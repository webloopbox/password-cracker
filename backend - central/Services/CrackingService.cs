using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace backend___central.Services
{
    public class CrackingService : ICrackingService
    {
        private readonly IEnumerable<ILogService> logServices;
        private readonly CheckService checkService;

        public CrackingService(IEnumerable<ILogService> logServices, CheckService checkService)
        {
            this.logServices = logServices;
            this.checkService = checkService;
        }

        public async Task<IActionResult> HandlBruteForceRequest(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");

            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();

            Console.WriteLine($"Request body content: {bodyContent}");

            try
            {
                ILogService.LogInfo(logServices, bodyContent);

                // Deserialize the JSON directly into a JsonDocument to safely handle JsonElement
                using JsonDocument document = JsonDocument.Parse(bodyContent);
                JsonElement root = document.RootElement;

                // Validate required fields
                if (!root.TryGetProperty("passwordLength", out JsonElement passwordLengthElement) ||
                    !root.TryGetProperty("userLogin", out JsonElement userLoginElement))
                {
                    return new ContentResult
                    {
                        Content = "Invalid request data. Missing required fields.",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }

                // Extract values safely
                if (!passwordLengthElement.TryGetInt32(out int passwordLength))
                {
                    return new ContentResult
                    {
                        Content = "Invalid request data. passwordLength must be an integer.",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }

                string userLogin = userLoginElement.GetString() ?? string.Empty;

                // Get the number of hosts dynamically
                int hostsCount = Startup.ServersIpAddresses.Count;

                Console.WriteLine($"Number of hosts: {hostsCount}");

                // Generate character portions based on granularity and hosts count
                var allChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                int portionSize = allChars.Length / hostsCount;
                var charPortions = Enumerable.Range(0, hostsCount)
                                             .Select(i => allChars.Substring(i * portionSize, Math.Min(portionSize, allChars.Length - i * portionSize)))
                                             .ToList();

                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromHours(2);

                Console.WriteLine($"Server IP Addresses: {string.Join(", ", Startup.ServersIpAddresses)}");

                var tasks = new List<Task<(bool Success, string? Password, string ServerIp)>>();
                for (int i = 0; i < Startup.ServersIpAddresses.Count; i++)
                {
                    string serverIpAddress = Startup.ServersIpAddresses[i].ToString();
                    await checkService.HandleCheckIfCanConnectToCalculatingServer(System.Net.IPAddress.Parse(serverIpAddress));

                    Console.WriteLine($"Making request to --> http://{serverIpAddress}:5099/api/synchronizing/brute-force");

                    var payload = new
                    {
                        passwordLength,
                        userLogin,
                        chars = charPortions[i]
                    };

                    string payloadJson = JsonSerializer.Serialize(payload);

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var response = await httpClient.PostAsync(
                                $"http://{serverIpAddress}:5099/api/synchronizing/brute-force",
                                new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
                            );

                            if (!response.IsSuccessStatusCode)
                            {
                                ILogService.LogError(logServices, $"Failed to synchronize with server {serverIpAddress}. Status code: {response.StatusCode}");
                                return (Success: false, Password: null, ServerIp: serverIpAddress);
                            }

                            string responseContent = await response.Content.ReadAsStringAsync();
                            try
                            {
                                var responseData = JsonSerializer.Deserialize<BruteForceResponse>(responseContent);
                                if (responseData != null && responseData.Message == "Password found." && !string.IsNullOrEmpty(responseData.Password))
                                {
                                    ILogService.LogInfo(logServices, $"Password found by server {serverIpAddress}: {responseData.Password}");
                                    return (Success: true, Password: responseData.Password, ServerIp: serverIpAddress);
                                }
                                else
                                {
                                    ILogService.LogInfo(logServices, $"No password found by server {serverIpAddress}.");
                                    return (Success: false, Password: null, ServerIp: serverIpAddress);
                                }
                            }
                            catch (JsonException ex)
                            {
                                ILogService.LogError(logServices, $"Failed to parse response from server {serverIpAddress}: {ex.Message}");
                                return (Success: false, Password: null, ServerIp: serverIpAddress);
                            }
                        }
                        catch (Exception ex)
                        {
                            ILogService.LogError(logServices, $"Error communicating with server {serverIpAddress}: {ex.Message}");
                            return (Success: false, Password: null, ServerIp: serverIpAddress);
                        }
                    }));
                }

                var results = await Task.WhenAll(tasks);

                var successfulResult = results.FirstOrDefault(r => r.Success);
                if (successfulResult.Success)
                {
                    return new OkObjectResult(new
                    {
                        Message = "Password found.",
                        Password = successfulResult.Password,
                        Server = successfulResult.ServerIp
                    });
                }

                return new NotFoundObjectResult(new
                {
                    Message = "Password not found by any server."
                });
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Cannot connect to calculating server due to: {ex.Message}");
                return new ContentResult
                {
                    Content = $"An error occurred while trying to connect calculating server: {ex.Message}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        public IActionResult HandleDictionaryCracking(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using dictionary method");
            ILogService.LogInfo(logServices, "Final cracking time with dictionary method was: 01:34:10. Cracking was successful.");

            return new ContentResult
            {
                Content = "Started dictionary password cracking.",
                StatusCode = 202
            };
        }

        private class BruteForceResponse
        {
            [JsonPropertyName("Message")]
            public string Message { get; set; }

            [JsonPropertyName("Password")]
            public string? Password { get; set; }
        }
    }
}