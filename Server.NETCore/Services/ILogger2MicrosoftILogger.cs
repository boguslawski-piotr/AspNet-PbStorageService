using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pbXNet;

namespace pbXStorage.Server.NETCore
{
	class ILogger2MicrosoftILogger : pbXNet.ILogger
	{
		Microsoft.Extensions.Logging.ILogger _logger;

		public ILogger2MicrosoftILogger(Microsoft.Extensions.Logging.ILogger logger)
		{
			_logger = logger;
		}

		public void L(DateTime dt, LogType type, string msg)
		{
			msg = dt.ToString("yyyy-M-d H:m:s.fff") + ": " + $"{msg}";
			switch (type)
			{
				case LogType.Debug:
					_logger.LogDebug(msg);
					break;
				case LogType.Info:
					_logger.LogInformation(msg);
					break;
				case LogType.Warning:
					_logger.LogWarning(msg);
					break;
				case LogType.Error:
					_logger.LogError(msg);
					break;
			}
		}
	}
}
