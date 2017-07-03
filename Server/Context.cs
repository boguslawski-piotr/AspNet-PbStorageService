using pbXNet;

namespace pbXStorage.Server
{
	public class Context
	{
		public IDb RepositoriesDb { get; set; }
		public ISimpleCryptographer Cryptographer { get; set; }
		public ISerializer Serializer { get; set; }
	}
}
