using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace dns_netcore {
    class RecursiveResolver : IRecursiveResolver {
        private IDNSClient dnsClient;

        private ConcurrentDictionary<string, IP4Addr> cache_ = new();

        public RecursiveResolver(IDNSClient client) {
            this.dnsClient = client;
        }

        private bool CheckCache(string domain, out IP4Addr address, out string foundDomain) {
            string remainingDomain = domain;
            while (true) {
                if (cache_.TryGetValue(remainingDomain, out IP4Addr addr)) {
                    if (dnsClient.Reverse(addr).Result == remainingDomain) {
                        foundDomain = remainingDomain;
                        address = addr;
                        return true;
                    }
                }
                if (!remainingDomain.Contains('.')) {
                    break;
                }
                remainingDomain = remainingDomain.Substring(remainingDomain.IndexOf(".") + 1);
            }
            address = new();
            foundDomain = "";
            return false;
        }

        private Task<IP4Addr> HandleRootServer(string domain, IP4Addr res) {
            return Task<IP4Addr>.Run(() => {
                List<string> domainToCache = new();
                if (CheckCache(domain, out IP4Addr address, out string foundDomain)) {
                    res = address;
                    if (domain == foundDomain) {
                        return res;
                    }
                    domain = domain.Substring(0, domain.Length - foundDomain.Length - 1);
                    domainToCache.Add(foundDomain);
                }
                string[] domains = domain.Split('.');
                Array.Reverse(domains);
                foreach (var sub in domains) {
                    var t = dnsClient.Resolve(res, sub);
                    t.Wait();
                    domainToCache.Insert(0, sub);
                    this.cache_.TryAdd(string.Join(".", domainToCache), t.Result);
                    res = t.Result;
                }
                return res;
            });
        }

        public Task<IP4Addr> ResolveRecursive(string domain) {
            return Task<IP4Addr>.Run(async () => {
                IReadOnlyList<IP4Addr> roots = dnsClient.GetRootServers();
                var tasks = new List<Task<IP4Addr>>();
                foreach(var root in roots) {
                    tasks.Add(HandleRootServer(domain, root));
                }

                IP4Addr[] results = await Task.WhenAll(tasks);
                // TODO cancel the other tasks
                return results[0];
            });
        }
    }
}
