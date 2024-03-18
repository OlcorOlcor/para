using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dns_netcore {
    class RecursiveResolver : IRecursiveResolver {
        private IDNSClient dnsClient;
        private ConcurrentDictionary<string, IP4Addr> finished_cache_ = new();
        private ConcurrentDictionary<string, IP4Addr> working_cache = new();

        public RecursiveResolver(IDNSClient client) {
            this.dnsClient = client;
        }

        private bool CheckCache(string domain, out IP4Addr address, out string foundDomain) {
            string remainingDomain = domain;
            while (true) {
                if (finished_cache_.TryGetValue(remainingDomain, out IP4Addr cachedAddr)) {
                    if (dnsClient.Reverse(cachedAddr).Result == remainingDomain) {
                        foundDomain = remainingDomain;
                        address = cachedAddr;
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

        private Task<IP4Addr> HandleRootServer(string domain, IP4Addr res, CancellationToken token) {
            return Task<IP4Addr>.Run(async () => {
                List<string> domainToCache = new();
                bool cacheHit = false;
                IP4Addr address = new();
                string foundDomain = "";
                try {
                    cacheHit = CheckCache(domain, out address, out foundDomain);
                } catch {
                    cacheHit = false;
                }
                if (cacheHit) {
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
                    if (token.IsCancellationRequested) { 
                        break;
                    }
                    var result = await dnsClient.Resolve(res, sub);
                    domainToCache.Insert(0, sub);
                    this.finished_cache_.TryAdd(string.Join(".", domainToCache), result);
                    res = result;
                }
                return res;
            });
        }

        public Task<IP4Addr> ResolveRecursive(string domain) {
            return Task<IP4Addr>.Run(async () => {
                IReadOnlyList<IP4Addr> roots = dnsClient.GetRootServers();
                CancellationTokenSource source = new CancellationTokenSource();
                CancellationToken token = source.Token;
                var tasks = new List<Task<IP4Addr>>();
                foreach(var root in roots) {
                    tasks.Add(HandleRootServer(domain, root, token));
                }
                Task<IP4Addr> result = await Task.WhenAny(tasks);
                source.Cancel();

                return await result;
            });
        }
    }
}
