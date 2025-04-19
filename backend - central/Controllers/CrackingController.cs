using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using backend___central.Services;
using System;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/cracking")]
    public class CrackingController : ControllerBase
    {
        private readonly ICrackingService crackingService;

        public CrackingController(ICrackingService crackingService)
        {
            this.crackingService = crackingService;
        }

        [HttpPost("brute-force")]
        public IActionResult CrackBruteForce()
        {
            return crackingService.HandleBruteForceCracking(HttpContext);
        }

        [HttpPost("dictionary")]
        public async Task<IActionResult> CrackDictionary()
        {
            return await crackingService.HandleDictionaryCracking(HttpContext);
        }
    }
}