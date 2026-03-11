# Benchmark Results

## Environment

- **OS:** macOS 26.1 (Darwin 25.1.0)
- **CPU:** Apple M4 Pro, 14 logical / 14 physical cores
- **Runtime:** .NET 8.0.24, Arm64 RyuJIT AdvSIMD
- **BenchmarkDotNet:** v0.14.0, ShortRun job
- **RabbitMQ:** Testcontainers (`rabbitmq:4-management`)

## 1. Isolated Hash Generation (CPU-only, no I/O)

`DefaultHashGenerator` vs `ParallelHashGenerator` — raw throughput without RabbitMQ.

| Method | Count | Mean | Ratio | Allocated |
|---|---|---|---|---|
| Default_StreamSha1s | 100 | 279.3 μs | 1.00 | 36.64 KB |
| Parallel_StreamSha1s | 100 | 532.2 μs | 1.91 | 42.13 KB |
| Default_StreamSha1s | 1,000 | 1,715 μs | 1.00 | 347.61 KB |
| Parallel_StreamSha1s | 1,000 | 2,720 μs | 1.59 | 394.45 KB |
| Default_StreamSha1s | 10,000 | 17,067 μs | 1.00 | 3,442 KB |
| Parallel_StreamSha1s | 10,000 | 21,188 μs | 1.24 | 3,879 KB |
| Default_StreamSha1s | 40,000 | 63,972 μs | 1.00 | 13,754 KB |
| Parallel_StreamSha1s | 40,000 | 85,363 μs | 1.33 | 15,962 KB |
| Default_StreamSha1s | 100,000 | 163,187 μs | 1.00 | 34,379 KB |
| Parallel_StreamSha1s | 100,000 | 200,961 μs | 1.23 | 40,300 KB |

`DefaultHashGenerator` is consistently faster. Parallel overhead (23–91%) from thread coordination outweighs gains for lightweight SHA1 computation.

## 2. Full Pipeline (Generate → Batch → Publish to RabbitMQ)

End-to-end with `RabbitMqBatchedOffloadToWorkerProcessor` (batchSize=100, DOP=ProcessorCount).

| Method | Count | Mean | Ratio | Allocated |
|---|---|---|---|---|
| Default_GenerateAndPublish | 1,000 | 30.4 ms | 1.00 | 1.52 MB |
| Parallel_GenerateAndPublish | 1,000 | 31.2 ms | 1.03 | 1.78 MB |
| Default_GenerateAndPublish | 10,000 | 50.5 ms | 1.00 | 7.06 MB |
| Parallel_GenerateAndPublish | 10,000 | 72.7 ms | 1.44 | 7.65 MB |
| Default_GenerateAndPublish | 40,000 | 146.1 ms | 1.00 | 24.52 MB |
| Parallel_GenerateAndPublish | 40,000 | 169.0 ms | 1.16 | 27.62 MB |
| Default_GenerateAndPublish | 100,000 | 263.3 ms | 1.00 | 60.2 MB |
| Parallel_GenerateAndPublish | 100,000 | 345.4 ms | 1.32 | 63.83 MB |

`DefaultHashGenerator` remains faster even with real network I/O. Parallel overhead (16–44%) is not recovered by overlapping with publishing.

## 3. Degree of Parallelism Tuning (40K hashes, real RabbitMQ)

`ParallelHashGenerator` with varying DOP values (batchSize=100).

| DOP | Mean | Allocated |
|---|---|---|
| 0 (=ProcessorCount, 14) | 152.5 ms | 26.41 MB |
| 1 | 510.9 ms | 23.77 MB |
| 2 | 322.7 ms | 24.37 MB |
| 4 | 259.6 ms | 25.11 MB |
| 8 | 209.9 ms | 25.92 MB |

Higher DOP improves `ParallelHashGenerator` throughput (DOP=0 is 3.4× faster than DOP=1), but `DefaultHashGenerator` at 40K (146 ms) still outperforms DOP=0 (152.5 ms).

## 4. Publish Batch Size (1M hashes, real RabbitMQ)

`DefaultHashGenerator` with `RabbitMqBatchedOffloadToWorkerProcessor` at varying batch sizes (DOP=ProcessorCount).

| BatchSize | Mean | Allocated |
|---|---|---|
| 10 | 21.486 s | 889.71 MB |
| 50 | 4.815 s | 623.45 MB |
| 100 | 2.642 s | 585.11 MB |
| 250 | 1.908 s | 619.16 MB |
| 500 | 1.559 s | 698.86 MB |
| 1,000 | 2.302 s | 864.51 MB |
| 2,000 | 2.250 s | 871.12 MB |
| 5,000 | 1.864 s | 896.65 MB |
| 10,000 | 2.059 s | 938.77 MB |
| 20,000 | 2.042 s | 974.77 MB |
| 40,000 | 2.294 s | 1,051.19 MB |

Performance improves sharply from 10→500, with batch size 500 achieving the lowest latency (1.559s). Beyond 500, latency plateaus around 1.9–2.3s. Memory allocation grows steadily with batch size — from 585 MB at 100 to over 1 GB at 40K — due to larger per-publish payloads increasing GC pressure (Gen2 collections rise significantly at 1K+). Batch size 100 offers the best memory efficiency, while 500 is the throughput optimum.
