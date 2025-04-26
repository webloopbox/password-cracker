namespace backend___central
{
    public class CrackingCredentials
    {

        public int ParseTime { get; set; }
        public string UserLogin { get; set; }
        public int PasswordLength { get; set; }

        public CrackingCredentials()
        {
            ParseTime = 0;
            UserLogin = "";
            PasswordLength = 0;
        }

        public CrackingCredentials(int parseTime, string userLogin, int passwordLength)
        {
            ParseTime = parseTime;
            UserLogin = userLogin;
            PasswordLength = passwordLength;
        }
    }
}