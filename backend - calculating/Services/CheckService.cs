using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Services {
    public class CheckService: ICheckService {
        private readonly IEnumerable<ILogService> logServices;
        private string DictionaryDirectory { get; set; } = "";

        public CheckService(IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
        }

        public IActionResult HandleCheckConnectionRequest(HttpContext httpContext) 
        {
            try 
            {
                using StreamReader reader = new (httpContext.Request.Body);
                string bodyContent = reader.ReadToEndAsync().Result;
                DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                string dictionaryLocation = Path.Combine(DictionaryDirectory, bodyContent);
                if (File.Exists(dictionaryLocation))
                {
                    ILogService.LogInfo(logServices, $"Dictionary file '{dictionaryLocation}' already exists.");
                    return new OkObjectResult(dictionaryLocation);
                }
                return new OkResult();
            } 
            catch (Exception ex) 
            {
                ILogService.LogError(logServices, $"Error handling check connection request: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}