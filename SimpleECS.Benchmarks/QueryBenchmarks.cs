namespace SimpleECS.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[JsonExporterAttribute.FullCompressed]
public class QueryBenchmarks
{
    [Params(100, 500, 1_000, 20_000, 100_000)]
    public int EntityCount;

    private World world;

    [IterationSetup]

    public void Setup()
    {
        world = new World($"World_{EntityCount}");

        for(int i = 0; i < EntityCount; i++)
        {
            world.CreateEntity(i, i / 2f);
        }
    }

    [IterationCleanup]
    public void Cleanup()
    {
        world.Dispose();
    }

    [Benchmark]
    public void Query_Foreach_SingleComponent_Readonly()
    {
        var query = world.CreateQuery().Has<int>();

        query.Foreach((ref int c1) => { });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponent_Readonly()
    {
        var query = world.CreateQuery().Has<int>().Has<float>();

        query.Foreach((ref int c1, ref float c2) => { });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponentWithEntity_Readonly()
    {
        var query = world.CreateQuery().Has<int>().Has<float>();

        query.Foreach((in Entity entity, ref int c1, ref float c2) => { });
    }

    [Benchmark]
    public void Query_Foreach_SingleComponent_Change()
    {
        var query = world.CreateQuery().Has<int>();

        query.Foreach((ref int c1) =>
        {
            c1++;
        });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponent_Change()
    {
        var query = world.CreateQuery().Has<int>().Has<float>();

        query.Foreach((ref int c1, ref float c2) =>
        {
            c1++;
            c2 /= 2;
        });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponentWithEntity_Change()
    {
        var query = world.CreateQuery().Has<int>().Has<float>();

        query.Foreach((in Entity entity, ref int c1, ref float c2) =>
        {
            entity.Set(c1 + 1);
            entity.Set(c2 / 2);
        });
    }
}
