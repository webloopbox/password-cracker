using System.Threading.Tasks;
using backend___central.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public class CrackingService : ICrackingService
    {
        private readonly IBruteForceCrackingService bruteForceCrackingService;
        private readonly IDictionaryCrackingService dictionaryCrackingService;

        public CrackingService(IBruteForceCrackingService bruteForceService, IDictionaryCrackingService dictionaryService)
        {
            bruteForceCrackingService = bruteForceService;
            dictionaryCrackingService = dictionaryService;
        }

        public async Task<IActionResult> HandleBruteForceRequest(HttpContext httpContext)
        {
            return await bruteForceCrackingService.HandleBruteForceRequest(httpContext);
        }

        public async Task<IActionResult> HandleDictionaryCracking(HttpContext httpContext)
        {
            return await dictionaryCrackingService.HandleDictionaryCracking(httpContext);
        }
    }
}