namespace backend___central.Models
{
    public class BruteForceRequestData
    {
        public int ParseTime { get; }
        public string UserLogin { get; }
        public int PasswordLength { get; }

        public BruteForceRequestData(int parseTime, string userLogin, int passwordLength)
        {
            ParseTime = parseTime;
            UserLogin = userLogin;
            PasswordLength = passwordLength;
        }
    }
}