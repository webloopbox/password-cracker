using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using backend___central.Models;
using backend___central.Interfaces;

namespace backend___central.Services
{
    public class ResponseProcessingService : IResponseProcessingService
    {
        private readonly IEnumerable<ILogService> logServices;

        public ResponseProcessingService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
        }

        public IActionResult ProcessResults(ServerTaskResult taskResult, BruteForceRequestData credentials, CrackingCharPackage charPackage, int totalTime)
        {
            CrackingResult? successfulResult = FindSuccessfulResult(taskResult.Results);
            if (successfulResult != null && successfulResult.Success)
            {
                return CreateSuccessResponse(taskResult, successfulResult, credentials, charPackage, totalTime);
            }
            return CreateNotFoundResponse(taskResult, credentials, charPackage, totalTime);
        }
 
        private static CrackingResult? FindSuccessfulResult(IEnumerable<CrackingResult> results)
        {
            return results.FirstOrDefault(static result => result.Success);
        }

        private IActionResult CreateSuccessResponse(ServerTaskResult taskResult, CrackingResult successfulResult, BruteForceRequestData credentials, CrackingCharPackage charPackage, int totalTime)
        {
            int communicationTime = totalTime - successfulResult.Time;
            LogSuccessfulCentralTiming(totalTime, successfulResult.Time, communicationTime, successfulResult.ServerIp);
            return new OkObjectResult(CreateSuccessResponseObject(
                successfulResult, totalTime, communicationTime, 
                credentials, charPackage, taskResult));
        }
 
        private void LogSuccessfulCentralTiming(int totalTime, int calculationTime, int communicationTime, string serverIp)
        {
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Central: Total = {totalTime} ms" + 
                $" | Calculating: ({serverIp}) Total = {calculationTime} ms" + 
                $" | Communication time = {communicationTime} ms");
        }

        private static object CreateSuccessResponseObject(CrackingResult successfulResult, int totalTime, int communicationTime, BruteForceRequestData credentials, CrackingCharPackage charPackage, ServerTaskResult taskResult)
        {
            return new
            {
                Message = "Password found.",
                successfulResult.Password,
                Server = successfulResult.ServerIp,
                ServerExecutionTime = successfulResult.Time,
                TotalExecutionTime = totalTime,
                CommunicationTime = communicationTime,
                Timing = new
                {
                    credentials.ParseTime,
                    taskResult.TaskSetupTime,
                    taskResult.ProcessingTime,
                    TotalTime = totalTime
                }
            };
        }

        private IActionResult CreateNotFoundResponse(ServerTaskResult taskResult, BruteForceRequestData credentials, CrackingCharPackage charPackage, int totalTime)
        {
            List<CrackingResult> validResults = GetValidResults(taskResult.Results);
            int avgServerTime = CalculateAverageServerTime(validResults);
            int avgCommunicationTime = totalTime - avgServerTime;
            LogNotFoundCentralTiming(totalTime, avgServerTime, avgCommunicationTime);
            return new NotFoundObjectResult(CreateNotFoundResponseObject(
                totalTime, avgServerTime, avgCommunicationTime, validResults,
                credentials, charPackage, taskResult));
        }

        private static List<CrackingResult> GetValidResults(IEnumerable<CrackingResult> results)
        {
            return results.Where(result => result.Time != -1).ToList();
        }

        private static int CalculateAverageServerTime(List<CrackingResult> validResults)
        {
            return (int)(validResults.Any() ? validResults.Average(result => result.Time) : 0);
        }

        private void LogNotFoundCentralTiming(int totalTime, int avgServerTime, int avgCommunicationTime)
        {
            ILogService.LogInfo(logServices, 
                $"[BruteForce] Central: Total = {totalTime} ms" + 
                (avgServerTime > 0 ? $" | Calculating: (average) Total = {avgServerTime} ms" : "") + 
                $" | Communication time = {avgCommunicationTime} ms");
        }

        private static object CreateNotFoundResponseObject(int totalTime, int avgServerTime, int avgCommunicationTime, List<CrackingResult> validResults, BruteForceRequestData credentials, CrackingCharPackage charPackage, ServerTaskResult taskResult)
        {
            return new
            {
                Message = "Password not found by any server.",
                TotalExecutionTime = totalTime,
                AverageServerTime = avgServerTime,
                CommunicationTime = avgCommunicationTime,
                ServersTimes = validResults
                    .Select(result => new { Server = result.ServerIp, result.Time })
                    .ToList(),
                Timing = new
                {
                    credentials.ParseTime,
                    taskResult.TaskSetupTime,
                    taskResult.ProcessingTime,
                    TotalTime = totalTime
                }
            };
        }

        public IActionResult ProcessDictionaryResult(bool passwordFound, PasswordInfo? passwordInfo = null)
        {
            ILogService.LogInfo(logServices, $"ProcessDictionaryResult called with: passwordFound={passwordFound}, passwordInfo={(passwordInfo != null ? $"value={passwordInfo.Value}" : "null")}");
            
            if (passwordFound && passwordInfo != null)
            {
                ILogService.LogInfo(logServices, $"Returning dictionary success response with password: {passwordInfo.Value}");
                JsonResult response = new (new
                {
                    Message = "Password found!",
                    Password = passwordInfo.Value,
                    Server = passwordInfo.ServerIp,
                    Time = passwordInfo.ServerTime,
                    passwordInfo.TotalTime,
                    Status = "Found"
                })
                {
                    StatusCode = 200,
                    ContentType = "application/json"
                };
                ILogService.LogInfo(logServices, $"Response JSON: {System.Text.Json.JsonSerializer.Serialize(response.Value)}");
                return response;
            }
            ILogService.LogInfo(logServices, "Returning dictionary not found response");
            return new JsonResult(new
            {
                Message = "Password not found in dictionary.",
                Status = "NotFound",
            })
            {
                StatusCode = 200,
                ContentType = "application/json"
            };
        }

        public IActionResult HandleError(Exception ex, int executionTime, bool isDictionary = false)
        {
            if (isDictionary)
            {
                LogDictionaryCrackingError(ex);
                return new JsonResult(CreateDictionaryErrorObject(ex))
                {
                    StatusCode = 500,
                    ContentType = "application/json"
                };
            }
            else
            {
                LogBruteForceError(ex, executionTime);
                return new ContentResult
                {
                    Content = $"An error occurred while trying to connect calculating server: {ex.Message}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        private void LogDictionaryCrackingError(Exception ex)
        {
            ILogService.LogError(logServices, $"Error during dictionary cracking: {ex.Message}");
        }

        private static object CreateDictionaryErrorObject(Exception ex)
        {
            return new
            {
                Message = $"An error occurred while cracking password: {ex.Message}",
                StatusCode = 500,
                Time = -1
            };
        }

        private void LogBruteForceError(Exception ex, int errorTime)
        {
            ILogService.LogError(logServices,  $"Cannot connect to calculating server due to: {ex.Message} (after {errorTime}ms)");
        }
    }
}