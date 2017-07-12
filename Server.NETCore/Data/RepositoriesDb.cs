using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pbXNet;

namespace pbXStorage.Server.NETCore.Data
{
	public class RepositoriesDb : DbContext, IDb
	{
		IDb _implementation;

		public RepositoriesDb() :
			base()
		{ }

		public RepositoriesDb(DbContextOptions<RepositoriesDb> options)
			: base(options)
		{
			var e = options.FindExtension<DbOnFileSystemOptionsExtension>();
			if (e != null)
			{
				_implementation = new DbOnFileSystem(e.Directory);
			}
			else
			{
				var builder = new DbContextOptionsBuilder<DbOnEF>();
				foreach (var _e in options.Extensions)
					((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(_e);

				_implementation = new DbOnEF(builder.Options);
			}
		}

		public async Task CreateAsync()
		{
			await _implementation.CreateAsync();
		}

		Task IDb.StoreThingAsync(string storageId, string thingId, string data, DateTime modifiedOn, ISimpleCryptographer cryptographer = null)
			=> _implementation.StoreThingAsync(storageId, thingId, data, modifiedOn, cryptographer);

		Task<bool> IDb.ThingExistsAsync(string storageId, string thingId)
			=> _implementation.ThingExistsAsync(storageId, thingId);

		Task<DateTime> IDb.GetThingModifiedOnAsync(string storageId, string thingId)
			=> _implementation.GetThingModifiedOnAsync(storageId, thingId);

		Task<string> IDb.GetThingCopyAsync(string storageId, string thingId, ISimpleCryptographer cryptographer = null)
			=> _implementation.GetThingCopyAsync(storageId, thingId, cryptographer);

		Task IDb.DiscardThingAsync(string storageId, string thingId)
			=> _implementation.DiscardThingAsync(storageId, thingId);

		Task<IEnumerable<IdInDb>> IDb.FindThingIdsAsync(string storageId, string pattern)
			=> _implementation.FindThingIdsAsync(storageId, pattern);

		Task IDb.DiscardAllAsync(string storageId)
			=> _implementation.DiscardAllAsync(storageId);

		Task<IEnumerable<IdInDb>> IDb.FindAllIdsAsync(string storageId, string pattern)
			=> _implementation.FindAllIdsAsync(storageId, pattern);
	}
}
