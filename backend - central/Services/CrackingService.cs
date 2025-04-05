using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public class CrackingService : ICrackingService
    {
        private readonly IEnumerable<ILogService> logServices;

        public CrackingService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
        }

        public IActionResult HandleBruteForceCracking(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");
            ILogService.LogInfo(logServices, "Final cracking time with brute force method was: 03:34:10. Cracking was unsuccessfull.");
            return new ContentResult {
                Content = "Started brute force password cracking.",
                StatusCode = 202
            };
        }

        public IActionResult HandleDictionaryCracking(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using dictionary method");
            ILogService.LogInfo(logServices, "Final cracking time with dictionary method was: 01:34:10. Cracking was successfull.");
            return new ContentResult {
                Content = "Started dictionary password cracking.",
                StatusCode = 202
            };
        }
    }
}