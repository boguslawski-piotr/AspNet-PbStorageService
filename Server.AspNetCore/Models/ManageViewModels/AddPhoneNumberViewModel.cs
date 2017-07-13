using System.ComponentModel.DataAnnotations;

namespace pbXStorage.Repositories.AspNetCore.Models.ManageViewModels
{
	public class AddPhoneNumberViewModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }
    }
}
