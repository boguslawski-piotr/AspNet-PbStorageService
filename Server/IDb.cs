using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace pbXStorage.Server
{
	public interface IDb
	{
		bool Initialized { get; }
		Task InitializeAsync(Manager manager);

		Task StoreClientsAsync(string clientsData);
		Task<string> GetClientsAsync();

		Task StoreThingAsync(Storage storage, string thingId, string data);
		Task<bool> ThingExistsAsync(Storage storage, string thingId);
		Task<DateTime> GetThingModifiedOnAsync(Storage storage, string thingId);
		Task SetThingModifiedOnAsync(Storage storage, string thingId, DateTime modifiedOn);
		Task<string> GetThingCopyAsync(Storage storage, string thingId);
		Task DiscardThingAsync(Storage storage, string thingId);

		Task<IEnumerable<string>> FindThingIdsAsync(Storage storage, string pattern);
	};
}
