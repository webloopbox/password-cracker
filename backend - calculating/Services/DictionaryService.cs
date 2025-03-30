namespace backend___calculating.Services
{
    public class DictionaryService(IEnumerable<ILogService> logServices) : IDictionaryService
    {
        private string DictionaryDirectory { get; set; } = "";
        private string[] DirectoryFiles { get; set; } = [];
        private readonly IEnumerable<ILogService> logServices = logServices;

        public async Task<IResult> SynchronizeDictionaryResult(HttpContext httpContext)
        {
            ILogService.LogInfo(logServices, "Synchronizing dictionary with central server");
            try
            {
                DictionaryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dictionary");
                HandleCreateDirectory();
                IFormCollection iFormCollection = await httpContext.Request.ReadFormAsync();
                IFormFile? iFormFile = iFormCollection.Files.GetFile("file");
                HandleValidateFile(iFormFile);
                string fileName = await HandleSaveFile(iFormFile);
                return Results.Ok(new { FileName = $"{fileName}.zip", Path = DictionaryDirectory });
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Error while synchronizing dictionary: {ex.Message}");
                return Results.Problem($"An error occurred while synchronizing dictionary pack file: {ex.Message}");
            }
        }

        private async Task<string> HandleSaveFile(IFormFile? iFormFile)
        {
            bool isDictionaryExists = GetIsDictionaryExists(iFormFile);
            if (isDictionaryExists && iFormFile != null)
            {
                return iFormFile.FileName;
            }
            return await HandleSaveDictionaryPack(iFormFile);
        }

        private async Task<string> HandleSaveDictionaryPack(IFormFile? iFormFile)
        {
            if (iFormFile != null)
            {
                string dictionaryLocation = Path.Combine(DictionaryDirectory, iFormFile.FileName);
                if (File.Exists(dictionaryLocation))
                {
                    ILogService.LogInfo(logServices, $"Dictionary file '{dictionaryLocation}' already exists. Skipping save.");
                    return dictionaryLocation;
                }
                using FileStream fileStream = new(dictionaryLocation, FileMode.CreateNew, FileAccess.Write);
                await iFormFile.CopyToAsync(fileStream);
                ILogService.LogInfo(logServices, "Successfully saved new dictionary");
                return dictionaryLocation;
            }
        
            ILogService.LogError(logServices, "Invalid dictionary filename");
            throw new Exception("Invalid dictionary filename");
        }

        private void HandleValidateFile(IFormFile? iFormFile)
        {
            bool isFormFileNotExists = iFormFile == null || iFormFile.Length == 0;
            bool isFormFileNameInvalid = iFormFile != null && !iFormFile.FileName.EndsWith(".zip");
            if (isFormFileNotExists || isFormFileNameInvalid)
            {
                ILogService.LogError(logServices, $"Received dictionary pack was invalid while trying to synchronize dictionary");
                throw new Exception("Invalid dictionary pack content or format.");
            }
        }

        private void HandleCreateDirectory()
        {
            if (!Directory.Exists(DictionaryDirectory))
            {
                Directory.CreateDirectory(DictionaryDirectory);
                ILogService.LogInfo(logServices, "Created dictionary directory");
            }
        }

        private bool GetIsDictionaryExists(IFormFile? iFormFile)
        {
            try
            {
                if (iFormFile != null)
                {
                    string centralLatestDictionaryHash = iFormFile.FileName;
                    string calculatingLatestDictionaryHash = GetLatestDictionaryHash();
                    return calculatingLatestDictionaryHash.Equals(centralLatestDictionaryHash);
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string GetLatestDictionaryHash()
        {
            FileInfo? latestFile = DirectoryFiles
                .Select(file => new FileInfo(file))
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .FirstOrDefault();
            if (latestFile == null)
            {
                ILogService.LogError(logServices, $"No valid dictionary hash files found while trying to synchronize dictionary");
                throw new Exception("No valid dictionary hash files found.");
            }
            string fileName = latestFile.Name;
            int startIndex = fileName.IndexOf("dictionary-");
            if (startIndex >= 0)
            {
                fileName = fileName[startIndex..];
            }
            return fileName;
        }
    }
}