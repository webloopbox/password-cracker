using System;
using System.Collections.Concurrent;
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
    public class ServerCommunicationService : IServerCommunicationService
    {
        private readonly IEnumerable<ILogService> logServices;
        private readonly CheckService checkService;
        private readonly ConcurrentQueue<string> workPackages;
        private int granularity = 4; 

        public ServerCommunicationService(IEnumerable<ILogService> logServices, CheckService checkService)
        {
            this.logServices = logServices;
            this.checkService = checkService;
            workPackages = new ConcurrentQueue<string>();
        }

        public void SetGranularity(int granularity)
        {
            if (granularity <= 0)
            {
                ILogService.LogError(logServices, "Granularity must be greater than 0. Using default value of 4.");
                this.granularity = 4;
                return;
            }
            this.granularity = granularity;
        }

        public async Task<HttpResponseMessage> SendRequestToServer(HttpClient httpClient, string serverIpAddress, string payloadJson)
        {
            return await httpClient.PostAsync(
                $"http://{serverIpAddress}:5099/api/synchronizing/brute-force",
                new StringContent(payloadJson, Encoding.UTF8, "application/json")
            );
        }

        public async Task ValidateServerConnection(string serverIpAddress)
        {
            ILogService.LogInfo(logServices, $"Checking connection to server {serverIpAddress}");
            await checkService.HandleCheckIfCanConnectToCalculatingServer(IPAddress.Parse(serverIpAddress));
            ILogService.LogInfo(logServices, $"Successfully connected to server {serverIpAddress}");
        }

        private void CreateCharacterPackages(string charSet)
        {
            workPackages.Clear(); 
            for (int i = 0; i < charSet.Length; i += granularity)
            {
                string package = new (charSet.Skip(i).Take(granularity).ToArray());
                if (!string.IsNullOrEmpty(package))
                {
                    workPackages.Enqueue(package);
                    ILogService.LogInfo(logServices, $"Created work package: '{package}'");
                }
            }
            ILogService.LogInfo(logServices, $"Total work packages created: {workPackages.Count}");
        }

        public async Task<List<Task<CrackingResult>>> CreateTasksForPortions(List<string> charSets, int passwordLength, string userLogin, List<string> serverIPs)
        {
            string fullCharSet = string.Join("", charSets);
            CreateCharacterPackages(fullCharSet);
            List<Task<CrackingResult>> tasks = new();
            int serverCount = serverIPs.Count;
            foreach (string serverIp in serverIPs)
            {
                await ValidateServerConnection(serverIp);
            }
            for (int i = 0; i < serverCount; i++)
            {
                string serverIpAddress = serverIPs[i];
                tasks.Add(ProcessPackagesForServer(serverIpAddress, passwordLength, userLogin));
            }
            return tasks;
        }

        private async Task<CrackingResult> ProcessPackagesForServer(string serverIpAddress, int passwordLength, string userLogin)
        {
            CrackingResult finalResult = new(-1, false, serverIpAddress, "");
            int totalProcessingTime = 0;
            bool passwordFound = false;
            int packagesProcessed = 0;
            while (!passwordFound && workPackages.TryDequeue(out string? charPackage) && charPackage != null)
            {
                packagesProcessed++;
                ILogService.LogInfo(logServices, $"Server {serverIpAddress} processing package #{packagesProcessed}: '{charPackage}'");
                CrackingResult result = await ProcessSinglePackage(serverIpAddress, passwordLength, userLogin, charPackage);
                totalProcessingTime += (result.Time > 0) ? result.Time : 0;
                if (result.Success)
                {
                    ILogService.LogInfo(logServices, $"Password found by server {serverIpAddress} in package: '{charPackage}'");
                    return result;
                }
            }
            if (packagesProcessed > 0)
            {
                ILogService.LogInfo(logServices, $"Server {serverIpAddress} processed {packagesProcessed} packages with no password found. Total time: {totalProcessingTime}ms");
                finalResult.Time = totalProcessingTime;
            }
            else
            {
                ILogService.LogInfo(logServices, $"No work packages were available for server {serverIpAddress}");
            }
            return finalResult;
        }

        private async Task<CrackingResult> ProcessSinglePackage(string serverIpAddress, int passwordLength, string userLogin, string chars)
        {
            object payload = CreateRequestPayload(passwordLength, userLogin, chars);
            string payloadJson = SerializePayload(payload);
            LogServerRequestInfo(serverIpAddress, chars);
            DateTime serverStartTime = DateTime.UtcNow;
            try
            {
                using HttpClient httpClient = CreateHttpClient();
                LogSendingBruteForceRequest(serverIpAddress);
                HttpResponseMessage response = await SendRequestToServer(httpClient, serverIpAddress, payloadJson);
                DateTime serverResponseTime = DateTime.UtcNow;
                int serverRequestTime = (int)(serverResponseTime - serverStartTime).TotalMilliseconds;
                return await ProcessServerResponse(response, serverIpAddress, serverRequestTime);
            }
            catch (Exception ex)
            {
                return HandleServerTaskException(ex, serverIpAddress, serverStartTime);
            }
        }

        private static object CreateRequestPayload(int passwordLength, string userLogin, string chars)
        {
            return new { passwordLength, userLogin, chars };
        }

        private static string SerializePayload(object payload)
        {
            return JsonSerializer.Serialize(payload);
        }

        private void LogServerRequestInfo(string serverIpAddress, string chars)
        {
            ILogService.LogInfo(logServices, $"Sending request to server {serverIpAddress} with character range: {chars.First()}-{chars.Last()}");
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromHours(2) };
        }

        private void LogSendingBruteForceRequest(string serverIpAddress)
        {
            ILogService.LogInfo(logServices, $"Sending brute force request to {serverIpAddress}");
        }

        public async Task<CrackingResult> ProcessServerResponse(HttpResponseMessage response, string serverIpAddress, int serverRequestTime)
        {
            if (!IsResponseSuccessful(response, serverIpAddress))
            {
                return CreateFailedResponseResult(serverIpAddress);
            }
            string responseContent = await ReadResponseContent(response);
            int calculatingServerTime = ExtractCalculatingServerTime(responseContent, serverIpAddress);
            int communicationTime = CalculateCommunicationTime(serverRequestTime, calculatingServerTime);
            LogCentralTiming(serverRequestTime, calculatingServerTime, communicationTime, serverIpAddress);
            BruteForceResponse responseData = ParseResponseData(responseContent, serverIpAddress);
            if (responseData == null)
            {
                return CreateInvalidResponseResult(serverIpAddress);
            }
            int time = DetermineTimeValue(calculatingServerTime, responseData.Time);
            if (IsPasswordFound(responseData))
            {
                LogServerProcessingCompleted(serverIpAddress, responseContent);
                return CreateSuccessfulResponseResult(time, serverIpAddress, responseData.Password);
            }
            LogServerProcessingCompleted(serverIpAddress, responseContent);
            return CreateFailedPasswordSearchResult(time, serverIpAddress);
        }

        private bool IsResponseSuccessful(HttpResponseMessage response, string serverIpAddress)
        {
            if (!response.IsSuccessStatusCode)
            {
                ILogService.LogError(logServices, $"Failed to synchronize with server {serverIpAddress}. Status code: {response.StatusCode}");
                return false;
            }
            return true;
        }

        private static CrackingResult CreateFailedResponseResult(string serverIpAddress)
        {
            return new CrackingResult(-1, false, serverIpAddress, "");
        }

        private static async Task<string> ReadResponseContent(HttpResponseMessage response)
        {
            return await response.Content.ReadAsStringAsync();
        }

        private int ExtractCalculatingServerTime(string responseContent, string serverIpAddress)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("time", out JsonElement timeElement) && 
                    timeElement.ValueKind == JsonValueKind.Number)
                {
                    return timeElement.GetInt32();
                }
                if (document.RootElement.TryGetProperty("calculationTime", out JsonElement calcTimeElement) && 
                    calcTimeElement.ValueKind == JsonValueKind.Number)
                {
                    return calcTimeElement.GetInt32();
                }
                return -1;
            }
            catch (JsonException)
            {
                ILogService.LogInfo(logServices, $"Response from server {serverIpAddress} is not in JSON format");
                return -1;
            }
        }

        private static int CalculateCommunicationTime(int serverRequestTime, int calculatingServerTime)
        {
            return calculatingServerTime > 0 ? serverRequestTime - calculatingServerTime : serverRequestTime;
        }

        private void LogCentralTiming(int serverRequestTime, int calculatingServerTime, int communicationTime, string serverIpAddress)
        {
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Central: Total = {serverRequestTime} ms" + 
                (calculatingServerTime > 0 ? $" | Calculating: ({serverIpAddress}) Total = {calculatingServerTime} ms" : "") + 
                $" | Communication time = {communicationTime} ms");
        }

        private BruteForceResponse ParseResponseData(string responseContent, string serverIpAddress)
        {
            try
            {
                return JsonSerializer.Deserialize<BruteForceResponse>(responseContent);
            }
            catch
            {
                ILogService.LogError(logServices, $"Invalid response from server {serverIpAddress}: Could not parse response");
                return null;
            }
        }

        private static CrackingResult CreateInvalidResponseResult(string serverIpAddress)
        {
            return new CrackingResult(-1, false, serverIpAddress, "");
        }

        private static int DetermineTimeValue(int calculatingServerTime, int responseDataTime)
        {
            return calculatingServerTime > 0 ? calculatingServerTime : responseDataTime;
        }

        private static bool IsPasswordFound(BruteForceResponse responseData)
        {
            return responseData.Message == "Password found." && !string.IsNullOrEmpty(responseData.Password);
        }

        private void LogServerProcessingCompleted(string serverIpAddress, string responseContent)
        {
            ILogService.LogInfo(logServices, $"Server {serverIpAddress} completed processing: {responseContent}");
        }

        private static CrackingResult CreateSuccessfulResponseResult(int time, string serverIpAddress, string password)
        {
            return new CrackingResult(time, true, serverIpAddress, password);
        }

        private static CrackingResult CreateFailedPasswordSearchResult(int time, string serverIpAddress)
        {
            return new CrackingResult(time, false, serverIpAddress, "");
        }

        private CrackingResult HandleServerTaskException(Exception ex, string serverIpAddress, DateTime serverStartTime)
        {
            DateTime errorTime = DateTime.UtcNow;
            int errorDuration = (int)(errorTime - serverStartTime).TotalMilliseconds;
            ILogService.LogError(logServices, 
                $"[BruteForce] Server {serverIpAddress}: Error | " +
                $"Communication time = {errorDuration} ms | Error: {ex.Message}"); 
            return new CrackingResult(-1, false, serverIpAddress, "");
        }
    }
}