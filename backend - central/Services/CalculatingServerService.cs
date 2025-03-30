using System.Net;

namespace backend___central.Services
{
    public class CalculatingServerService(IEnumerable<ILogService> logServices, DictionaryService dictionaryService) : ICalculatingServerService
    {
        private readonly IEnumerable<ILogService> logServices = logServices;
        private readonly DictionaryService dictionaryService = dictionaryService;

        public async Task<IResult> HandleConnectToCentralServerRequest(HttpContext httpContext)
        {
            try
            {
                IFormCollection iFormCollection = await httpContext.Request.ReadFormAsync();
                string ipAddressString = iFormCollection["IpAddress"].ToString();
                IPAddress ipAddress = IPAddress.Parse(ipAddressString);
                if (ipAddress != null)
                {
                    ILogService.LogInfo(logServices, $"Made request to try to connect calculating server from IP address: {ipAddress}");
                    HandleCheckIfDatabaseIsAlive();
                    await HandleCheckIfCanConnectToCalculatingServer(ipAddress);
                    Program.ServersIpAddresses.Add(ipAddress);
                    ILogService.LogInfo(logServices, $"Calculating server with IP address: {ipAddress} successfully connected to the central server");
                    return Results.Ok();
                }
                throw new Exception("Retrieved calculating server IP address is invalid");
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Cannot connect to calculating server due to: {ex.Message}");
                return Results.Problem($"An error occurred while trying to connect calculating server: {ex.Message}");
            }
        }

        private static void HandleCheckIfDatabaseIsAlive()
        {
            if (Program.IsDatabaseRunning == false)
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
                string serverUrl = $"http://{ipAddress}:5099/api/central/check-connection";
                HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(serverUrl);
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    throw new Exception($"Server {ipAddress} responded with status code {httpResponseMessage.StatusCode}");
                }
                ILogService.LogInfo(logServices, $"Successfully connected to calculating server {ipAddress}");
                dictionaryService.DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                dictionaryService.SetDirectoryFiles();
                await SynchronizeDictionaryWithCalculatingServer(ipAddress.ToString());
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Failed to connect with calculating server {ipAddress} for reason: {ex.Message}");
                throw;
            }
        }

        private async Task SynchronizeDictionaryWithCalculatingServer(string serverIp)
        {
            try
            {
                string latestZipFilePath = GetLatestDictionaryFilePath();
                using FileStream fileStream = OpenFileStream(latestZipFilePath);
                using HttpClient httpClient = CreateHttpClient();
                using MultipartFormDataContent formData = CreateFormData(fileStream, latestZipFilePath);
                await SendFileToServer(httpClient, serverIp, formData);
                ILogService.LogInfo(logServices, $"Successfully synchronized dictionary with server {serverIp}");
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Failed to synchronize dictionary with server {serverIp}: {ex.Message}");
                throw;
            }
        }

        private string GetLatestDictionaryFilePath()
        {
            string dictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
            string latestDictionaryHash = dictionaryService.GetLatestDictionaryHash();
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
            HttpResponseMessage response = await httpClient.PostAsync($"http://{ipAddress}:5099/api/synchronizing/dictionary", formData);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Server {ipAddress} responded with status code {response.StatusCode}");
            }
        }
    }
}