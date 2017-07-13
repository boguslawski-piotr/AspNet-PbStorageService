namespace pbXStorage.Repositories
{
	public class DBOnFileSystemFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnFileSystem(connectionString);
		}
	}
}
