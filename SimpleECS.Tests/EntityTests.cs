namespace SimpleECS.Tests;

public class EntityTests
{

    [Fact]
    public void Create_Valid()
    {
        using var world = new World();
        var entity = world.CreateEntity("my entity", 3, 5f);

        Assert.True(entity);
    }

    [Fact]
    public void Destroy_NotValid()
    {
        using var world = new World();
        var entity = world.CreateEntity("temp entity", 1);

        Assert.True(entity);

        entity.Destroy();
        Assert.False(entity);
    }

    [Fact]
    public void Has_Missing()
    {
        using var world = new World();
        var entity = world.CreateEntity("temp entity", 1);

        Assert.False(entity.Has<bool>());
    }

    [Fact]
    public void Has_Included()
    {
        using var world = new World();
        var entity = world.CreateEntity("temp entity", 1);

        Assert.True(entity.Has<int>());
    }

    [Fact]
    public void Get_Check()
    {
        using var world = new World();
        var entity = world.CreateEntity(3);
        Assert.Equal(3, entity.Get<int>());
    }

    [Fact]
    public void Get_Reference()
    {
        using var world = new World();
        var entity = world.CreateEntity(3);
        Assert.Equal(3, entity.Get<int>());

        ref var count = ref entity.Get<int>();
        
        count += 4;

        Assert.Equal(7, entity.Get<int>());
    }

    [Fact]
    public void Get_NoRef()
    {
        using var world = new World();
        var entity = world.CreateEntity(3);
        Assert.Equal(3, entity.Get<int>());

        // Note the missing `ref` keywords...
        var count = entity.Get<int>();

        count += 4;

        Assert.Equal(7, count);
        Assert.Equal(3, entity.Get<int>());
    }

    [Fact]
    public void Set_New()
    {
        using var world = new World();

        var testString = "test string";
        var entity = world.CreateEntity(3);
        Assert.False(entity.Has<string>());

        entity.Set(testString);
        Assert.True(entity.Has<string>());
        Assert.Equal(testString, entity.Get<string>());
    }

    [Fact]
    public void Remove()
    {
        using var world = new World();

        var entity = world.CreateEntity(3);
        Assert.True(entity.Has<int>());

        entity.Remove<int>();
        Assert.False(entity.Has<int>());
    }
}
