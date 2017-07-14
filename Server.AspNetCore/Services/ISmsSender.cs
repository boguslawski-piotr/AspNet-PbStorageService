using System.Threading.Tasks;

namespace pbXStorage.Server.AspNetCore.Services
{
	public interface ISmsSender
    {
        Task SendSmsAsync(string number, string message);
    }
}
