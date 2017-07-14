using Microsoft.AspNetCore.DataProtection;
using pbXStorage.Repositories;

namespace pbXStorage.Server.AspNetCore.Services
{
	class DataProtectorCryptographer : ISimpleCryptographer
	{
		IDataProtector _protector;

		public DataProtectorCryptographer(IDataProtector protector)
		{
			_protector = protector;
		}

		public string Encrypt(string data) => _protector.Protect(data);
		public string Decrypt(string data) => _protector.Unprotect(data);
	}
}
