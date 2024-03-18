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
        private ConcurrentDictionary<string, TaskCompletionSource<IP4Addr>> working_cache = new();

        public RecursiveResolver(IDNSClient client) {
            this.dnsClient = client;
        }

        private async Task<(bool, IP4Addr?, string)> CheckCache(string domain) {
            string remainingDomain = domain;
            while (true) {
                if (finished_cache_.TryGetValue(remainingDomain, out IP4Addr cachedAddr)) {
                    var result = await dnsClient.Reverse(cachedAddr);
                    if (result == remainingDomain) {
                        return (true, cachedAddr, remainingDomain);
                    }
                }
                if (!remainingDomain.Contains('.')) {
                    break;
                }
                remainingDomain = remainingDomain.Substring(remainingDomain.IndexOf(".") + 1);
            }
            return (false, null, null);
        }

        private async Task<IP4Addr> HandleRootServer(string domain, IP4Addr res, CancellationToken token) {
            List<string> domainToCache = new();
            bool cacheHit = false;
            IP4Addr? address = new();
            string foundDomain = "";
            try {
                (cacheHit, address, foundDomain) = await CheckCache(domain);
            } catch {
                cacheHit = false;
            }
            if (cacheHit) {
                res = (IP4Addr)address;
                if (domain == foundDomain) {
                    return res;
                }
                domain = domain.Substring(0, domain.Length - foundDomain.Length - 1);
                domainToCache.Add(foundDomain);
            }

            if (working_cache.TryGetValue(domain, out TaskCompletionSource<IP4Addr> tsc)) {
                await tsc.Task;
                return tsc.Task.Result;
            }
            
            TaskCompletionSource<IP4Addr> taskCompletionSource = new();
            working_cache.TryAdd(domain, taskCompletionSource);

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
            taskCompletionSource.SetResult(res);
            working_cache.TryRemove(domain, out _);
            return res;
        }

        public async Task<IP4Addr> ResolveRecursive(string domain) {
            // IReadOnlyList<IP4Addr> roots = dnsClient.GetRootServers();
            IP4Addr root = dnsClient.GetRootServers()[0];
             CancellationTokenSource source = new CancellationTokenSource();
             CancellationToken token = source.Token;
            // var tasks = new List<Task<IP4Addr>>();
            // foreach(var root in roots) {
            //     tasks.Add(HandleRootServer(domain, root, token));
            // }
            // Task<IP4Addr> result = await Task.WhenAny(tasks);
            // source.Cancel();

            return await HandleRootServer(domain, root, token);
        }
    }
}
