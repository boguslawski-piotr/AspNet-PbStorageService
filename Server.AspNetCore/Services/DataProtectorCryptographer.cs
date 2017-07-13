using Microsoft.AspNetCore.DataProtection;

namespace pbXStorage.Repositories.AspNetCore.Services
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
