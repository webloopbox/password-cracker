using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public class CheckService : ICheckService
    {
        private readonly IEnumerable<ILogService> _logServices;
        private readonly DictionaryService _dictionaryService;

        public CheckService(IEnumerable<ILogService> logServices, DictionaryService dictionaryService)
        {
            _logServices = logServices;
            _dictionaryService = dictionaryService;
        }

        public async Task<IActionResult> HandleConnectToCentralServerRequest(HttpContext httpContext)
        {
            try
            {
                IFormCollection iFormCollection = await httpContext.Request.ReadFormAsync();
                string ipAddressString = iFormCollection["IpAddress"].ToString();
                IPAddress ipAddress = IPAddress.Parse(ipAddressString);
                if (ipAddress != null)
                {
                    if (Startup.ServersIpAddresses.Contains(ipAddress))
                    {
                        throw new Exception($"Calculating server with IP address {ipAddress} is already connected");
                    }
                    ILogService.LogInfo(_logServices, $"Made request to try to connect calculating server from IP address: {ipAddress}");
                    HandleCheckIfDatabaseIsAlive();
                    await HandleCheckIfCanConnectToCalculatingServer(ipAddress);
                    Startup.ServersIpAddresses.Add(ipAddress);
                    ILogService.LogInfo(_logServices, $"Calculating server with IP address: {ipAddress} successfully connected to the central server");
                    return new OkResult();
                }
                throw new Exception("Retrieved calculating server IP address is invalid");
            }
            catch (Exception ex)
            {
                ILogService.LogError(_logServices, $"Cannot connect to calculating server due to: {ex.Message}");
                return new ContentResult 
                {
                    Content = $"An error occurred while trying to connect calculating server: {ex.Message}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        private static void HandleCheckIfDatabaseIsAlive()
        {
            if (Startup.IsDatabaseRunning == false)
            {
                throw new Exception("database for calculating operations is not running");
            }
        }

        private async Task HandleCheckIfCanConnectToCalculatingServer(IPAddress ipAddress)
        {
            try
            {
                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                string serverUrl = $"http://{ipAddress}:5099/api/calculating/check-connection";
                StringContent dictionaryHash = new (_dictionaryService.GetCurrentDictionaryHashResult());
                HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(serverUrl, dictionaryHash);
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    throw new Exception($"Server {ipAddress} responded with status code {httpResponseMessage.StatusCode}");
                }
                string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(responseContent))
                {
                    ILogService.LogInfo(_logServices, $"No dictionary found on server {ipAddress}, starting synchronization");
                    _dictionaryService.DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                    _dictionaryService.SetDirectoryFiles();
                    await SynchronizeDictionaryWithCalculatingServer(ipAddress.ToString());
                }
                else
                {
                    ILogService.LogInfo(_logServices, $"Dictionary already exists on server {ipAddress} at path: {responseContent}");
                }
                ILogService.LogInfo(_logServices, $"Successfully connected to calculating server {ipAddress}");
            }
            catch (Exception ex)
            {
                ILogService.LogError(_logServices, $"Failed to connect with calculating server {ipAddress} for reason: {ex.Message}");
                throw;
            }
        }

        private async Task SynchronizeDictionaryWithCalculatingServer(string serverIp)
        {
            try
            {
                string latestFilePath = GetLatestDictionaryFilePath();
                using FileStream fileStream = OpenFileStream(latestFilePath);
                using HttpClient httpClient = CreateHttpClient();
                using MultipartFormDataContent formData = CreateFormData(fileStream, latestFilePath);
                await SendFileToServer(httpClient, serverIp, formData);
                ILogService.LogInfo(_logServices, $"Successfully synchronized dictionary with server {serverIp}");
            }
            catch (Exception ex)
            {
                ILogService.LogError(_logServices, $"Failed to synchronize dictionary with server {serverIp}: {ex.Message}");
                throw;
            }
        }

        private string GetLatestDictionaryFilePath()
        {
            string dictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
            string latestDictionaryHash = _dictionaryService.GetLatestDictionaryHash();
            if (string.IsNullOrEmpty(latestDictionaryHash))
            {
                throw new Exception("No valid dictionary hash found. Ensure the dictionary directory contains valid files.");
            }
            return Path.Combine(dictionaryDirectory, latestDictionaryHash);
        }

        private static FileStream OpenFileStream(string filePath)
        {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient httpClient = new()
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            return httpClient;
        }

        private static MultipartFormDataContent CreateFormData(FileStream fileStream, string filePath)
        {
            MultipartFormDataContent formData = new()
            {
                { new StreamContent(fileStream), "file", Path.GetFileName(filePath) }
            };
            return formData;
        }

        private static async Task SendFileToServer(HttpClient httpClient, string ipAddress, MultipartFormDataContent formData)
        {
            HttpResponseMessage response = await httpClient.PostAsync($"http://{ipAddress}:5099/api/dictionary/synchronizing", formData);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Server {ipAddress} responded with status code {response.StatusCode}");
            }
        }
    }
}