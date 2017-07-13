using System.ComponentModel.DataAnnotations;

namespace pbXStorage.Server.AspNetCore.Models.AccountViewModels
{
	public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
