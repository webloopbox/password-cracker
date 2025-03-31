namespace backend___central.Services
{
    public class CrackingService(IEnumerable<ILogService> logServices) : ICrackingService
    {
        private readonly IEnumerable<ILogService> logServices = logServices;

        public IResult HandleBruteForceCracking(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using brute force method");
            ILogService.LogInfo(logServices, "Final cracking time with brute force method was: 03:34:10. Cracking was unsuccessfull.");
            return Results.Accepted("Started brute force password cracking.");
        }

        public IResult HandleDictionaryCracking(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Made request to crack password using dictionary method");
            ILogService.LogInfo(logServices, "Final cracking time with dictionary method was: 01:34:10. Cracking was successfull.");
            return Results.Accepted("Started dictionary password cracking.");
        }
    }
}