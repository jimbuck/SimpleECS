namespace SimpleECS.Tests;

public class QueryTests
{
    [Fact]
    public void Foreach_ComponentOnly()
    {
        using var world = new World(nameof(Foreach_ComponentOnly));

        var matchingEntity = world.CreateEntity(1, 0.5f, (short)7);
        var nonMatchingEntity = world.CreateEntity(0, "not");

        var query = world.CreateQuery()
                 .Has<int>()
                 .Has(typeof(float), typeof(short))
                 .Not<string>()
                 .Not(typeof(ushort));
                                                    
        query.Foreach((ref int int_value, ref float float_value) =>
        {
            Assert.Equal(matchingEntity.Get<int>(), int_value);
            Assert.Equal(matchingEntity.Get<float>(), float_value);
        });
    }

    [Fact]
    public void Foreach_EntityAndComponent()
    {
        using var world = new World(nameof(Foreach_EntityAndComponent));

        var entity = world.CreateEntity("my entity", 3);
        entity.Remove<string>();
        Assert.False(entity.Has<string>());

        var query = world.CreateQuery().Has<int>();

        query.Foreach((Entity entity, ref int int_val) =>
        {
            int_val = 4;
            Assert.Equal(4, int_val);

            entity.Remove<int>();

            Assert.True(entity.Has<int>());

            entity.Set("my entity");

            Assert.False(entity.Has<string>());
        });

        Assert.True(entity.Has<string>());   // this will now return true
        Assert.False(entity.Has<int>());      // and this will now return false
    }

    [Fact]
    public void ManualIteration()
    {
        using var world = new World(nameof(ManualIteration));

        var entity = world.CreateEntity("my entity", 3);

        var query = world.CreateQuery().Has<int>();

        foreach (var archetype in query)
        {
            Assert.Equal(entity.Archetype, archetype);
            Assert.Equal(1, archetype.EntityCount);

            var didGetEntityBuffer = archetype.TryGetEntityBuffer(out var entity_buffer);
            var didGetComponentBuffer = archetype.TryGetComponentBuffer<int>(out var int_buffer);

            Assert.True(didGetEntityBuffer);
            Assert.True(didGetComponentBuffer);

            Assert.Equal(entity, entity_buffer[0]);
            Assert.Equal(3, int_buffer[0]);

            Assert.True(entity_buffer[0].IsValid());
        }
    }
}
