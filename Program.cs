using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace dns_netcore
{
	/// <summary>
	/// Representing result of one tested recursive query (and the time it took to process it).
	/// </summary>
	struct TestResult
	{
		public readonly string domain;
		public readonly IP4Addr address;
		public readonly long elapsedMilliseconds;

		public TestResult(string domain, IP4Addr address, long elapsedMilliseconds)
		{
			this.domain = domain;
			this.address = address;
			this.elapsedMilliseconds = elapsedMilliseconds;
		}
	}

	class Program
	{
		/// <summary>
		/// Run some simple recursive queries (as many as we have logical cores) to warm up the thread pool.
		/// This should ensure the pool have sufficient treads warm and ready afterwards.
		/// </summary>
		static void WarmUpThreadPool()
		{
			var tasks = new Task[Environment.ProcessorCount];
			for (int i = 0; i < tasks.Length; ++i) {
				tasks[i] = Task.Delay(50);
			}
			Task.WaitAll(tasks);
		}

		/// <summary>
		/// Start measuring task that executes query and measures its latency.
		/// </summary>
		/// <param name="resolver">Resolver implementation being tested</param>
		/// <param name="domain">Domain to be resolved</param>
		/// <returns>Task which yields TestResult representing this test</returns>
		static Task<TestResult> MeasureQuery(IRecursiveResolver resolver, string domain)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			var t = resolver.ResolveRecursive(domain);
			return t.ContinueWith<TestResult>(t => {
				stopwatch.Stop();
				return new TestResult(domain, t.Result, stopwatch.ElapsedMilliseconds);
			});
		}

		/// <summary>
		/// Run a batch of queries simultaneously and wait for them all to finish.
		/// </summary>
		/// <param name="resolver">Resolver implementation being tested</param>
		/// <param name="domains">Array of domains to be resolved</param>
		/// <returns>Sum of measured times (in milliseconds)</returns>
		static long RunTestBatch(IRecursiveResolver resolver, string[] domains)
		{
			Console.Write("Starting ... ");
			var tests = domains.Select(domain => MeasureQuery(resolver, domain)).ToArray();
			Console.WriteLine("{0} tests", tests.Length);
			Task.WaitAll(tests);

			long sum = 0;
			foreach (var test in tests) {
				Console.WriteLine("Domain {0} has IP {1} (elapsed time {2} ms) ",
					test.Result.domain, test.Result.address, test.Result.elapsedMilliseconds);
				sum += test.Result.elapsedMilliseconds;
			}
			if (tests.Length > 0) {
				Console.WriteLine("Avg delay {0} ms", sum / tests.Length);
			}
			return sum;
		}

		static void Main(string[] args)
		{
			var client = new DNSClient();
			client.InitData(new string[] {
				"www.ksi.ms.mff.cuni.cz",
				"parlab.ms.mff.cuni.cz",
				"www.seznam.cz",
				"www.google.com",
			});
			var resolver = new RecursiveResolver(client);

			Console.WriteLine("Total {0} logical processors detected.", Environment.ProcessorCount);
			Console.WriteLine("Warming up thread pool...");
			WarmUpThreadPool();

			Console.WriteLine("{0}", ThreadPool.ThreadCount);

			RunTestBatch(resolver, new string[] {
				"www.ksi.ms.mff.cuni.cz",
			});

            RunTestBatch(resolver, new string[] {
                "www.ksi.ms.mff.cuni.cz",
            });

            RunTestBatch(resolver, new string[] {
				"parlab.ms.mff.cuni.cz",
				"www.seznam.cz",
				"www.google.com",
			});

			Console.WriteLine("{0}", ThreadPool.ThreadCount);
		}
	}
}
