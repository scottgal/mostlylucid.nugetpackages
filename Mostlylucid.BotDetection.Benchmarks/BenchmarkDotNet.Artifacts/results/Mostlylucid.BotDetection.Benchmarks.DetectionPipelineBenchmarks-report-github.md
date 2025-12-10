```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7171/24H2/2024Update/HudsonValley)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.100-rc.1.25451.107
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4
  Job-ORZUYQ : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4

IterationCount=10  RunStrategy=Throughput  WarmupCount=3  

```
| Method                                    | Mean     | Error    | StdDev   | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Allocated |
|------------------------------------------ |---------:|---------:|---------:|---------------------:|-----------------:|-------:|-------:|----------:|
| &#39;Human request (typical website visitor)&#39; | 26.73 μs | 0.881 μs | 0.461 μs |               0.9540 |           0.0001 | 0.6104 | 0.1221 |  43.48 KB |
| &#39;Obvious bot (curl user-agent)&#39;           | 29.21 μs | 7.863 μs | 4.112 μs |               0.0439 |                - | 0.7324 | 0.2441 |  43.01 KB |
| &#39;Search engine bot (Googlebot)&#39;           | 24.09 μs | 2.444 μs | 1.617 μs |               0.0698 |                - | 0.4883 | 0.1221 |  43.29 KB |
| &#39;Datacenter IP with browser UA&#39;           | 25.03 μs | 4.746 μs | 2.824 μs |               0.0948 |                - | 0.6104 | 0.1221 |  42.63 KB |
