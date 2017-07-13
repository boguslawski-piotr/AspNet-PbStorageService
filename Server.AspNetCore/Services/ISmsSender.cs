using System.Threading.Tasks;

namespace pbXStorage.Repositories.AspNetCore.Services
{
	public interface ISmsSender
    {
        Task SendSmsAsync(string number, string message);
    }
}
