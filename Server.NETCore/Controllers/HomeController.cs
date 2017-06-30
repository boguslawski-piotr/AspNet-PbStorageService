using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using pbXStorage.Server.NETCore.Data;

namespace pbXStorage.Server.NETCore.Controllers
{
    public class HomeController : Controller
    {
		readonly Manager _manager;
		readonly ApplicationDbContext _dbContext;
		readonly UserManager<ApplicationUser> _userManager;
		readonly SignInManager<ApplicationUser> _signInManager;

		public HomeController(
			Manager manager, 
			ApplicationDbContext dbContext, 
			UserManager<ApplicationUser> userManager,
			SignInManager<ApplicationUser> signInManager)
		{
			_manager = manager;
			_dbContext = dbContext;
			_userManager = userManager;
			_signInManager = signInManager;
		}

		public IActionResult Index()
        {
            return View();
        }

		public IActionResult IndexWithSourceCode(string repositoryId)
		{
			ViewData["SourceCode"] = repositoryId;
			return View("Index");
		}

		async Task<ApplicationUser> GetUserAsync()
		{
			ApplicationUser _user = await _userManager.GetUserAsync(User);

			if (_user == null || !_signInManager.IsSignedIn(User))
				throw new Exception("Unauthorized access.");

			return _user;
		}

		public async Task<IActionResult> NewRepository()
		{
			this.Request.Host.ToString();
			ApplicationUser user = await GetUserAsync();

			Repository repository = await _manager.NewRepositoryAsync();

			user.Repositories.Add(
				new ApplicationUserRepository
				{
					RepositoryId = repository.Id,
					UserId = user.Id,
					User = user,
				}
			);

			await _dbContext.SaveChangesAsync();

			return RedirectToAction(nameof(HomeController.Index));
		}

		public async Task<IActionResult> RemoveRepository(string repositoryId)
		{
			ApplicationUser user = await GetUserAsync();

			_dbContext.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.Load();

			user.Repositories.RemoveAll((r) => r.RepositoryId == repositoryId);

			await _dbContext.SaveChangesAsync();

			await _manager.RemoveRepositoryAsync(repositoryId);

			return RedirectToAction(nameof(HomeController.Index));
		}

		public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
