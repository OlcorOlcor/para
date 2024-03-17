using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace dns_netcore
{
	/// <summary>
	/// Structure representing IPv4 address (as 32bit uint).
	/// </summary>
	struct IP4Addr
	{
		public uint Value;

		public IP4Addr(uint value = 0)
		{
			this.Value = value;
		}

		/// <summary>
		/// Fill in IP address from array of bytes.
		/// </summary>
		/// <param name="bytes">Address encoded as sequence of bytes</param>
		private void FromBytes(byte[] bytes)
		{
			if (bytes.Length != 4) {
				throw new ArgumentOutOfRangeException("Exactly 4 bytes are expected for IPv4");
			}

			this.Value = 0;
			foreach (byte x in bytes) {
				Value <<= 8;
				Value += x;
			}
		}

		public IP4Addr(byte[] bytes)
		{
			this.Value = 0;
			FromBytes(bytes);
		}

		/// <summary>
		/// Create IP address from string representation.
		/// </summary>
		/// <param name="address">Address in decimal text representation (e.g., "192.168.1.1")</param>
		public IP4Addr(string address)
		{
			this.Value = 0;
			var bytes = address.Split('.').Select(token => Byte.Parse(token)).ToArray();
			FromBytes(bytes);
		}

		/// <summary>
		/// Get the address as four bytes in an array.
		/// </summary>
		public byte[] ToBytes()
		{
			var res = new byte[4];
			uint value = this.Value;
			for (int i = 3; i >= 0; --i) {
				res[i] = (byte)(value % 256);
				value /= 256;
			}
			return res;
		}

		/// <summary>
		/// Return standard decimal representation of the address.
		/// </summary>
		/// <returns>E.g., "10.0.0.1"</returns>
		public override string ToString()
		{
			return string.Join('.', ToBytes().Select(b => b.ToString()));
		}
	}

	/// <summary>
	/// Exception thrown if DNS resolving fails.
	/// </summary>
	class DNSClientException : Exception
	{
		public DNSClientException()
		{
		}

		public DNSClientException(string message) : base(message)
		{
		}

		public DNSClientException(string message, Exception inner) : base(message, inner)
		{
		}
	}

	/// <summary>
	/// Low-level DNS client interface.
	/// </summary>
	interface IDNSClient
	{
		/// <summary>
		/// Get a list of root dns servers, we can ask for TLD.
		/// </summary>
		/// <returns>List of IP addresses</returns>
		IReadOnlyList<IP4Addr> GetRootServers();

		/// <summary>
		/// Ask a server to resolve its subdomain.
		/// E.g., if we know address of 'mff.cuni.cz', we can ask it for addres of 'ksi.mff.cuni.cz'.
		/// </summary>
		/// <param name="server">IP address of the server we are asking</param>
		/// <param name="subDomain">Sub domain (i.e., only 'ksi' would be here from the example above)</param>
		/// <returns>Async. task that will yield the IP address of the target subdomain</returns>
		/// <exception cref="DNSClientException">If the server or the subdomain are not found</exception>
		Task<IP4Addr> Resolve(IP4Addr server, string subDomain);

		/// <summary>
		/// Reverse resolution retrieves domain name for given IP address.
		/// </summary>
		/// <param name="server">IP address of a server</param>
		/// <returns>Domain name of that server</returns>
		/// <exception cref="DNSClientException">If the server is not found</exception>
		Task<string> Reverse(IP4Addr server);
	}


	/// <summary>
	/// Recursive resolver interface.
	/// </summary>
	interface IRecursiveResolver
	{
		/// <summary>
		/// Resolves complete domain name into an IP address.
		/// </summary>
		/// <param name="domain">Full domain name (subdomains concatenated by '.')</param>
		/// <returns>IP address of the whole domain</returns>
		Task<IP4Addr> ResolveRecursive(string domain);
	}
}
