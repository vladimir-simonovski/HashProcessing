using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;

namespace HashProcessing.Benchmarks;

[MemoryDiagnoser]
public class HashGeneratorIsolatedBenchmark
{
    [Params(100, 1_000, 10_000, 40_000, 100_000)]
    public uint Count { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<uint> Default_StreamSha1s()
    {
        var generator = new DefaultHashGenerator(128);
        var reader = generator.StreamSha1s(Count);
        return await DrainAsync(reader);
    }

    [Benchmark]
    public async Task<uint> Parallel_StreamSha1s()
    {
        var generator = new ParallelHashGenerator(0, null);
        var reader = generator.StreamSha1s(Count);
        return await DrainAsync(reader);
    }

    private static async Task<uint> DrainAsync(ChannelReader<Sha1Hash> reader)
    {
        var count = 0u;
        await foreach (var _ in reader.ReadAllAsync())
            count++;
        return count;
    }
}
