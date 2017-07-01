using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using pbXNet;
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

		async Task<ApplicationUser> GetUserAsync()
		{
			if (!_signInManager.IsSignedIn(User))
				return null;

			ApplicationUser user = await _userManager.GetUserAsync(User);
			return user;
		}

		async Task<ApplicationUser> CheckAndGetUserAsync()
		{
			ApplicationUser user = await GetUserAsync();
			if (user == null)
				throw new Exception("Unauthorized access.");
			return user;
		}

		public async Task<IActionResult> Index()
        {
			if(await GetUserAsync() != null)
				return RedirectToAction(nameof(HomeController.Repositories));

			return View();
        }

		public async Task<IActionResult> Repositories(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			await _dbContext.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.LoadAsync();

			IList<Repository> repositories = new List<Repository>();

			foreach (var r in user.Repositories)
			{
				try
				{
					repositories.Add(await _manager.GetRepositoryAsync(r.RepositoryId));
				}
				catch (Exception) { }
			}

			ViewData["repositories"] = repositories;
			ViewData["repositoryWithIDPK"] = repositoryId;

			return View();
		}

		public async Task<IActionResult> NewRepository(string name)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			Repository repository = await _manager.NewRepositoryAsync(name);

			user.Repositories.Add(
				new ApplicationUserRepository
				{
					RepositoryId = repository.Id,
					UserId = user.Id,
					User = user,
				}
			);

			await _dbContext.SaveChangesAsync();

			return RedirectToAction(nameof(HomeController.Repositories));
		}

		public async Task<IActionResult> Repository(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			Repository repository = await _manager.GetRepositoryAsync(repositoryId);

			ViewData["repository"] = repository;

			return View();
		}

		public async Task<IActionResult> RemoveRepository(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			await _manager.RemoveRepositoryAsync(repositoryId);

			_dbContext.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.Load();

			user.Repositories.RemoveAll((r) => r.RepositoryId == repositoryId);

			await _dbContext.SaveChangesAsync();

			return RedirectToAction(nameof(HomeController.Repositories));
		}

		public IActionResult About()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
