using Microsoft.AspNetCore.Mvc;
using backend___calculating.Services;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/synchronizing")]
    public class DictionaryController(IDictionaryService dictionaryService) : ControllerBase
    {
        private readonly IDictionaryService dictionaryService = dictionaryService;

        [HttpPost("dictionary")]
        public async Task<IResult> SynchronizeDictionary()
        {
            return await dictionaryService.SynchronizeDictionaryResult(HttpContext);
        }
    }
}