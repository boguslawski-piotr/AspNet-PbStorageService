using System.ComponentModel.DataAnnotations;

namespace pbXStorage.Repositories.AspNetCore.Models.AccountViewModels
{
	public class ExternalLoginConfirmationViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
