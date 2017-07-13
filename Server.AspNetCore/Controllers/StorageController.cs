using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using pbXNet;
using pbXStorage.Repositories.AspNetCore.Data;
using pbXStorage.Repositories.AspNetCore.Services;

namespace pbXStorage.Repositories.AspNetCore.Controllers
{
	[Route("api/[controller]")]
    public class StorageController : Controller
    {
		Manager _manager;
		Context _context;
		RepositoriesDbPool _repositoriesDbPool;

		public StorageController(
			Manager manager,
			ContextBuilder contextBuilder,
			ISerializer serializer,
			IDataProtectionProvider dataProtectionProvider,
			RepositoriesDbPool repositoriesDbPool)
		{
			_manager = manager;
			_repositoriesDbPool = repositoriesDbPool;
			_context = contextBuilder.Build(repositoriesDbPool.Rent(), serializer, dataProtectionProvider);
		}

		protected override void Dispose(bool disposing)
		{
			_repositoriesDbPool?.Return(_context.RepositoriesDb);

			_manager = null;
			_context = null;
			_repositoriesDbPool = null;

			base.Dispose(disposing);
		}

		[HttpPost("registerapp/{repositoryId}")]
		public async Task<string> RegisterApp(string repositoryId, [FromBody]string appPublicKey)
		{
			return await _manager.RegisterAppAsync(_context, repositoryId, appPublicKey);
		}

		[HttpGet("open/{appToken},{storageId}")]
        public async Task<string> Open(string appToken, string storageId)
        {
			return await _manager.OpenStorageAsync(_context, appToken, storageId);
        }

		[HttpPut("store/{storageToken},{thingId}")]
		public async Task<string> Store(string storageToken, string thingId, [FromBody]string data)
		{
			return await _manager.StoreThingAsync(_context, storageToken, thingId, data);
		}

		[HttpGet("exists/{storageToken},{thingId}")]
		public async Task<string> Exists(string storageToken, string thingId)
		{
			return await _manager.ThingExistsAsync(_context, storageToken, thingId);
		}

		[HttpGet("getmodifiedon/{storageToken},{thingId}")]
		public async Task<string> GetModifiedOn(string storageToken, string thingId)
		{
			return await _manager.GetThingModifiedOnAsync(_context, storageToken, thingId);
		}

		[HttpGet("getacopy/{storageToken},{thingId}")]
		public async Task<string> GetACopy(string storageToken, string thingId)
		{
			return await _manager.GetThingCopyAsync(_context, storageToken, thingId);
		}

		[HttpDelete("discard/{storageToken},{thingId}")]
        public async Task<string> Discard(string storageToken, string thingId)
        {
			return await _manager.DiscardThingAsync(_context, storageToken, thingId);
		}

		[HttpGet("findids/{storageToken},{pattern}")]
		public async Task<string> FindIds(string storageToken, string pattern)
		{
			return await _manager.FindThingIdsAsync(_context, storageToken, pattern);
		}
	}
}
