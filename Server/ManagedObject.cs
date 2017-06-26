using System;
using System.Collections.Generic;
using System.Text;

namespace pbXStorage.Server
{
	public class ManagedObject
	{
		protected Manager Manager;

		public ManagedObject(Manager manager)
		{
			Manager = manager;
		}
	}
}
