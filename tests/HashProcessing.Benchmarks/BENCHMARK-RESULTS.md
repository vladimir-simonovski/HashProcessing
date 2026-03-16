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
| Default_StreamSha1s | 100 | 163.1 μs | 1.00 | 36.38 KB |
| Parallel_StreamSha1s | 100 | 171.9 μs | 1.06 | 42.63 KB |
| Default_StreamSha1s | 1,000 | 1,293 μs | 1.00 | 346.05 KB |
| Parallel_StreamSha1s | 1,000 | 1,490 μs | 1.15 | 396.21 KB |
| Default_StreamSha1s | 10,000 | 12,785 μs | 1.00 | 3,440 KB |
| Parallel_StreamSha1s | 10,000 | 14,345 μs | 1.12 | 3,935 KB |
| Default_StreamSha1s | 40,000 | 50,657 μs | 1.00 | 13,754 KB |
| Parallel_StreamSha1s | 40,000 | 57,456 μs | 1.13 | 15,710 KB |
| Default_StreamSha1s | 100,000 | 127,807 μs | 1.00 | 34,378 KB |
| Parallel_StreamSha1s | 100,000 | 144,632 μs | 1.13 | 39,518 KB |

`DefaultHashGenerator` is consistently faster. Parallel overhead (6–15%) from thread coordination outweighs gains for lightweight SHA1 computation.

## 2. Degree of Parallelism Tuning (40K hashes, real RabbitMQ)

`ParallelHashGenerator` with varying DOP values.

| DOP | Mean | Allocated |
|---|---|---|
| 0 (=ProcessorCount, 14) | 66.25 ms | 26.92 MB |
| 1 | 122.32 ms | 23.92 MB |
| 2 | 87.04 ms | 24.39 MB |
| 4 | 73.10 ms | 25.60 MB |
| 8 | 69.37 ms | 26.68 MB |

Higher DOP improves `ParallelHashGenerator` throughput (DOP=8 is 1.8× faster than DOP=1), but `DefaultHashGenerator` at 40K in the full pipeline (54.7 ms) still outperforms the best parallel DOP (66.3 ms at DOP=ProcessorCount).

## 3. Full Pipeline (Generate → Batch → Publish to RabbitMQ)

End-to-end with `RabbitMqBatchedOffloadToWorkerProcessor` (batchSize=500, DOP=ProcessorCount).

| Method | Count | Mean | Ratio | Allocated |
|---|---|---|---|---|
| Default_GenerateAndPublish | 1,000 | 2.3 ms | 1.01 | 1,040 KB |
| Parallel_GenerateAndPublish | 1,000 | 2.7 ms | 1.20 | 1,140 KB |
| Default_GenerateAndPublish | 10,000 | 14.2 ms | 1.00 | 6,720 KB |
| Parallel_GenerateAndPublish | 10,000 | 17.4 ms | 1.23 | 9,300 KB |
| Default_GenerateAndPublish | 40,000 | 54.7 ms | 1.00 | 25,150 KB |
| Parallel_GenerateAndPublish | 40,000 | 68.9 ms | 1.26 | 30,710 KB |
| Default_GenerateAndPublish | 100,000 | 138.4 ms | 1.00 | 60,470 KB |
| Parallel_GenerateAndPublish | 100,000 | 164.5 ms | 1.19 | 72,170 KB |

`DefaultHashGenerator` remains faster end-to-end. Parallel overhead (19–26%) is not recovered by overlapping with publishing.

## 4. End-to-End Roundtrip (API → RabbitMQ → Worker → DB → Daily Counts)

Full roundtrip: HTTP POST `/hashes?count=40000` → generate hashes → batch-publish to RabbitMQ → Worker consumes, persists to MariaDB, publishes daily counts → API consumes daily count events. Varying batch sizes with `DefaultHashGenerator` (DOP=ProcessorCount). Uses `WebApplicationFactory` for the API and a real Worker host, both backed by Testcontainers (RabbitMQ + MariaDB).

| BatchSize | Mean | Allocated |
|---|---|---|
| 50 | 23.298 s | 16,457 MB |
| 100 | 11.943 s | 8,328 MB |
| 250 | 4.911 s | 3,433 MB |
| 500 | 2.595 s | 1,817 MB |
| 1,000 | 1.477 s | 994 MB |

Latency scales nearly linearly with batch size — doubling the batch roughly halves the roundtrip time — confirming that RabbitMQ message count (not payload size) dominates end-to-end cost. At BatchSize=50, the Worker must consume 800 messages (40K/50), while at BatchSize=1000 it only processes 40 messages. Memory allocation tracks proportionally: 50→1000 reduces allocations by 16×. BatchSize=1000 achieves the lowest latency (1.48s) and memory usage (994 MB). The full roundtrip adds significant overhead from Worker-side DB persistence (batch INSERTs into MariaDB) and the daily-count event loop compared to the producer-only pipeline (§3).

## 5. High Batch Size E2E Roundtrip (200K hashes, batch sizes 500–10000)

Same full-roundtrip setup as §4, scaled to 200K hashes with higher batch sizes to find the throughput plateau. HTTP POST `/hashes?count=200000` → generate → batch-publish → Worker consume/persist → daily count events.

| BatchSize | Mean | Allocated |
|---|---|---|
| 500 | 82.366 s | 40.53 GB |
| 1,000 | 41.694 s | 20.77 GB |
| 2,000 | 22.507 s | 10.87 GB |
| 5,000 | 10.433 s | 4.95 GB |
| 10,000 | 5.809 s | 2.98 GB |

The near-linear scaling observed in §4 continues at 200K hashes with no sign of plateau or degradation up to BatchSize=10000. Each doubling of batch size still roughly halves latency: 500→1000 (1.98×), 1000→2000 (1.85×), 2000→5000 (2.16×), 5000→10000 (1.80×). Memory allocation follows the same linear relationship — BatchSize=10000 allocates 13.6× less than BatchSize=500. At 200K hashes with BatchSize=10000, the Worker processes only 20 RabbitMQ messages, each containing 10K hashes, achieving 34.4K hashes/sec throughput. No diminishing returns are visible yet; the batch-size sweet spot for this workload is at or above 10000.