namespace pbXStorage.Repositories
{
	public interface ISimpleCryptographer
	{
		string Encrypt(string data);
		string Decrypt(string data);
	}
}
