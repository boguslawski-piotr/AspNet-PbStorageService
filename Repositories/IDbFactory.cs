namespace pbXStorage.Repositories
{
	public interface IDbFactory
	{
		IDb Create(string connectionString);
	}
}
