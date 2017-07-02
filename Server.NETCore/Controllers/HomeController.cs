using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using pbXNet;
using pbXStorage.Server.NETCore.Data;

namespace pbXStorage.Server.NETCore.Controllers
{
    public class HomeController : Controller
    {
		readonly Manager _manager;
		readonly Context _context;
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
			_context = _manager.CreateContext(dbContext.RepositoriesDb);
			_dbContext = dbContext;
			_userManager = userManager;
			_signInManager = signInManager;
		}

		async Task<ApplicationUser> GetUserAsync()
		{
			if (!_signInManager.IsSignedIn(User))
				return null;
			return await _userManager.GetUserAsync(User);
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
					repositories.Add(await _manager.GetRepositoryAsync(_context, r.RepositoryId));
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

			Repository repository = await _manager.NewRepositoryAsync(_context, name);

			await _dbContext.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.LoadAsync();

			user.Repositories.Add(
				new ApplicationUserRepository
				{
					RepositoryId = repository.Id,
					ApplicationUserId = user.Id,
				}
			);

			await _dbContext.SaveChangesAsync();

			return RedirectToAction(nameof(HomeController.Repositories));
		}

		public async Task<IActionResult> Repository(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			Repository repository = await _manager.GetRepositoryAsync(_context, repositoryId);

			IEnumerable<IdInDb> ids = await repository.FindIdsAsync(_context.RepositoriesDb, "");

			ViewData["repository"] = repository;
			ViewData["ids"] = ids;

			return View();
		}

		public async Task<IActionResult> RemoveRepository(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			await _manager.RemoveRepositoryAsync(_context, repositoryId);

			await _dbContext.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.LoadAsync();

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
