using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

//
// special tool -> client id, public Skey
//
// client id -> new storage(public Ckey encrypted with public Skey) -> storage id, public storageSkey
//
// client id -> storage id -> store(data id, data encrypted with public storageSkey and signed with private Ckey)
//
// client id -> storage id -> get(data id) -> data encrypted with public Ckey and signed with private storageSkey 
//

namespace pbXStorage.AspNetCore.MVC.Controllers
{
	class MyMainClass
	{
		public static void Test()
		{
			byte[] toEncrypt;
			byte[] encrypted;
			byte[] signature;
			//Choose a small amount of data to encrypt.
			string original = "Hello";
			ASCIIEncoding myAscii = new ASCIIEncoding();

			//Create a sender and receiver.
			Sender mySender = new Sender();
			Receiver myReceiver = new Receiver();

			//Convert the data string to a byte array.
			toEncrypt = myAscii.GetBytes(original);

			//Encrypt data using receiver's public key.
			encrypted = mySender.EncryptData(myReceiver.PublicParameters, toEncrypt);

			//Hash the encrypted data and generate a signature on the hash
			// using the sender's private key.
			signature = mySender.HashAndSign(encrypted);

			Console.WriteLine("Original: {0}", original);

			//Verify the signature is authentic using the sender's public key.
			if (myReceiver.VerifyHash(mySender.PublicParameters, encrypted, signature))
			{
				//Decrypt the data using the receiver's private key.
				myReceiver.DecryptData(encrypted);
			}
			else
			{
				Console.WriteLine("Invalid signature");
			}
		}
	}

	class Sender
	{
		RSAParameters rsaPubParams;
		RSAParameters rsaPrivateParams;

		public Sender()
		{
			RSACryptoServiceProvider rsaCSP = new RSACryptoServiceProvider();

			//Generate public and private key data.
			rsaPrivateParams = rsaCSP.ExportParameters(true);
			rsaPubParams = rsaCSP.ExportParameters(false);
		}

		public RSAParameters PublicParameters
		{
			get {
				return rsaPubParams;
			}
		}

		//Manually performs hash and then signs hashed value.
		public byte[] HashAndSign(byte[] encrypted)
		{
			RSACryptoServiceProvider rsaCSP = new RSACryptoServiceProvider();
			SHA256 hash = SHA256.Create();
			byte[] hashedData;

			rsaCSP.ImportParameters(rsaPrivateParams);

			hashedData = hash.ComputeHash(encrypted);
			return rsaCSP.SignHash(hashedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		}

		//Encrypts using only the public key data.
		public byte[] EncryptData(RSAParameters rsaParams, byte[] toEncrypt)
		{
			RSACryptoServiceProvider rsaCSP = new RSACryptoServiceProvider();

			rsaCSP.ImportParameters(rsaParams);
			return rsaCSP.Encrypt(toEncrypt, false);
		}
	}

	class Receiver
	{
		RSAParameters rsaPubParams;
		RSAParameters rsaPrivateParams;

		public Receiver()
		{
			RSACryptoServiceProvider rsaCSP = new RSACryptoServiceProvider();

			//Generate public and private key data.
			rsaPrivateParams = rsaCSP.ExportParameters(true);
			rsaPubParams = rsaCSP.ExportParameters(false);
		}

		public RSAParameters PublicParameters
		{
			get {
				return rsaPubParams;
			}
		}

		//Manually performs hash and then verifies hashed value.
		public bool VerifyHash(RSAParameters rsaParams, byte[] signedData, byte[] signature)
		{
			RSACryptoServiceProvider rsaCSP = new RSACryptoServiceProvider();
			SHA256 hash = SHA256.Create();
			byte[] hashedData;

			rsaCSP.ImportParameters(rsaParams);
			bool dataOK = rsaCSP.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			hashedData = hash.ComputeHash(signedData);
			return rsaCSP.VerifyHash(hashedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		}

		//Decrypt using the private key data.
		public void DecryptData(byte[] encrypted)
		{
			byte[] fromEncrypt;
			string roundTrip;
			ASCIIEncoding myAscii = new ASCIIEncoding();
			RSACryptoServiceProvider rsaCSP = new RSACryptoServiceProvider();

			rsaCSP.ImportParameters(rsaPrivateParams);
			fromEncrypt = rsaCSP.Decrypt(encrypted, false);
			roundTrip = myAscii.GetString(fromEncrypt);

			Console.WriteLine("RoundTrip: {0}", roundTrip);
		}
	}

	[Route("api/[controller]")]
	public class StorageController : Controller
	{
		// GET api/storage
		[HttpGet]
		public string Help()
		{
			System.IO.File.WriteAllText("ala", "cos");

			try
			{
				RSAParameters S_RSAParams_Public;
				RSAParameters S_RSAParams;
				RSAParameters C_RSAParams_Public;
				RSAParameters C_RSAParams;

				//Create a new RSACryptoServiceProvider object.
				//Export the key information to an RSAParameters object.
				//Pass false to export the public key information or pass
				//true to export public and private key information.

				// Server
				using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
				{
					S_RSAParams = RSA.ExportParameters(true);
					S_RSAParams_Public = RSA.ExportParameters(false);
				}
				
				// Client
				using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
				{
					C_RSAParams = RSA.ExportParameters(true);
					C_RSAParams_Public = RSA.ExportParameters(false);
				}

				//

				using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
				{
					// Client

					string data = "ala ma kota";
					byte[] bdata = Encoding.UTF8.GetBytes(data);

					RSA.ImportParameters(S_RSAParams_Public);
					byte[] bedata = RSA.Encrypt(bdata, false);

					RSA.ImportParameters(C_RSAParams);
					byte[] bedata_sign = RSA.SignData(bedata, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

					//

					//bedata[6] = 3;

					// Server

					RSA.ImportParameters(C_RSAParams_Public);
					bool ok = RSA.VerifyData(bedata, bedata_sign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

					RSA.ImportParameters(S_RSAParams);
					byte[] bddata = RSA.Decrypt(bedata, false);

					string ddata = Encoding.UTF8.GetString(bddata);
				}
			}
			catch (CryptographicException e)
			{
				//Catch this exception in case the encryption did
				//not succeed.
				Console.WriteLine(e.Message);
			}

			return "pbXStorage";
		}

		// GET api/storage/token
		[HttpGet("token/{id}")]
		public async Task<string> GetToken(string clientId)
		{
			string token = Guid.NewGuid().ToString();
			return $"{token}";
		}

		// GET api/storage/{id}
		[HttpGet("{id},{token}")]
		public string Get(string id, string token)
		{
			return $"{id} for {token}";
		}

		// POST api/storage
		[HttpPost]
		public void Post([FromBody]string value)
		{
		}

		// PUT api/storage/5
		[HttpPut("{id}")]
		public void Put(int id, [FromBody]string value)
		{
		}

		// DELETE api/storage/5
		[HttpDelete("{id}")]
		public void Delete(int id)
		{
		}
	}
}
