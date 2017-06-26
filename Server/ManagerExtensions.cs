using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public static class ManagerExtensions
	{
		public static async Task<Manager> UseDbAsync<T>(this Manager manager) where T : IDb, new()
		{
			try
			{
				IDb db = new T();
				await db.InitializeAsync(manager).ConfigureAwait(false);
				manager.Db = db;
			}
			catch (Exception ex)
			{
				Log.E(ex.Message);
				throw ex;
			}

			return manager;
		}
	}
}
