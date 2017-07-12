using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace pbXStorage.Server.NETCore.Data
{
	public class ApplicationUser : IdentityUser
	{
		public List<ApplicationUserRepository> Repositories { get; set; }
	}
}
