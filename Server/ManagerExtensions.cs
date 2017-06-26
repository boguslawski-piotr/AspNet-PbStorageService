using pbXNet;

namespace pbXStorage.Server
{
	public static class ManagerExtensions
	{
		public static Manager UseSerializer<T>(this Manager manager) where T : ISerializer, new() => UseSerializer(manager, new T());

		public static Manager UseSerializer(this Manager manager, ISerializer serializer)
		{
			manager.Serializer = serializer;
			return manager;
		}

		public static Manager UseSimpleCryptographer<T>(this Manager manager) where T : ISimpleCryptographer, new() => UseSimpleCryptographer(manager, new T());

		public static Manager UseSimpleCryptographer(this Manager manager, ISimpleCryptographer cryptographer)
		{
			manager.Cryptographer = cryptographer;
			return manager;
		}

		public static Manager UseDb<T>(this Manager manager) where T : IDb, new() => UseDb(manager, new T());

		public static Manager UseDb(this Manager manager, IDb db)
		{
			manager.Db = db;
			return manager;
		}
	}

	public static class ManagerExtensionsEx
	{
		public static Manager UseNewtonsoftJSonSerializer(this Manager manager) => ManagerExtensions.UseSerializer<NewtonsoftJsonSerializer>(manager);
	}
}
