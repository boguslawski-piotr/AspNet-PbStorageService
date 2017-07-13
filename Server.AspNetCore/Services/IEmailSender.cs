using System.Threading.Tasks;

namespace pbXStorage.Repositories.AspNetCore.Services
{
	public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }
}
