	using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using pbXNet;
using pbXStorage.Repositories;
using pbXStorage.Server.AspNetCore.Data;
using pbXStorage.Server.AspNetCore.Services;

namespace pbXStorage.Server.AspNetCore.Controllers
{
	public class HomeController : Controller
    {
		Manager _manager;
		Context _context;
		UsersDb _usersDb;
		RepositoriesDbPool _repositoriesDbPool;
		UserManager<ApplicationUser> _userManager;
		SignInManager<ApplicationUser> _signInManager;

		public HomeController(
			Manager manager,
			ContextBuilder contextBuilder,
			ISerializer serializer,
			IDataProtectionProvider dataProtectionProvider,
			UsersDb usersDb,
			RepositoriesDbPool repositoriesDbPool,
			UserManager<ApplicationUser> userManager,
			SignInManager<ApplicationUser> signInManager)
		{
			_manager = manager;
			_usersDb = usersDb;
			_repositoriesDbPool = repositoriesDbPool;
			_userManager = userManager;
			_signInManager = signInManager;
			_context = contextBuilder.Build(repositoriesDbPool.Rent(), serializer, dataProtectionProvider);
		}

		protected override void Dispose(bool disposing)
		{
			_repositoriesDbPool?.Return(_context?.RepositoriesDb);

			_manager = null;
			_context = null;
			_usersDb = null;
			_userManager = null;
			_signInManager = null;
			_repositoriesDbPool = null;

			base.Dispose(disposing);
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

		public async Task<IActionResult> Repositories(string error)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			await _usersDb.Entry(user)
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

			ViewData["error"] = error;
			ViewData["repositories"] = repositories;

			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> NewRepository(string name)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			if (string.IsNullOrWhiteSpace(name))
			{
				return RedirectToAction(nameof(Repositories), new { error = "You can not create a repository without a name." });
			}

			Repository repository = await _manager.NewRepositoryAsync(_context, name);

			await _usersDb.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.LoadAsync();

			user.Repositories.Add(
				new ApplicationUserRepository
				{
					RepositoryId = repository.Id,
					ApplicationUserId = user.Id,
				}
			);

			await _usersDb.SaveChangesAsync();

			ViewData["error"] = null;
			return RedirectToAction(nameof(HomeController.Repositories));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Repository(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			Repository repository = await _manager.GetRepositoryAsync(_context, repositoryId);

			IEnumerable<IdInDb> ids = await repository.FindIdsAsync(_context, "");

			ViewData["repository"] = repository;
			ViewData["ids"] = ids;

			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RemoveRepository(string repositoryId)
		{
			ApplicationUser user = await CheckAndGetUserAsync();

			await _manager.RemoveRepositoryAsync(_context, repositoryId);

			await _usersDb.Entry(user)
				.Collection(nameof(ApplicationUser.Repositories))
				.LoadAsync();

			user.Repositories.RemoveAll((r) => r.RepositoryId == repositoryId);

			await _usersDb.SaveChangesAsync();

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
