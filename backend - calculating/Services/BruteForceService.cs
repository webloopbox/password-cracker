using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;
using System.Collections.Generic;
using backend___calculating.Models;
using System.Security.Cryptography;
using System.Text;
using backend___calculating.Interfaces;
using System.Numerics;

namespace backend___calculating.Services
{
    public class BruteForceService : IBruteForceService
    {
        private const int LogInterval = 1000;
        private readonly IEnumerable<ILogService> logServices;

        private readonly IPasswordRepository _passwordRepository;

        public BruteForceService(IEnumerable<ILogService> logServices, IPasswordRepository passwordRepository)
        {
            this.logServices = logServices;
            _passwordRepository = passwordRepository;
        }

        public async Task<IActionResult> SynchronizeBruteForce(HttpContext httpContext)
        {
            DateTime startTime = DateTime.UtcNow;
            ILogService.LogInfo(logServices, "Starting brute force cracking");
            try
            {
                if (!IsValidRequest(httpContext))
                {
                    return CreateErrorResponse("HttpContext or Request.Body is null.", StatusCodes.Status400BadRequest);
                }
                BruteForceRequest? requestData = await ParseRequestBody(httpContext);
                if (!IsValidRequestData(requestData))
                {
                    return CreateErrorResponse("Invalid request data.", StatusCodes.Status400BadRequest);
                }
                if (string.IsNullOrEmpty(requestData?.UserLogin))
                {
                    return CreateErrorResponse("User login is null or empty.", StatusCodes.Status400BadRequest);
                }
                string? hash = await GetHashFromDatabase(requestData.UserLogin);
                if (string.IsNullOrEmpty(hash))
                {
                    return CreateErrorResponse($"Hash for user login '{requestData.UserLogin}' not found.", StatusCodes.Status404NotFound);
                }
                BruteForceResult result = ExecuteBruteForce(startTime, requestData, hash);
                return CreateBruteForceResponse(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, startTime);
            }
        }

        private static bool IsValidRequest(HttpContext? httpContext)
        {
            return httpContext != null && httpContext.Request?.Body != null;
        }

        private static bool IsValidRequestData(BruteForceRequest? requestData)
        {
            return requestData != null && !string.IsNullOrEmpty(requestData.UserLogin);
        }

        private async Task<BruteForceRequest?> ParseRequestBody(HttpContext httpContext)
        {
            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();
            ILogService.LogInfo(logServices, $"Request body content: {bodyContent}");
            return JsonSerializer.Deserialize<BruteForceRequest>(bodyContent);
        }

        private IActionResult CreateErrorResponse(string message, int statusCode)
        {
            ILogService.LogError(logServices, message);
            BruteForceResponse response = new()
            {
                Message = message,
                Time = -1,
                CalculationTime = -1
            };
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => new BadRequestObjectResult(response),
                StatusCodes.Status404NotFound => new NotFoundObjectResult(response),
                _ => new ObjectResult(response) { StatusCode = statusCode }
            };
        }

        private BruteForceResult ExecuteBruteForce(DateTime startTime, BruteForceRequest requestData, string hash)
        {
            DateTime calculationStartTime = DateTime.UtcNow;
            int setupTime = (int)(calculationStartTime - startTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"Setup completed in {setupTime}ms");
            string? foundPassword = PerformBruteForce(requestData.Chars, requestData.PasswordLength, hash);
            DateTime endTime = DateTime.UtcNow;
            int totalTime = (int)(endTime - startTime).TotalMilliseconds;
            int calculationTime = (int)(endTime - calculationStartTime).TotalMilliseconds;
            int communicationTime = totalTime - calculationTime;
            LogTimingMetrics(totalTime, calculationTime, communicationTime);
            return new BruteForceResult
            {
                Password = foundPassword,
                TotalTime = totalTime,
                CalculationTime = calculationTime
            };
        }

        private void LogTimingMetrics(int totalTime, int calculationTime, int communicationTime)
        {
            ILogService.LogInfo(logServices,
                $"[BruteForce] Total = {totalTime} ms | " +
                $"Calculation = {calculationTime} ms | " +
                $"Communication time = {communicationTime} ms");
        }

        private IActionResult CreateBruteForceResponse(BruteForceResult result)
        {
            if (result.Password != null)
            {
                ILogService.LogInfo(logServices, $"Password found: {result.Password}");
                return new OkObjectResult(new BruteForceResponse
                {
                    Message = "Password found.",
                    Password = result.Password,
                    Time = result.TotalTime,
                    CalculationTime = result.CalculationTime
                });
            }
            else
            {
                ILogService.LogInfo(logServices, "Password not found in the given range.");
                return new OkObjectResult(new BruteForceResponse
                {
                    Message = "Password not found.",
                    Time = result.TotalTime,
                    CalculationTime = result.CalculationTime
                });
            }
        }

        private IActionResult HandleException(Exception ex, DateTime startTime)
        {
            DateTime endTime = DateTime.UtcNow;
            int totalTime = (int)(endTime - startTime).TotalMilliseconds;
            ILogService.LogError(logServices, $"Error during brute force: {ex.Message}");
            BruteForceResponse bruteForceResponse = new()
            {
                Message = $"An error occurred during brute force: {ex.Message}",
                Time = totalTime,
                CalculationTime = -1
            };
            return new ObjectResult(bruteForceResponse)
            {
                StatusCode = 500
            };
        }

        private async Task<string?> GetHashFromDatabase(string userLogin)
        {
            try
            {
                ILogService.LogInfo(logServices, $"Looking up hash for user: {userLogin}");
                return await _passwordRepository.GetPasswordHash(userLogin);
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error getting hash for user {userLogin}: {ex.Message}");
                return null;
            }
        }

        private string? PerformBruteForce(string chars, int passwordLength, string targetHash)
        {
            DateTime bruteForceStartTime = DateTime.UtcNow;
            ILogService.LogInfo(logServices, $"Starting brute force with chars: '{chars}', length: {passwordLength}, targetHash: {targetHash}");
            BigInteger combinationCount = 0;
            int charSetSize = chars.Length;
            BigInteger maxCombinations;
            if (passwordLength > 20)
            {
                maxCombinations = BigInteger.Pow(charSetSize, passwordLength);
                ILogService.LogInfo(logServices, $"Character set size: {charSetSize}, Password length: {passwordLength}, Very large search space");
            }
            else
            {
                maxCombinations = (long)Math.Pow(charSetSize, passwordLength);
                ILogService.LogInfo(logServices, $"Character set size: {charSetSize}, Max combinations: {maxCombinations}");
            }
            const long MAX_COMBINATIONS_TO_CHECK = long.MaxValue;
            for (BigInteger i = 0; i < maxCombinations && i < MAX_COMBINATIONS_TO_CHECK; i++)
            {
                combinationCount++;
                string combination = IndexToCombination(i, chars, passwordLength);
                string computedHash = CalculateMD5Hash(combination);
                if (computedHash == targetHash)
                {
                    LogPasswordFound(combination, combinationCount, bruteForceStartTime);
                    return combination;
                }
                LogProgressIfNeeded(combinationCount, bruteForceStartTime);
            }
            LogNoMatchFound(combinationCount, bruteForceStartTime);
            return null;
        }


        private static string IndexToCombination(BigInteger index, string charset, int length)
        {
            char[] result = new char[length];
            int baseN = charset.Length;
            for (int i = length - 1; i >= 0; i--)
            {
                int charIndex = (int)(index % baseN);
                result[i] = charset[charIndex];
                index /= baseN;
            }
            return new string(result);
        }

        private static string CalculateMD5Hash(string input)
        {
            using MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void LogPasswordFound(string combination, BigInteger combinationCount, DateTime startTime)
        {
            DateTime endTime = DateTime.UtcNow;
            int bruteForceTime = (int)(endTime - startTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"Match found after {combinationCount} combinations in {bruteForceTime}ms! Password: {combination}");
        }

        private void LogProgressIfNeeded(BigInteger combinationCount, DateTime startTime)
        {
            if (combinationCount % LogInterval == 0)
            {
                DateTime currentTime = DateTime.UtcNow;
                int elapsedTime = (int)(currentTime - startTime).TotalMilliseconds;
                ILogService.LogInfo(logServices, $"Checked {combinationCount} combinations in {elapsedTime}ms");
            }
        }

        private void LogNoMatchFound(BigInteger combinationCount, DateTime startTime)
        {
            DateTime endTime = DateTime.UtcNow;
            int totalBruteForceTime = (int)(endTime - startTime).TotalMilliseconds;
            ILogService.LogInfo(logServices, $"No match found after checking {combinationCount} combinations in {totalBruteForceTime}ms");
        }
    }
}