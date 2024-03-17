using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace dns_netcore
{
	class SerialRecursiveResolver : IRecursiveResolver
	{
		private IDNSClient dnsClient;

		public SerialRecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
		}

		public Task<IP4Addr> ResolveRecursive(string domain)
		{
			return Task<IP4Addr>.Run(() => {
				string[] domains = domain.Split('.');
				Array.Reverse(domains);
				IP4Addr res = dnsClient.GetRootServers()[0];
				foreach (var sub in domains) {
					var t = dnsClient.Resolve(res, sub);
					t.Wait();
					res = t.Result;
				}
				return res;
			});
		}
	}
}
