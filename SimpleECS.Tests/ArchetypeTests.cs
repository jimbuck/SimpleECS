namespace SimpleECS.Tests;

public class ArchetypeTests
{
    [Fact]
    public void FromEntity_Valid()
    {
        var world = new World();
        var entity = world.CreateEntity(13);
        Assert.True(entity);
        var archetype = entity.Archetype;
        Assert.True(archetype.IsValid());
    }

    [Fact]
    public void FromEntity_NotValid()
    {
        var world = new World();
        var entity = world.CreateEntity(13);
        Assert.True(entity);
        entity.Destroy();
        Assert.False(entity);

        var archetype = entity.Archetype;
        Assert.False(archetype.IsValid());
    }

    [Fact]
    public void CreateEntity_Valid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        var archetype = entity.Archetype;

        var newEntity = archetype.CreateEntity();
        Assert.True(newEntity.IsValid());
        Assert.True(newEntity.Has<int>());
        Assert.Equal(0, newEntity.Get<int>());
    }

    [Fact]
    public void CreateEntity_NotValid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        Assert.True(entity);
        entity.Destroy();
        Assert.False(entity);

        var archetype = entity.Archetype;

        var newEntity = archetype.CreateEntity();
        Assert.False(newEntity.IsValid());
    }

    [Fact]
    public void TryGetEntityBuffer_Valid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        var archetype = entity.Archetype;

        var newEntity = archetype.CreateEntity();

        var didGetEntityBuffer = archetype.TryGetEntityBuffer(out var entity_buffer);
        Assert.True(didGetEntityBuffer);
        Assert.False(entity_buffer.IsEmpty);
        Assert.Equal(entity, entity_buffer[0]);
        Assert.Equal(newEntity, entity_buffer[1]);
    }

    [Fact]
    public void TryGetEntityBuffer_NotValid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        entity.Destroy();
        Assert.False(entity);
        var archetype = entity.Archetype;

        var newEntity = archetype.CreateEntity();
        Assert.False(newEntity);

        var didGetEntityBuffer = archetype.TryGetEntityBuffer(out var entity_buffer);
        Assert.False(didGetEntityBuffer);
        Assert.True(entity_buffer.IsEmpty);
    }

    [Fact]
    public void TryGetComponentBuffer_Valid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        var archetype = entity.Archetype;

        var newEntity = archetype.CreateEntity();

        if (archetype.TryGetComponentBuffer<int>(out var int_buffer))
        {
            for (int i = 0; i < archetype.EntityCount; ++i)
                int_buffer[i]++;
        }

        Assert.Equal(14, entity.Get<int>());                 
        Assert.Equal(1, newEntity.Get<int>());
    }

    [Fact]
    public void TryGetComponentBuffer_NotValid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        entity.Destroy();
        Assert.False(entity);
        var archetype = entity.Archetype;

        var newEntity = archetype.CreateEntity();
        Assert.False(newEntity);

        var didGetComponentBuffer = archetype.TryGetComponentBuffer<Entity>(out var componentBuffer);
        Assert.False(didGetComponentBuffer);
        Assert.True(componentBuffer.IsEmpty);
    }

    [Fact]
    public void Destroy_Valid()
    {
        using var world = new World();
        var entity = world.CreateEntity(13);
        var archetype = entity.Archetype;
        var newEntity = archetype.CreateEntity();

        Assert.True(entity);
        Assert.True(newEntity);

        archetype.Destroy();

        Assert.False(archetype);
        Assert.False(entity);
        Assert.False(newEntity);
    }
}
