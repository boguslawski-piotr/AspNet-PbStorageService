using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace pbXStorage.Server.NETCore.Controllers
{
	[Route("api/[controller]")]
    public class StorageController : Controller
    {
		Manager _manager;

		public StorageController(Manager manager)
		{
			_manager = manager;
		}

		[HttpPost("registerapp/{repositoryId}")]
		public async Task<string> RegisterApp(string repositoryId, [FromBody]string appPublicKey)
		{
			return await _manager.RegisterAppAsync(repositoryId, appPublicKey);
		}

		[HttpGet("open/{appToken},{storageId}")]
        public async Task<string> Open(string appToken, string storageId)
        {
			return await _manager.OpenStorageAsync(appToken, storageId);
        }

		[HttpPut("store/{storageToken},{thingId}")]
		public async Task<string> Store(string storageToken, string thingId, [FromBody]string data)
		{
			return await _manager.StoreThingAsync(storageToken, thingId, data);
		}

		[HttpGet("exists/{storageToken},{thingId}")]
		public async Task<string> Exists(string storageToken, string thingId)
		{
			return await _manager.ThingExistsAsync(storageToken, thingId);
		}

		[HttpGet("getmodifiedon/{storageToken},{thingId}")]
		public async Task<string> GetModifiedOn(string storageToken, string thingId)
		{
			return await _manager.GetThingModifiedOnAsync(storageToken, thingId);
		}

		[HttpGet("getacopy/{storageToken},{thingId}")]
		public async Task<string> GetACopy(string storageToken, string thingId)
		{
			return await _manager.GetThingCopyAsync(storageToken, thingId);
		}

		[HttpDelete("discard/{storageToken},{thingId}")]
        public async Task<string> Discard(string storageToken, string thingId)
        {
			return await _manager.DiscardThingAsync(storageToken, thingId);
		}

		[HttpGet("findids/{storageToken},{pattern}")]
		public async Task<string> FindIds(string storageToken, string pattern)
		{
			return await _manager.FindThingIdsAsync(storageToken, pattern);
		}
	}
}
