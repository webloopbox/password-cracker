using Microsoft.AspNetCore.Mvc;
using backend___central.Services;

namespace backend___central.Controllers
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

        [HttpGet("dictionary")]
        public IResult GetActualDictionaryFile()
        {
            return dictionaryService.GetCurrentDictionaryPackResult();
        }

        [HttpGet("dictionary-hash")]
        public IResult GetActualDictionaryHash()
        {
            return dictionaryService.GetCurrentDictionaryHashResult();
        }
    }
}