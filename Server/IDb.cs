using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace pbXStorage.Server
{
	public interface IDb
	{
		bool Initialized { get; }
		Task InitializeAsync(Manager manager);

		Task StoreThingAsync(string storageId, string thingId, string data);
		Task<bool> ThingExistsAsync(string storageId, string thingId);
		Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId);
		Task SetThingModifiedOnAsync(string storageId, string thingId, DateTime modifiedOn);
		Task<string> GetThingCopyAsync(string storageId, string thingId);
		Task DiscardThingAsync(string storageId, string thingId);

		Task<IEnumerable<string>> FindThingIdsAsync(string storageId, string pattern);
	};
}
