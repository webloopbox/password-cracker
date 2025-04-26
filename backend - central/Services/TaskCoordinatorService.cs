using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using backend___central.Interfaces;
using backend___central.Models;

namespace backend___central.Services
{
    public class TaskCoordinatorService
    {
        public static PasswordInfo? LastFoundPassword { get; private set; }
        private volatile bool passwordFound;
        private HashSet<IPAddress> failedServers;
        private readonly IEnumerable<ILogService> logServices;
        private Dictionary<CalculatingServerState, Task> processingTasks;
        private Dictionary<CalculatingServerState, TaskCompletionSource<bool>> taskCompletionSources;

        public TaskCoordinatorService(IEnumerable<ILogService> logServices)
        {
            passwordFound = false;
            this.logServices = logServices;
            processingTasks = new Dictionary<CalculatingServerState, Task>();
            taskCompletionSources = new Dictionary<CalculatingServerState, TaskCompletionSource<bool>>();
            failedServers = new HashSet<IPAddress>();
            ResetState();
        }

        public void AddServerTask(CalculatingServerState server, Chunk chunk, string username)
        {
            TaskCompletionSource<bool> taskCompletionSource = new();
            taskCompletionSources[server] = taskCompletionSource;
            processingTasks[server] = ProcessServerChunkAsync(server, chunk, taskCompletionSource, username);
        }

        public void CancelAllTasks()
        {
            foreach (TaskCompletionSource<bool> taskCompletionSource in taskCompletionSources.Values)
            {
                taskCompletionSource.TrySetCanceled();
            }
        }

        public async Task<bool> ProcessCompletedTasks()
        {
            if (!processingTasks.Any())
            {
                await Task.Delay(100);
                return false;
            }
            ILogService.LogInfo(logServices, $"Processing {processingTasks.Count} tasks, checking for completed tasks");
            Task completedTask = await Task.WhenAny(processingTasks.Values);
            CalculatingServerState completedServer = processingTasks.First(x => x.Value == completedTask).Key;
            try
            {
                ILogService.LogInfo(logServices, $"Awaiting completed task from server {completedServer.IpAddress}");
                await completedTask;
                if (LastFoundPassword != null || passwordFound)
                {
                    ILogService.LogInfo(logServices, $"Password found after task completion: {(LastFoundPassword != null ? LastFoundPassword.Value : "unknown")}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Task error: {ex.Message}");
                HandleServerError(completedServer);
            }
            ILogService.LogInfo(logServices, $"Task completed normally, removed from tracking");
            processingTasks.Remove(completedServer);
            taskCompletionSources.Remove(completedServer);
            return LastFoundPassword != null || passwordFound;
        }

        private async Task ProcessServerChunkAsync(CalculatingServerState server, Chunk chunk, TaskCompletionSource<bool> taskCompletionSource, string username)
        {
            try
            {
                await ValidateServerHealth(server);
                var (response, content) = await SendChunkToServer(server, chunk, username);
                HandleServerResponse(server, response, content, chunk, taskCompletionSource);
            }
            catch (Exception ex)
            {
                HandleProcessingException(server, ex, taskCompletionSource);
            }
            finally
            {
                server.IsBusy = false;
            }
        }

        private async Task ValidateServerHealth(CalculatingServerState server)
        {
            if (!await IsServerHealthy(server.IpAddress))
            {
                throw new Exception($"Server {server.IpAddress} is not healthy");
            }
        }

        private static async Task<(HttpResponseMessage response, string content)> SendChunkToServer(CalculatingServerState server, Chunk chunk, string username)
        {
            using HttpClient httpClient = new() { Timeout = TimeSpan.FromHours(2) };
            StringContent requestContent = new(
                JsonSerializer.Serialize(new { chunk, username }),
                Encoding.UTF8,
                "application/json"
            );
            HttpResponseMessage response = await SendRequestToCalculatingServer(httpClient, server.IpAddress, requestContent);
            string responseContent = await response.Content.ReadAsStringAsync();
            return (response, responseContent);
        }

        private void HandleServerResponse(CalculatingServerState server, HttpResponseMessage response, string responseContent, Chunk chunk, TaskCompletionSource<bool> taskCompletionSource)
        {
            if (CheckForPasswordFound(server, responseContent, taskCompletionSource, chunk))
                return;
            if (passwordFound || taskCompletionSource.Task.IsCanceled)
                return;
            if (!response.IsSuccessStatusCode)
            {
                HandleFailedResponse(server, response);
                return;
            }
            HandleSuccessfulResponse(server, responseContent, taskCompletionSource, chunk);
        }

        private bool CheckForPasswordFound(CalculatingServerState server, string responseContent, TaskCompletionSource<bool> taskCompletionSource, Chunk chunk)
        {
            ILogService.LogInfo(logServices, $"Checking response from server {server.IpAddress}: {responseContent}");
            if (responseContent.Contains("Password found") || responseContent.Contains("\"message\":\"Password found!"))
            {
                string password = ExtractPasswordFromResponse(responseContent);
                passwordFound = true;
                DateTime lastDateTime = DateTime.UtcNow;
                int totalCentralExecutionTime = (int)(lastDateTime - chunk.firstDateTime).TotalMilliseconds;
                int calculatingServerTime = -1;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(responseContent);
                    if (document.RootElement.TryGetProperty("time", out JsonElement timeElement) &&
                        timeElement.ValueKind == JsonValueKind.Number)
                    {
                        calculatingServerTime = timeElement.GetInt32();
                    }
                }
                catch (JsonException)
                {
                    ILogService.LogInfo(logServices, $"Response from server {server.IpAddress} is not in JSON format");
                }

                int finalTime = calculatingServerTime > 0 ? totalCentralExecutionTime - calculatingServerTime : totalCentralExecutionTime;
                ILogService.LogInfo(logServices,
                    $"[Dictionary] Central: Total = {totalCentralExecutionTime} ms" +
                    (calculatingServerTime > 0 ? $" | Calculating: ({server.IpAddress}) Total = {calculatingServerTime} ms" : "") +
                    $" | Communication time = {finalTime} ms");
                ILogService.LogInfo(logServices, $"Password found by server {server.IpAddress}: {responseContent}");
                LastFoundPassword = new PasswordInfo
                {
                    Value = password,
                    ServerIp = server.IpAddress.ToString(),
                    ServerTime = calculatingServerTime,
                    TotalTime = totalCentralExecutionTime
                };
                ILogService.LogInfo(logServices, $"Setting password found flag to true with password: {password}");
                passwordFound = true;
                CancelAllTasks();
                taskCompletionSource.TrySetResult(true);
                return true;
            }
            return false;
        }

        private string ExtractPasswordFromResponse(string responseContent)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("message", out JsonElement messageElement))
                {
                    string message = messageElement.GetString() ?? "";
                    ILogService.LogInfo(logServices, $"Examining message: {message}");
                    int passwordIndex = message.IndexOf("Password: ");
                    if (passwordIndex >= 0)
                    {
                        return message.Substring(passwordIndex + 10).Trim();
                    }
                }
                if (document.RootElement.TryGetProperty("password", out JsonElement pwElement))
                {
                    return pwElement.GetString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error extracting password from response: {ex.Message}");
            }
            int startIndex = responseContent.IndexOf("Password: ");
            if (startIndex >= 0)
            {
                startIndex += 10; 
                int endIndex = responseContent.IndexOf('"', startIndex);
                if (endIndex > startIndex)
                {
                    return responseContent.Substring(startIndex, endIndex - startIndex);
                }
                return responseContent.Substring(startIndex);
            }
            return "Unknown";
        }

        private void HandleFailedResponse(CalculatingServerState server, HttpResponseMessage response)
        {
            MarkServerAsFailed(server.IpAddress);
            throw new Exception($"Server {server.IpAddress} responded with status code {response.StatusCode}");
        }

        private void HandleSuccessfulResponse(CalculatingServerState server, string responseContent, TaskCompletionSource<bool> taskCompletionSource, Chunk chunk)
        {
            DateTime lastDateTime = DateTime.UtcNow;
            int totalCentralExecutionTime = (int)(lastDateTime - chunk.firstDateTime).TotalMilliseconds;
            int calculatingServerTime = -1;
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("time", out JsonElement timeElement) &&
                    timeElement.ValueKind == JsonValueKind.Number)
                {
                    calculatingServerTime = timeElement.GetInt32();
                }
            }
            catch (JsonException)
            {
                ILogService.LogInfo(logServices, $"Response from server {server.IpAddress} is not in JSON format");
            }
            int finalTime = calculatingServerTime > 0 ? totalCentralExecutionTime - calculatingServerTime : totalCentralExecutionTime;
            ILogService.LogInfo(logServices,
                $"[Dictionary] Central: Total = {totalCentralExecutionTime} ms" +
                (calculatingServerTime > 0 ? $" | Calculating: ({server.IpAddress}) Total = {calculatingServerTime} ms" : "") +
                $" | Communication time = {finalTime} ms");
            ILogService.LogInfo(logServices, $"Server {server.IpAddress} completed chunk processing: {responseContent}");
            taskCompletionSource.TrySetResult(true);
        }

        private void HandleProcessingException(CalculatingServerState server, Exception ex, TaskCompletionSource<bool> taskCompletionSource)
        {
            if (ex is PasswordFoundException)
            {
                passwordFound = true;
                CancelAllTasks();
                taskCompletionSource.TrySetException(ex);
                return;
            }
            if (!taskCompletionSource.Task.IsCanceled && !passwordFound)
            {
                HandleServerError(server);
                taskCompletionSource.TrySetException(ex);
            }
        }

        public void ResetState()
        {
            processingTasks = new Dictionary<CalculatingServerState, Task>();
            taskCompletionSources = new Dictionary<CalculatingServerState, TaskCompletionSource<bool>>();
            failedServers = new HashSet<IPAddress>();
            passwordFound = false;
        }

        private static async Task<HttpResponseMessage> SendRequestToCalculatingServer(HttpClient httpClient, IPAddress serverIp, StringContent content)
        {
            return await httpClient.PostAsync(
                $"http://{serverIp}:5099/api/dictionary/cracking",
                content
            );
        }

        private void HandleServerError(CalculatingServerState server)
        {
            server.IsBusy = false;
            failedServers.Add(server.IpAddress);
            if (Startup.ServersIpAddresses.Count == 0)
            {
                throw new Exception("All calculating servers have failed. Stopping dictionary cracking.");
            }
        }

        private async Task<bool> IsServerHealthy(IPAddress serverIp)
        {
            try
            {
                using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
                string serverUrl = $"http://{serverIp}:5099/api/calculating/check-connection";
                HttpResponseMessage response = await httpClient.GetAsync(serverUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ILogService.LogError(logServices, $"Server {serverIp} health check failed with status code {response.StatusCode}");
                    RemoveUnhealthyServer(serverIp);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Server {serverIp} health check failed: {ex.Message}");
                RemoveUnhealthyServer(serverIp);
                return false;
            }
        }

        private void RemoveUnhealthyServer(IPAddress serverIp)
        {
            MarkServerAsFailed(serverIp);
            CalculatingServerState? serverToRemove = processingTasks.Keys
                .FirstOrDefault(s => s.IpAddress.Equals(serverIp));
            if (serverToRemove != null)
            {
                if (processingTasks.ContainsKey(serverToRemove))
                    processingTasks.Remove(serverToRemove);
                if (taskCompletionSources.ContainsKey(serverToRemove))
                    taskCompletionSources.Remove(serverToRemove);
                serverToRemove.IsBusy = false;
            }
            ILogService.LogInfo(logServices, $"Removed unhealthy server {serverIp} from active processing pool");
        }

        public bool CanProcessServer(CalculatingServerState server)
        {
            if (passwordFound)
                return false;
            if (failedServers.Contains(server.IpAddress))
                return false;
            if (server.IsBusy)
                return false;
            if (processingTasks.ContainsKey(server) &&
                (!processingTasks[server].IsCompleted ||
                !taskCompletionSources[server].Task.IsCompleted))
                return false;
            Task<bool> healthCheckTask = IsServerHealthy(server.IpAddress);
            try
            {
                if (!healthCheckTask.Wait(TimeSpan.FromSeconds(5)) || !healthCheckTask.Result)
                {
                    ILogService.LogError(logServices,
                        $"Server {server.IpAddress} failed health check, marking as failed");
                    MarkServerAsFailed(server.IpAddress);
                    return false;
                }
            }
            catch
            {
                MarkServerAsFailed(server.IpAddress);
                return false;
            }

            return true;
        }

        private void MarkServerAsFailed(IPAddress serverIp)
        {
            if (Startup.ServersIpAddresses.Contains(serverIp))
            {
                Startup.ServersIpAddresses.Remove(serverIp);
                failedServers.Add(serverIp);
                ILogService.LogInfo(logServices,
                    $"Removed failed server {serverIp}. Remaining: {Startup.ServersIpAddresses.Count}");
            }
        }
    }
}