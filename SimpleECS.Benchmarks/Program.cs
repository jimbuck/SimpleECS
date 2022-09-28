global using System;

global using BenchmarkDotNet.Attributes;
global using BenchmarkDotNet.Running;


BenchmarkRunner.Run(new[] {
    typeof(SimpleECS.Benchmarks.WorldBenchmarks),
    typeof(SimpleECS.Benchmarks.QueryBenchmarks) 
});