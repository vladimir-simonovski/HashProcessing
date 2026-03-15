# Benchmark Results

## Environment

- **OS:** macOS 26.1 (Darwin 25.1.0)
- **CPU:** Apple M4 Pro, 14 logical / 14 physical cores
- **Runtime:** .NET 8.0.24, Arm64 RyuJIT AdvSIMD
- **BenchmarkDotNet:** v0.14.0
- **RabbitMQ:** Testcontainers (`rabbitmq:4-management`)
- **MariaDB:** Testcontainers (`mariadb:11`)

## 1. Isolated Hash Generation (CPU-only, no I/O)

`DefaultHashGenerator` vs `ParallelHashGenerator` — raw throughput without RabbitMQ.

| Method | Count | Mean | Ratio | Allocated |
|---|---|---|---|---|
| Default_StreamSha1s | 100 | 153.1 μs | 1.01 | 36.41 KB |
| Parallel_StreamSha1s | 100 | 171.1 μs | 1.13 | 43.15 KB |
| Default_StreamSha1s | 1,000 | 1,359 μs | 1.00 | 345.95 KB |
| Parallel_StreamSha1s | 1,000 | 1,490 μs | 1.10 | 400.33 KB |
| Default_StreamSha1s | 10,000 | 12,508 μs | 1.00 | 3,440 KB |
| Parallel_StreamSha1s | 10,000 | 14,244 μs | 1.14 | 3,927 KB |
| Default_StreamSha1s | 40,000 | 50,174 μs | 1.00 | 13,753 KB |
| Parallel_StreamSha1s | 40,000 | 58,512 μs | 1.17 | 15,533 KB |
| Default_StreamSha1s | 100,000 | 126,838 μs | 1.00 | 34,378 KB |
| Parallel_StreamSha1s | 100,000 | 144,427 μs | 1.14 | 38,684 KB |

`DefaultHashGenerator` is consistently faster. Parallel overhead (10–17%) from thread coordination outweighs gains for lightweight SHA1 computation.

## 2. Degree of Parallelism Tuning (40K hashes, real RabbitMQ)

`ParallelHashGenerator` with varying DOP values.

| DOP | Mean | Allocated |
|---|---|---|
| 0 (=ProcessorCount, 14) | 65.67 ms | 26.46 MB |
| 1 | 115.51 ms | 23.59 MB |
| 2 | 81.56 ms | 24.06 MB |
| 4 | 73.10 ms | 25.11 MB |
| 8 | 67.32 ms | 25.86 MB |

Higher DOP improves `ParallelHashGenerator` throughput (DOP=8 is 1.7× faster than DOP=1), but `DefaultHashGenerator` at 40K in the full pipeline (52.2 ms) still outperforms the best parallel DOP (65.7 ms at DOP=ProcessorCount).

## 3. Consumer Prefetch Count (40K hashes, full E2E roundtrip)

Full roundtrip with varying `prefetchCount` (BatchSize=1000, 4 parallel consumers). HTTP POST `/hashes?count=40000` → generate → batch-publish → Worker consume/persist → daily count events.

| PrefetchCount | Mean | Allocated |
|---|---|---|
| 1 | 2.570 s | 1.77 GB |
| 10 | 2.589 s | 1.77 GB |
| 25 | 2.553 s | 1.76 GB |
| 50 | 2.593 s | 1.78 GB |
| 250 | 2.560 s | 1.76 GB |

Performance is virtually identical across all prefetch values (~2.55–2.59s), indicating the bottleneck is not message delivery latency but consumer-side processing (deserialization, DB persistence, downstream publish). Memory allocation is stable (~1.77 GB) across all values. A prefetch count of 10–50 provides a reasonable balance without risk of excessive in-flight message buffering.

## 4. Full Pipeline (Generate → Batch → Publish to RabbitMQ)

End-to-end with `RabbitMqBatchedOffloadToWorkerProcessor` (batchSize=500, DOP=ProcessorCount).

| Method | Count | Mean | Ratio | Allocated |
|---|---|---|---|---|
| Default_GenerateAndPublish | 1,000 | 2.2 ms | 1.01 | 935 KB |
| Parallel_GenerateAndPublish | 1,000 | 2.5 ms | 1.15 | 1,157 KB |
| Default_GenerateAndPublish | 10,000 | 13.7 ms | 1.00 | 7,490 KB |
| Parallel_GenerateAndPublish | 10,000 | 16.3 ms | 1.19 | 9,450 KB |
| Default_GenerateAndPublish | 40,000 | 52.2 ms | 1.00 | 25,565 KB |
| Parallel_GenerateAndPublish | 40,000 | 65.1 ms | 1.25 | 30,919 KB |
| Default_GenerateAndPublish | 100,000 | 137.4 ms | 1.00 | 61,081 KB |
| Parallel_GenerateAndPublish | 100,000 | 161.4 ms | 1.18 | 73,769 KB |

`DefaultHashGenerator` remains faster end-to-end. Parallel overhead (15–25%) is not recovered by overlapping with publishing. Using the throughput-optimal batch size of 500 significantly improves pipeline throughput compared to smaller batch sizes.

## 5. End-to-End Roundtrip (API → RabbitMQ → Worker → DB → Daily Counts)

Full roundtrip: HTTP POST `/hashes?count=40000` → generate hashes → batch-publish to RabbitMQ → Worker consumes, persists to MariaDB, publishes daily counts → API consumes daily count events. Varying batch sizes with `DefaultHashGenerator` (DOP=ProcessorCount). Uses `WebApplicationFactory` for the API and a real Worker host, both backed by Testcontainers (RabbitMQ + MariaDB).

| BatchSize | Mean | Allocated |
|---|---|---|
| 50 | 23.069 s | 16,458 MB |
| 100 | 11.849 s | 8,318 MB |
| 250 | 4.958 s | 3,432 MB |
| 500 | 2.647 s | 1,803 MB |
| 1,000 | 1.513 s | 1,000 MB |

Latency scales nearly linearly with batch size — doubling the batch roughly halves the roundtrip time — confirming that RabbitMQ message count (not payload size) dominates end-to-end cost. At BatchSize=50, the Worker must consume 800 messages (40K/50), while at BatchSize=1000 it only processes 40 messages. Memory allocation tracks proportionally: 50→1000 reduces allocations by 16×. BatchSize=1000 achieves the lowest latency (1.51s) and memory usage (1,000 MB). The full roundtrip adds significant overhead from Worker-side DB persistence (batch INSERTs into MariaDB) and the daily-count event loop compared to the producer-only pipeline (§4).

## 6. High Batch Size E2E Roundtrip (200K hashes, batch sizes 500–10000)

Same full-roundtrip setup as §5, scaled to 200K hashes with higher batch sizes to find the throughput plateau. HTTP POST `/hashes?count=200000` → generate → batch-publish → Worker consume/persist → daily count events.

| BatchSize | Mean | Allocated |
|---|---|---|
| 500 | 77.054 s | 40.57 GB |
| 1,000 | 39.150 s | 20.75 GB |
| 2,000 | 20.496 s | 10.86 GB |
| 5,000 | 8.943 s | 4.92 GB |
| 10,000 | 5.190 s | 2.97 GB |

The near-linear scaling observed in §5 continues at 200K hashes with no sign of plateau or degradation up to BatchSize=10000. Each doubling of batch size still roughly halves latency: 500→1000 (1.97×), 1000→2000 (1.91×), 2000→5000 (2.29×), 5000→10000 (1.72×). Memory allocation follows the same linear relationship — BatchSize=10000 allocates 13.7× less than BatchSize=500. At 200K hashes with BatchSize=10000, the Worker processes only 20 RabbitMQ messages, each containing 10K hashes, achieving 38.5K hashes/sec throughput. No diminishing returns are visible yet; the batch-size sweet spot for this workload is at or above 10000.