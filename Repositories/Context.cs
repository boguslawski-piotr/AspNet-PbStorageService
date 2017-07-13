using pbXNet;

namespace pbXStorage.Repositories
{
	public class Context
	{
		public IDb RepositoriesDb { get; set; }
		public ISimpleCryptographer Cryptographer { get; set; }
		public ISerializer Serializer { get; set; }
	}
}
