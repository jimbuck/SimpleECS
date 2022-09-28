namespace SimpleECS.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[JsonExporterAttribute.FullCompressed]
public class WorldBenchmarks
{
    [Benchmark]
    public void WorldCreation_DefaultName()
    {
        using var world = new World();
    }

    [Benchmark]
    public void WorldCreation_StaticName()
    {
        using var world = new World("Static Name");
    }
}
