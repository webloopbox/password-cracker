using Microsoft.AspNetCore.Mvc;
using backend___central.Services;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/cracking")]
    public class CrackingController(ICrackingService crackingService) : ControllerBase
    {
        private readonly ICrackingService crackingService = crackingService;

        [HttpPost("brute-force")]
        public IResult CrackBruteForce()
        {
            return crackingService.HandleBruteForceCracking(HttpContext);
        }

        [HttpPost("dictionary")]
        public IResult CrackDictionary()
        {
            return crackingService.HandleDictionaryCracking(HttpContext);
        }
    }
}