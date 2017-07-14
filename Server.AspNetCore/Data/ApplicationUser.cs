using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace pbXStorage.Server.AspNetCore.Data
{
	public class ApplicationUser : IdentityUser
	{
		public List<ApplicationUserRepository> Repositories { get; set; }
	}
}
