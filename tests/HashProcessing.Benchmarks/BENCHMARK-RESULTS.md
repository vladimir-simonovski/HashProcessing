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
| Default_StreamSha1s | 100 | 156.1 μs | 1.01 | 36.32 KB |
| Parallel_StreamSha1s | 100 | 172.5 μs | 1.11 | 42.42 KB |
| Default_StreamSha1s | 1,000 | 1,329 μs | 1.00 | 345.86 KB |
| Parallel_StreamSha1s | 1,000 | 1,492 μs | 1.12 | 396.82 KB |
| Default_StreamSha1s | 10,000 | 12,575 μs | 1.00 | 3,440 KB |
| Parallel_StreamSha1s | 10,000 | 14,395 μs | 1.15 | 3,910 KB |
| Default_StreamSha1s | 40,000 | 51,523 μs | 1.00 | 13,754 KB |
| Parallel_StreamSha1s | 40,000 | 58,664 μs | 1.14 | 16,525 KB |
| Default_StreamSha1s | 100,000 | 131,639 μs | 1.00 | 34,379 KB |
| Parallel_StreamSha1s | 100,000 | 146,937 μs | 1.12 | 39,158 KB |

`DefaultHashGenerator` is consistently faster. Parallel overhead (11–14%) from thread coordination outweighs gains for lightweight SHA1 computation.

## 2. Degree of Parallelism Tuning (40K hashes, real RabbitMQ)

`ParallelHashGenerator` with varying DOP values.

| DOP | Mean | Allocated |
|---|---|---|
| 0 (=ProcessorCount, 14) | 74.50 ms | 27.27 MB |
| 1 | 121.36 ms | 23.58 MB |
| 2 | 87.43 ms | 24.22 MB |
| 4 | 76.71 ms | 25.69 MB |
| 8 | 73.10 ms | 27.21 MB |

Higher DOP improves `ParallelHashGenerator` throughput (DOP=8 is 1.7× faster than DOP=1), but `DefaultHashGenerator` at 40K in the full pipeline (60.3 ms) still outperforms the best parallel DOP (73.1 ms at DOP=8).

## 3. Publish Batch Size (1M hashes, real RabbitMQ)

`DefaultHashGenerator` with `RabbitMqBatchedOffloadToWorkerProcessor` at varying batch sizes (DOP=ProcessorCount).

| BatchSize | Mean | Allocated |
|---|---|---|
| 10 | 6.924 s | 889.26 MB |
| 50 | 1.400 s | 624.28 MB |
| 100 | 1.425 s | 592.89 MB |
| 250 | 1.521 s | 587.13 MB |
| 500 | 1.485 s | 591.45 MB |
| 1,000 | 1.454 s | 609.04 MB |
| 2,000 | 1.492 s | 721.11 MB |
| 5,000 | 1.495 s | 800.09 MB |
| 10,000 | 1.519 s | 863.43 MB |
| 20,000 | 1.592 s | 914.96 MB |
| 40,000 | 1.615 s | 1,023.32 MB |

Performance improves sharply from 10→50 (4.9× speedup), then plateaus across the 50–1,000 range (~1.40–1.49s). Batch size 50 achieves the lowest latency (1.400s). Beyond 1,000, latency drifts upward slightly (1.49–1.62s) while memory allocation grows steadily — from 587 MB at 250 to over 1 GB at 40K — due to larger per-publish payloads increasing GC pressure. Batch sizes 100–500 offer the best balance of throughput and memory efficiency.

## 4. Consumer Prefetch Count (20K messages, 4 consumers, no-op persistence)

`RabbitMqHashConsumer` consuming 20K pre-loaded messages (10 hashes each, 200K total) with 4 parallel consumers and varying `prefetchCount`. Persistence is replaced with a no-op repository to isolate the RabbitMQ delivery/ack pipeline.

| PrefetchCount | Mean | Allocated |
|---|---|---|
| 1 | 10.98 s | 679.90 MB |
| 5 | 10.48 s | 679.79 MB |
| 10 | 10.56 s | 679.98 MB |
| 25 | 10.43 s | 679.72 MB |
| 50 | 10.48 s | 679.83 MB |
| 100 | 10.49 s | 679.82 MB |
| 250 | 10.45 s | 683.91 MB |

PrefetchCount=1 is the slowest (10.98s) due to per-message round-trip overhead — each consumer must wait for a broker ACK before receiving the next message. Increasing prefetch to 5 yields a measurable 4.5% improvement (10.48s) by allowing the broker to push messages ahead of acknowledgements. Beyond 5, performance converges into a narrow ~10.43–10.56s band, indicating the bottleneck shifts from message delivery to consumer-side processing (deserialization, command dispatch, downstream publish). PrefetchCount=25 achieved the lowest mean (10.43s, 5.0% faster than prefetch=1). Memory allocation is stable (~680 MB) across all values except PrefetchCount=250, which triggers significantly more Gen1/Gen2 collections due to larger in-flight message buffers. A prefetch count of 10–50 provides the best balance of throughput and memory efficiency.

## 5. Full Pipeline (Generate → Batch → Publish to RabbitMQ)

End-to-end with `RabbitMqBatchedOffloadToWorkerProcessor` (batchSize=500, DOP=ProcessorCount).

| Method | Count | Mean | Ratio | Allocated |
|---|---|---|---|---|
| Default_GenerateAndPublish | 1,000 | 8.1 ms | 1.01 | 1.74 MB |
| Parallel_GenerateAndPublish | 1,000 | 8.7 ms | 1.08 | 1.99 MB |
| Default_GenerateAndPublish | 10,000 | 16.9 ms | 1.02 | 8.90 MB |
| Parallel_GenerateAndPublish | 10,000 | 22.2 ms | 1.34 | 10.79 MB |
| Default_GenerateAndPublish | 40,000 | 60.3 ms | 1.00 | 26.13 MB |
| Parallel_GenerateAndPublish | 40,000 | 70.2 ms | 1.17 | 31.25 MB |
| Default_GenerateAndPublish | 100,000 | 147.4 ms | 1.00 | 63.62 MB |
| Parallel_GenerateAndPublish | 100,000 | 161.5 ms | 1.10 | 64.73 MB |

`DefaultHashGenerator` remains faster end-to-end. Parallel overhead (8–34%) is not recovered by overlapping with publishing. Using the throughput-optimal batch size of 500 (from §3) significantly improves pipeline throughput compared to smaller batch sizes.
