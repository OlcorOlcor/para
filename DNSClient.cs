using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;

namespace dns_netcore
{
	/// <summary>
	/// Trivial mock implementation of low-level DNS resolver.
	/// It is given a list of domains and create internal records that associate these
	/// domains (and all subdomains) with IP addresses from 10.x.x.x range.
	/// The interface method uses delay to simulate asynchronous workload.
	/// </summary>
	class DNSClient : IDNSClient
	{
		/// <summary>
		/// Internal node structure used to build domain trees.
		/// </summary>
		private class Node
		{
			public readonly Node Parent;
			public readonly IP4Addr Address;
			public readonly string Domain;
			public readonly Dictionary<string, Node> Subdomains;

			public Node(Node parent, IP4Addr address, string domain)
			{
				Parent = parent;
				Address = address;
				Domain = domain;
				Subdomains = new Dictionary<string, Node>();
			}
		}

		/// <summary>
		/// Complete index IP -> DNS node
		/// </summary>
		private Dictionary<uint, Node> knownDomains = new Dictionary<uint, Node>();

		/// <summary>
		/// List of IP addresses allocated to root servers.
		/// </summary>
		private IReadOnlyList<IP4Addr> rootServers;

		public DNSClient()
		{
			var rootAddr = new IP4Addr("10.0.0.1");
			var rootNode = new Node(null, rootAddr, "");
			knownDomains.Add(rootAddr.Value, rootNode);
			rootServers = ImmutableArray.Create<IP4Addr>(rootAddr);
		}

		/// <summary>
		/// Register a new subdomain for given server, assign it an arbitrary IP and return this IP.
		/// </summary>
		/// <param name="parentAddr">IP of the parent domain</param>
		/// <param name="subdomain">Subdomain to be registered within the parent</param>
		/// <returns>IP of newly associated domain.</returns>
		private IP4Addr RegisterNewSubdomain(IP4Addr parentAddr, string subdomain)
		{
			if (!knownDomains.ContainsKey(parentAddr.Value)) {
				throw new ArgumentException("Parent address does not belong to a known server.");
			}

			var parent = knownDomains[parentAddr.Value];
			if (parent.Subdomains.ContainsKey(subdomain)) {
				// Record already exists.
				return parent.Subdomains[subdomain].Address;
			}

			// Create a new node record...
			var newAddr = new IP4Addr(rootServers[0].Value + (uint)knownDomains.Count);
			Debug.Assert(!knownDomains.ContainsKey(newAddr.Value));
			var fullDomain = parent.Domain == "" ? subdomain : (subdomain + "." + parent.Domain);
			var node = new Node(parent, newAddr, fullDomain);

			// Insert it into data structures...
			parent.Subdomains.Add(subdomain, node);
			knownDomains.Add(newAddr.Value, node);

			return newAddr;
		}

		/// <summary>
		/// Initialize the datastructure with a list of domains.
		/// IP addresses are assigned automatically from 10.x.x.x range.
		/// </summary>
		public void InitData(string[] domains)
		{
			foreach (var domain in domains) {
				string[] subdomains = domain.Split('.');
				Array.Reverse(subdomains); // we need to start from top-level domain

				var ip = rootServers[0];
				foreach (var subdomain in subdomains) {
					ip = RegisterNewSubdomain(ip, subdomain);
				}
			}
		}

		#region IDNSClient
		
		public IReadOnlyList<IP4Addr> GetRootServers()
		{
			return rootServers;
		}

		public async Task<IP4Addr> Resolve(IP4Addr server, string subDomain)
		{
			await Task.Delay(500); // simulate some async work

			if (!knownDomains.ContainsKey(server.Value)) {
				throw new DNSClientException("Server " + server.ToString() + " not found.");
			}

			var node = knownDomains[server.Value];
			if (!node.Subdomains.ContainsKey(subDomain)) {
				throw new DNSClientException("Subdomain " + subDomain + " not found on server " + server.ToString() + ".");
			}

			return node.Subdomains[subDomain].Address;
		}

		public async Task<string> Reverse(IP4Addr server)
		{
			await Task.Delay(100); // simulate some async work

			if (!knownDomains.ContainsKey(server.Value)) {
				throw new DNSClientException("Server " + server.ToString() + " not found.");
			}

			return knownDomains[server.Value].Domain;
		}

		#endregion
	}
}
