namespace pbXStorage.Server
{
	public interface IDbFactory
	{
		IDb Create(string connectionString);
	}
}
