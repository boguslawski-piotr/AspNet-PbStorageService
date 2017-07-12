using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using pbXNet;

namespace pbXStorage.Server
{
	public class Thing
	{
		public string StorageId { get; set; }
		public string Id { get; set; }
		public string Data { get; set; }
		public DateTime ModifiedOn { get; set; }
	}

	public class DbOnEF : DbContext, IDb
	{
		public DbSet<Thing> Things { get; set; }

		public DbOnEF() 
			: base()
		{ }

		public DbOnEF(DbContextOptions<DbOnEF> options)
			: base(options)
		{
		}

		public async Task CreateAsync()
		{
			await Database.EnsureCreatedAsync();
			//Database.Migrate();
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			builder.Entity<Thing>()
				.HasKey(t => new { t.StorageId, t.Id });
			builder.Entity<Thing>()
				.HasIndex(t => t.StorageId);
			builder.Entity<Thing>()
				.HasIndex(t => t.Id);
		}

		public async Task StoreThingAsync(string storageId, string thingId, string data, DateTime modifiedOn, ISimpleCryptographer cryptographer = null)
		{
			Thing t = await Things.FindAsync(storageId, thingId);
			if (t == null)
			{
				t = new Thing
				{
					StorageId = storageId,
					Id = thingId,
				};

				Things.Add(t);
			}

			if (cryptographer != null)
				data = cryptographer.Encrypt(data);

			t.Data = data;
			t.ModifiedOn = modifiedOn;

			await SaveChangesAsync();
		}

		public async Task<bool> ThingExistsAsync(string storageId, string thingId)
		{
			return await Things.FindAsync(storageId, thingId) != null;
		}

		public async Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId)
		{
			Thing t = await Things.FindAsync(storageId, thingId);
			if (t == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));

			return t.ModifiedOn;
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId, ISimpleCryptographer cryptographer = null)
		{
			Thing t = await Things.FindAsync(storageId, thingId);
			if (t == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));

			string data = t.Data;

			if (cryptographer != null)
				data = cryptographer.Decrypt(data);

			return data;
		}

		public async Task DiscardThingAsync(string storageId, string thingId)
		{
			Thing t = await Things.FindAsync(storageId, thingId);
			if (t != null)
			{
				Things.Remove(t);
				await SaveChangesAsync();
			}
		}

		public async Task<IEnumerable<IdInDb>> FindThingIdsAsync(string storageId, string pattern)
		{
			IEnumerable<Thing> ts = null;

			if (string.IsNullOrWhiteSpace(pattern))
				ts = Things
					.AsNoTracking()
					.Where((_t) => _t.StorageId == storageId);
			else
				ts = Things
					.AsNoTracking()
					.Where((_t) =>
						_t.StorageId == storageId &&
						Regex.IsMatch(_t.Id, pattern)
					);

			return ts.Select<Thing, IdInDb>((t) => new IdInDb { Type = IdInDbType.Thing, Id = t.Id, StorageId = t.StorageId });
		}

		IEnumerable<Thing> FindAllIds(string storageId, string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
				return Things
					.AsNoTracking()
					.Where((_t) => _t.StorageId.StartsWith(storageId));

			return Things
				.AsNoTracking()
				.Where((_t) =>
					_t.StorageId.StartsWith(storageId) &&
					Regex.IsMatch(_t.Id, pattern)
				);
		}

		public async Task DiscardAllAsync(string storageId)
		{
			IEnumerable<Thing> ts = FindAllIds(storageId, "");
			Things.RemoveRange(ts);
			await SaveChangesAsync();
		}

		public async Task<IEnumerable<IdInDb>> FindAllIdsAsync(string storageId, string pattern)
		{
			List<IdInDb> ids = new List<IdInDb>();

			IEnumerable<Thing> ts = FindAllIds(storageId, pattern);

			var tids = ts.Select<Thing, IdInDb>((t) => new IdInDb { Type = IdInDbType.Thing, Id = t.Id, StorageId = t.StorageId });

			ids.AddRange(tids);

			IEnumerable<IGrouping<string, Thing>> ss = ts.GroupBy<Thing, string>((_t) => _t.StorageId);

			var sids = ss.Select((IGrouping<string, Thing> s) =>
			{
				string[] _sids = s.Key.Split('/');
				return new IdInDb { Type = IdInDbType.Storage, Id = _sids[_sids.Length - 1], StorageId = _sids[0] };
			});

			ids.AddRange(sids);

			return ids;
		}
	}
}
