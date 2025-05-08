using System.Threading.Tasks;

namespace backend___calculating.Interfaces
{
    public interface IPasswordRepository
    {
        Task Initialize();
        Task<string?> GetPasswordHash(string username);
        Task<bool> CheckPassword(string username, string hashedPassword);
    }
}