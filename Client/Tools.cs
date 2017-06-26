using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Client
{
    public static class Tools
    {
		public static readonly char[] commaCharArray = { ',' };

		public static HttpClient httpClient = new HttpClient();

		public static bool QuietMode = false;

		public static async Task<string> ExecuteCommandAsync(string cmd, Uri uri, HttpContent content = null)
		{
			httpClient.Timeout = TimeSpan.FromSeconds(30);

			try
			{
				if(!QuietMode)
					Console.WriteLine($"REQUEST: {cmd}: {uri}");

				HttpResponseMessage response = null;
				switch (cmd)
				{
					case "GET":
						response = await httpClient.GetAsync(uri);
						break;
					case "POST":
						response = await httpClient.PostAsync(uri, content);
						break;
					case "PUT":
						response = await httpClient.PutAsync(uri, content);
						break;
					case "DELETE":
						response = await httpClient.DeleteAsync(uri);
						break;
				}

				if (response != null)
				{
					if(!QuietMode)
						Console.Write("RESPONSE: ");

					if (response.IsSuccessStatusCode)
					{
						var responseContent = await response.Content.ReadAsStringAsync();
						responseContent = Obfuscator.DeObfuscate(responseContent);

						if(!QuietMode)
							Console.WriteLine($"{responseContent}");

						string[] contentData = responseContent.Split(commaCharArray, 2);
						if (contentData[0] == "ERROR")
						{
							throw new Exception(contentData[1]);
						}
						else if (contentData[0] != "OK")
						{
							throw new Exception("Incorrect data format.");
						}

						return contentData.Length > 1 ? contentData[1] : null;
					}
					else
						throw new Exception($"Failed to read data. Error: {response.StatusCode}.");
				}
				else
					throw new Exception($"Command {cmd} unrecognized.");
			}
			catch (Exception ex)
			{
				string message = $"{ex.Message}";
				if (ex.InnerException != null)
					message += $"\n{ex.InnerException.Message + (ex.InnerException.Message.EndsWith(".") ? "" : ".")}";
#if DEBUG
				message += $"\n{cmd}: {uri}";
#endif
				throw new Exception(message);
			}
		}
	}
}
