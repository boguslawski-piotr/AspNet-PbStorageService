﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

// register (special util, web page, etc.) -> clientId, clientPublicKey
// app has:
//   clientId
//   clientPublicKey
// server has:
//   clientId
//   clientPublicKey
//   clientPrivateKey

// app run
// generate:
//   appPrivateKey
//   appPublicKey

// hello (post clientId) <- (appPublicKey) encrypted with clientPublicKey -> (appToken) signed with clientPrivateKey
// app has:
//   appToken
// server has
//   appPublicKey

// open (get appToken, storageId) <- nothing -> (storageToken, storagePublicKey) encrypted with appPublicKey, and signed with clientPrivateKey
// app has:
//   storageToken
//   storagePublicKey
// server has
//   storageToken
//   storagePrivateKey

// store (put storageToken, thingId) <- (data) encrypted with storagePublicKey, and signed with appPrivateKey -> OK or error

// getACopy (get storageToken, thingId) <- nothing -> (data) encrypted with appPublicKey, and signed with storagePrivateKey

// etc...

namespace pbXStorage.Server.Controllers
{
    [Route("api/[controller]")]
    public class StorageController : Controller
    {
		Manager _manager;

		public StorageController(Manager manager)
		{
			_manager = manager;
		}

		// GET api/storage
		[HttpGet]
        public string Get()
        {
			return "pbXStorage"; // TODO: jakas strona powitalna, z dokumentacja, itp.
        }

#if DEBUG
		// GET api/storage/newclient
		[HttpGet("newclient")]
        public async Task<string> NewClient()
        {
			return await _manager.NewClientAsync();
        }
#endif

		// POST api/storage/registerapp
		[HttpPost("registerapp/{clientId}")]
		public async Task<string> RegisterApp(string clientId, [FromBody]string appPublicKey)
		{
			return await _manager.RegisterAppAsync(clientId, appPublicKey);
		}

		// GET api/storage/open
		[HttpGet("open/{appToken},{storageId}")]
        public async Task<string> Open(string appToken, string storageId)
        {
			return await _manager.OpenStorageAsync(appToken, storageId);
        }

		// PUT api/storage/store
		[HttpPut("store/{storageToken},{thingId}")]
		public async Task<string> Store(string storageToken, string thingId, [FromBody]string data)
		{
			return await _manager.StoreAsync(storageToken, thingId, data);
		}

		// GET api/storage/getacopy
		[HttpGet("getacopy/{storageToken},{thingId}")]
		public async Task<string> GetACopy(string storageToken, string thingId)
		{
			return await _manager.GetACopyAsync(storageToken, thingId);
		}


		// DELETE api/storage/5
		[HttpDelete("{id}")]
        public string Delete(int id, [FromBody]string value)
        {
			return $"from delete: {id}, {value}";
		}
	}
}
