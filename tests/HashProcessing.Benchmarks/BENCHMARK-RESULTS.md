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

## 3. Consumer Prefetch Count (20K messages, 4 consumers, no-op persistence)

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

## 4. Full Pipeline (Generate → Batch → Publish to RabbitMQ)

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

`DefaultHashGenerator` remains faster end-to-end. Parallel overhead (8–34%) is not recovered by overlapping with publishing. Using the throughput-optimal batch size of 500 significantly improves pipeline throughput compared to smaller batch sizes.

## 6. End-to-End Roundtrip (API → RabbitMQ → Worker → DB → Daily Counts)

Full roundtrip: HTTP POST `/hashes?count=40000` → generate hashes → batch-publish to RabbitMQ → Worker consumes, persists to MariaDB, publishes daily counts → API consumes daily count events. Varying batch sizes with `DefaultHashGenerator` (DOP=ProcessorCount). Uses `WebApplicationFactory` for the API and a real Worker host, both backed by Testcontainers (RabbitMQ + MariaDB).

| BatchSize | Mean | Allocated |
|---|---|---|
| 50 | 21.784 s | 16,478.13 MB |
| 100 | 10.840 s | 8,329.81 MB |
| 250 | 4.579 s | 3,440.16 MB |
| 500 | 2.387 s | 1,823.78 MB |
| 1,000 | 1.423 s | 998.25 MB |

Latency scales nearly linearly with batch size — doubling the batch roughly halves the roundtrip time — confirming that RabbitMQ message count (not payload size) dominates end-to-end cost. At BatchSize=50, the Worker must consume 800 messages (40K/50), while at BatchSize=1000 it only processes 40 messages. Memory allocation tracks proportionally: 50→1000 reduces allocations by 16×. BatchSize=1000 achieves the lowest latency (1.42s) and memory usage (998 MB). The full roundtrip adds significant overhead from Worker-side DB persistence (batch INSERTs into MariaDB) and the daily-count event loop compared to the producer-only pipeline (§4).

## 7. High Batch Size E2E Roundtrip (200K hashes, batch sizes 500–10000)

Same full-roundtrip setup as §6, scaled to 200K hashes with higher batch sizes to find the throughput plateau. HTTP POST `/hashes?count=200000` → generate → batch-publish → Worker consume/persist → daily count events.

| BatchSize | Mean | Allocated |
|---|---|---|
| 500 | 76.096 s | 40.59 GB |
| 1,000 | 37.848 s | 20.76 GB |
| 2,000 | 20.899 s | 10.90 GB |
| 5,000 | 9.116 s | 4.92 GB |
| 10,000 | 5.333 s | 2.96 GB |

The near-linear scaling observed in §6 continues at 200K hashes with no sign of plateau or degradation up to BatchSize=10000. Each doubling of batch size still roughly halves latency: 500→1000 (2.01×), 1000→2000 (1.81×), 2000→5000 (2.29×), 5000→10000 (1.71×). Memory allocation follows the same linear relationship — BatchSize=10000 allocates 13.7× less than BatchSize=500. At 200K hashes with BatchSize=10000, the Worker processes only 20 RabbitMQ messages, each containing 10K hashes, achieving 37.5K hashes/sec throughput. No diminishing returns are visible yet; the batch-size sweet spot for this workload is at or above 10000.
