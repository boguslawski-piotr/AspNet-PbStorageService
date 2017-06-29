using System;

namespace pbXStorage.Server
{
	public class ManagedObject
	{
		[System.NonSerialized]
		public Manager Manager;

		public ManagedObject(Manager manager)
		{
			Manager = manager;
		}
	}
}
