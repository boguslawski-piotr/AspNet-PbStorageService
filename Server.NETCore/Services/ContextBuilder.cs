using Microsoft.AspNetCore.DataProtection;
using pbXNet;

namespace pbXStorage.Server.AspNetCore.Services
{
	public class ContextBuilder
    {
		string _serverId;

		public ContextBuilder(string serverId)
		{
			_serverId = serverId;
		}

		public Context Build(
			IDb repositoriesDb,
			ISerializer serializer,
			IDataProtectionProvider dataProtectionProvider)
		{
			return new Context
			{
				Cryptographer = new DataProtectorCryptographer(dataProtectionProvider.CreateProtector(_serverId)),
				RepositoriesDb = repositoriesDb,
				Serializer = serializer,
			};
		}
	}
}
