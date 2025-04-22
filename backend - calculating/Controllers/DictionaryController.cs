using Microsoft.AspNetCore.Mvc;
using backend___calculating.Services;
using System.Threading.Tasks;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/dictionary")]
    public class DictionaryController : ControllerBase
    {
        private readonly IDictionaryService dictionaryService;

        public DictionaryController(IDictionaryService dictionaryService)
        {
            this.dictionaryService = dictionaryService;
        }

        [HttpPost("synchronizing")]
        public async Task<IActionResult> SynchronizeDictionary()
        {
            return await dictionaryService.SynchronizeDictionaryResult(HttpContext);
        }

        [HttpPost("cracking")]
        public async Task<IActionResult> StartCracking()
        {
            return await dictionaryService.StartCrackingResult(HttpContext);
        }
    }
}