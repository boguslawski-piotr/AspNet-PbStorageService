using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace pbXStorage.Server
{
	public interface IDb
	{
		Task InitializeAsync();

		Task StoreClientsAsync(string clientsData);
		Task<string> GetClientsAsync();

		Task StoreThingAsync(Storage storage, string thingId, string data);
		Task<string> GetThingCopyAsync(Storage storage, string thingId);
		Task DiscardThingAsync(Storage storage, string thingId);
	};
}
