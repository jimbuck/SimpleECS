namespace SimpleECS.Tests;

public class EntityTests
{
    private readonly World world;

    public EntityTests()
    {
        world = World.Create(nameof(EntityTests));
    }

    [Fact]
    public void Create_Valid()
    {
        var entity = world.CreateEntity("my entity", 3, 5f);

        Assert.True(entity);
    }

    [Fact]
    public void Destroy_NotValid()
    {
        var entity = world.CreateEntity("temp entity", 1);

        Assert.True(entity);

        entity.Destroy();
        Assert.False(entity);
    }

    [Fact]
    public void Has_Missing()
    {
        var entity = world.CreateEntity("temp entity", 1);

        Assert.False(entity.Has<bool>());
    }

    [Fact]
    public void Has_Included()
    {
        var entity = world.CreateEntity("temp entity", 1);

        Assert.True(entity.Has<int>());
    }

    [Fact]
    public void Get_Check()
    {
        var entity = world.CreateEntity(3);
        Assert.Equal(3, entity.Get<int>());
    }

    [Fact]
    public void Get_Reference()
    {
        var entity = world.CreateEntity(3);
        Assert.Equal(3, entity.Get<int>());

        ref var count = ref entity.Get<int>();
        
        count += 4;

        Assert.Equal(7, entity.Get<int>());
    }

    [Fact]
    public void Get_NoRef()
    {
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
        var entity = world.CreateEntity(3);
        Assert.True(entity.Has<int>());

        entity.Remove<int>();
        Assert.False(entity.Has<int>());
    }
}
