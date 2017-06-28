using System;
using System.Reflection;
using pbXNet;

namespace pbXStorage.Server
{
	public static class ManagerExtensionsEx
	{
		public static Manager UseNewtonsoftJSonSerializer(this Manager manager) => ManagerExtensions.UseSerializer<NewtonsoftJsonSerializer>(manager);
	}

	public static class ManagerExtensions
	{
		public static Manager SetId(this Manager manager, string id)
		{
			manager.Id = id;
			return manager;
		}

		public static Manager UseSerializer<T>(this Manager manager) where T : ISerializer, new() => UseSerializer(manager, new T());

		public static Manager UseSerializer(this Manager manager, ISerializer serializer)
		{
			TrySetManagerProperty(serializer, manager);
			manager.Serializer = serializer;
			return manager;
		}

		public static Manager UseSimpleCryptographer<T>(this Manager manager) where T : ISimpleCryptographer, new() => UseSimpleCryptographer(manager, new T());

		public static Manager UseSimpleCryptographer(this Manager manager, ISimpleCryptographer cryptographer)
		{
			TrySetManagerProperty(cryptographer, manager);
			manager.Cryptographer = cryptographer;
			return manager;
		}

		public static Manager UseDb<T>(this Manager manager) where T : IDb, new() => UseDb(manager, new T());

		public static Manager UseDb(this Manager manager, IDb db)
		{
			TrySetManagerProperty(db, manager);
			manager.Db = db;
			return manager;
		}

		static void TrySetManagerProperty(object obj, Manager manager)
		{
			void TrySet(Type type)
			{
				if (type == null)
					return;

				PropertyInfo p = type.GetProperty("Manager");
				if (p != null)
					p.SetValue(obj, manager);
				else
				{
					FieldInfo f = type.GetField("Manager");
					if (f != null)
						f.SetValue(obj, manager);
				}
			}

			TrySet(obj.GetType());
		}
	}
}
