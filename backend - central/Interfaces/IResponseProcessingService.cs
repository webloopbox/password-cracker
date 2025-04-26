using Microsoft.AspNetCore.Mvc;
using backend___central.Models;

namespace backend___central.Interfaces
{
    public interface IResponseProcessingService
    {
        IActionResult ProcessResults(ServerTaskResult taskResult, BruteForceRequestData credentials, CrackingCharPackage charPackage, int totalTime);
        IActionResult ProcessDictionaryResult(bool passwordFound, PasswordInfo? passwordInfo);
        IActionResult HandleError(System.Exception ex, int executionTime, bool isDictionary = false);
    }
}