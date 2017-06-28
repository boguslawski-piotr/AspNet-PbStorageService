using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace pbXStorage.Server
{
	public interface IDb
	{
		// In the class that will implement this interface you can define
		// <c>public Manager Manager</c>
		// and then the UseDb(...) extension will set this field to a valid value.

		bool Initialized { get; }
		Task InitializeAsync();

		Task StoreThingAsync(string storageId, string thingId, string data);
		Task<bool> ThingExistsAsync(string storageId, string thingId);
		Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId);
		Task SetThingModifiedOnAsync(string storageId, string thingId, DateTime modifiedOn);
		Task<string> GetThingCopyAsync(string storageId, string thingId);
		Task DiscardThingAsync(string storageId, string thingId);

		Task<IEnumerable<string>> FindThingIdsAsync(string storageId, string pattern);
	};
}
