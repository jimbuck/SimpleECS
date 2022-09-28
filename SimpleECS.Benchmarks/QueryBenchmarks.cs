using System.Numerics;

namespace SimpleECS.Benchmarks;

readonly struct Transform
{
    public readonly Vector3 Translation { get; init; } = Vector3.Zero;
    public readonly Quaternion Rotation { get; init; } = Quaternion.Identity;
    public readonly Vector3 Scale { get; init; } = Vector3.One;

    public Transform(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Translation = translation;
        Rotation = rotation;
        Scale = scale;
    }

    public Transform() { }
}

[MemoryDiagnoser]
[JsonExporterAttribute.Full]
[JsonExporterAttribute.FullCompressed]
public class QueryBenchmarks
{
    [Params(100, 500, 1_000, 20_000, 100_000)]
    public int EntityCount;

    private World world;
    private Query intQuery;
    private Query floatQuery;
    private Query stringQuery;
    private Query transformQuery;
    private Query intFloatQuery;
    private Query intTransformQuery;
    private Query transformNoStringQuery;

    [IterationSetup]
    public void Setup()
    {
        world = new World($"World_{EntityCount}");

        for(int i = 0; i < EntityCount; i++)
        {
            world.CreateEntity(i, i / 2f);
            world.CreateEntity($"E_1_{i}");
            world.CreateEntity(i, new Transform(new Vector3(i), Quaternion.Identity, Vector3.One));
            world.CreateEntity($"E_1_{i}", i ^ 2, new Transform(new Vector3(i), Quaternion.Identity, Vector3.One * 3));
        }

        intQuery = world.CreateQuery().Has<int>();
        floatQuery = world.CreateQuery().Has<float>();
        stringQuery = world.CreateQuery().Has<string>();
        transformQuery = world.CreateQuery().Has<Transform>();
        intFloatQuery = world.CreateQuery().Has<int>().Has<float>();
        intTransformQuery = world.CreateQuery().Has<int>().Has<Transform>();
        transformNoStringQuery = world.CreateQuery().Has<Transform>().Not<string>();
    }

    [IterationCleanup]
    public void Cleanup()
    {
        world.Dispose();
    }

    [Benchmark]
    public void Query_Foreach_SingleComponent_Readonly()
    {
        intQuery.Foreach((ref int val) => { });
        floatQuery.Foreach((ref float val) => { });
        stringQuery.Foreach((ref string val) => { });
        transformQuery.Foreach((ref Transform val) => { });
        intFloatQuery.Foreach((ref int val) => { });
        intFloatQuery.Foreach((ref float val) => { });
        intTransformQuery.Foreach((ref int val) => { });
        intTransformQuery.Foreach((ref Transform val) => { });
        transformNoStringQuery.Foreach((ref Transform val) => { });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponent_Readonly()
    {
        intQuery.Foreach((ref int val) => { });
        floatQuery.Foreach((ref float val) => { });
        stringQuery.Foreach((ref string val) => { });
        transformQuery.Foreach((ref Transform val) => { });

        intFloatQuery.Foreach((ref int val1, ref float val2) => { });
        intTransformQuery.Foreach((ref int val1, ref Transform val2) => { });

        transformNoStringQuery.Foreach((ref Transform val) => { });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponentWithEntity_Readonly()
    {
        intQuery.Foreach((in Entity entity, ref int val) => { });
        floatQuery.Foreach((in Entity entity, ref float val) => { });
        stringQuery.Foreach((in Entity entity, ref string val) => { });
        transformQuery.Foreach((in Entity entity, ref Transform val) => { });

        intFloatQuery.Foreach((in Entity entity, ref int val1, ref float val2) => { });
        intTransformQuery.Foreach((in Entity entity, ref int val1, ref Transform val2) => { });

        transformNoStringQuery.Foreach((in Entity entity, ref Transform val) => { });
    }

    [Benchmark]
    public void Query_Foreach_SingleComponent_Change()
    {
        intQuery.Foreach((ref int val) => {
            val++;
        });
        floatQuery.Foreach((ref float val) => {
            val *= 1.1f;
        });
        stringQuery.Foreach((ref string val) => {
            val += ".";
        });
        transformQuery.Foreach((ref Transform val) => {
            val = val with { Scale = val.Scale * 1.1f };
        });
        intFloatQuery.Foreach((ref int val) => {
            val--;
        });
        intFloatQuery.Foreach((ref float val) => {
            val /= 1.1f;
        });
        intTransformQuery.Foreach((ref int val) => {
            val += 2;
        });
        intTransformQuery.Foreach((ref Transform val) => {
            val = val with { Scale = val.Scale * 1.1f };
        });
        transformNoStringQuery.Foreach((ref Transform val) => {
            val = val with { Translation = val.Translation with { Y = val.Translation.Y + 1 } };
        });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponent_Change()
    {
        intQuery.Foreach((ref int val) => {
            val++;
        });
        floatQuery.Foreach((ref float val) => {
            val *= 1.1f;
        });
        stringQuery.Foreach((ref string val) => {
            val += ".";
        });
        transformQuery.Foreach((ref Transform val) => {
            val = val with { Scale = val.Scale * 1.1f };
        });

        intFloatQuery.Foreach((ref int val1, ref float val2) => {
            val1--;
            val2 /= 1.1f;
        });
        intTransformQuery.Foreach((ref int val1, ref Transform val2) => {
            val1 += 2;
            val2 = val2 with { Scale = val2.Scale * 1.1f };
        });

        transformNoStringQuery.Foreach((ref Transform val) => {
            val = val with { Translation = val.Translation with { Y = val.Translation.Y + 1 } };
        });
    }

    [Benchmark]
    public void Query_Foreach_DoubleComponentWithEntity_Change()
    {
        intQuery.Foreach((in Entity entity, ref int val) => {
            entity.Set(val + 1);
        });
        floatQuery.Foreach((in Entity entity, ref float val) => {
            entity.Set(val * 1.1f);
        });
        stringQuery.Foreach((in Entity entity, ref string val) => {
            entity.Set(val + ".");
        });
        transformQuery.Foreach((in Entity entity, ref Transform val) => {
            entity.Set(val with { Scale = val.Scale * 1.1f });
        });

        intFloatQuery.Foreach((in Entity entity, ref int val1, ref float val2) => {
            entity.Set(val1 - 1);
            entity.Set(val2 / 1.1f);
        });
        intTransformQuery.Foreach((in Entity entity, ref int val1, ref Transform val2) => {
            entity.Set(val1 + 2);
            entity.Set(val2 with { Scale = val2.Scale * 1.1f });
        });

        transformNoStringQuery.Foreach((in Entity entity, ref Transform val) => {
            entity.Set(val with { Translation = val.Translation with { Y = val.Translation.Y + 1 } });
        });
    }
}
