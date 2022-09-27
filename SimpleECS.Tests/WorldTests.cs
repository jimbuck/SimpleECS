namespace SimpleECS.Tests;

public class WorldTests
{
    #region Destroy

    [Fact]
    public void Destroy_Entities_NotValid()
    {
        var world = new World(nameof(Destroy_Entities_NotValid));

        var entity = world.CreateEntity("my entity", 3, 5f);
        Assert.True(entity.IsValid());

        world.Dispose();
        Assert.False(entity.IsValid());
    }

    #endregion

    #region Create/GetOrCreate

    [Fact]
    public void Create_SameName_DifferentWorlds()
    {
        var world1 = new World(nameof(Create_SameName_DifferentWorlds));
        var world2 = new World(nameof(Create_SameName_DifferentWorlds));

        world1.CreateEntity(1);

        Assert.NotSame(world1, world2);
        Assert.Equal(1, world1.EntityCount);
        Assert.Equal(0, world2.EntityCount);
    }

    #endregion

    #region WorldData

    [Fact]
    public void WorldData_SetThenGet()
    {
        var world = new World(nameof(WorldData_SetThenGet));
        var str = "Test String";
        world.SetData(str);
        Assert.Equal(str, world.GetData<string>());
    }

    [Fact]
    public void WorldData_InQuery()
    {      
        var world = new World();
        var delta_time = 1f;
        world.SetData(delta_time);

        var entity = world.CreateEntity(0f);
        var query = world.CreateQuery().Has<float>();

        query.Foreach((in float dt, Entity entity, ref float velocity) =>
        {
            Assert.Equal(delta_time, dt);
            entity.Set(dt * 4f);
            Assert.Equal(0, entity.Get<float>());
        });
        Assert.Equal(4, entity.Get<float>());
    }

    [Fact]
    public void WorldData_GetAllWorldData()
    {
        var world = new World(nameof(WorldData_GetAllWorldData));
        var name = "My Value";
        var count = 7;
        var delta_time = 0.123f;

        world.SetData(count).SetData(name);
        world.GetData<float>() = delta_time;

        var worldData = world.GetAllWorldData();
        Assert.Equal(3, worldData.Length);
        Assert.Contains(name, worldData);
        Assert.Contains(count, worldData);
        Assert.Contains(delta_time, worldData);
    }

    [Fact]
    public void WorldData_GetAllWorldDataTypes()
    {
        var world = new World(nameof(WorldData_GetAllWorldDataTypes));
        var name = "My Value";
        var count = 7;
        var delta_time = 0.123f;

        world.SetData(count).SetData(name);
        world.GetData<float>() = delta_time;

        var worldDataTypes = world.GetAllWorldDataTypes();
        Assert.Equal(3, worldDataTypes.Length);
        Assert.Contains(typeof(string), worldDataTypes);
        Assert.Contains(typeof(int), worldDataTypes);
        Assert.Contains(typeof(float), worldDataTypes);
    }

    #endregion

    #region OnSet

    [Fact]
    public void OnSet_NewValueOnly()
    {
        var world = new World(nameof(OnSet_NewValueOnly));
        var oldValue = 2;
        var newValue = 4;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        world.OnSet((ref int new_value) =>
        {
            Assert.Equal(newValue, new_value);
            triggered++;
        });

        entity.Set(newValue);

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void OnSet_EntityAndNewValue()
    {
        var world = new World(nameof(OnSet_EntityAndNewValue));
        var oldValue = 2;
        var newValue = 4;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        world.OnSet((Entity _entity, ref int new_value) =>
        {
            Assert.Equal(entity, _entity);
            Assert.Equal(newValue, new_value);
            triggered++;
        });

        entity.Set(newValue);

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void OnSet_EntityNewAndOldValue()
    {
        var world = new World(nameof(OnSet_EntityNewAndOldValue));
        var oldValue = 2;
        var newValue = 4;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        world.OnSet((Entity _entity, int old_value, ref int new_value) =>
        {
            Assert.Equal(entity, _entity);
            Assert.Equal(newValue, new_value);
            Assert.Equal(oldValue, old_value);
            triggered++;
        });

        entity.Set(newValue);

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void OnSet_NamedCallback()
    {
        var world = new World(nameof(OnSet_EntityAndNewValue));
        var oldValue = 2;
        var newValue = 4;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        void IntSetCallback(ref int value)
        {
            Assert.Equal(newValue, value);
            triggered++;
        }

        world.OnSet<int>(IntSetCallback);

        entity.Set(newValue);
        Assert.Equal(newValue, entity.Get<int>());

        world.OnSet<int>(IntSetCallback, false);

        entity.Set(oldValue);
        Assert.Equal(oldValue, entity.Get<int>());

        Assert.Equal(1, triggered);
    }

    #endregion

    #region OnRemove

    [Fact]
    public void OnRemove_ValueOnly()
    {
        var world = new World(nameof(OnRemove_ValueOnly));
        var oldValue = 2;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        world.OnRemove((int value) =>
        {
            Assert.Equal(oldValue, value);
            triggered++;
        });

        entity.Remove<int>();

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void OnRemove_EntityAndValue()
    {
        var world = new World(nameof(OnRemove_EntityAndValue));
        var oldValue = 2;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        world.OnRemove((Entity e, int value) =>
        {
            Assert.Equal(entity, e);
            Assert.Equal(oldValue, value);
            triggered++;
        });

        entity.Remove<int>();

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void OnRemove_NamedCallback()
    {
        var world = new World(nameof(OnRemove_NamedCallback));
        var oldValue = 2;
        var triggered = 0;

        var entity = world.CreateEntity(oldValue);

        void IntRemoveCallback(int value)
        {
            Assert.Equal(oldValue, value);
            triggered++;
        }

        world.OnRemove<int>(IntRemoveCallback);

        Assert.True(entity.Has<int>());
        entity.Remove<int>();
        Assert.False(entity.Has<int>());
        Assert.Equal(1, triggered);

        world.OnRemove<int>(IntRemoveCallback, false);

        entity.Set(oldValue);
        Assert.True(entity.Has<int>());
        entity.Remove<int>();
        Assert.False(entity.Has<int>());

        Assert.Equal(1, triggered);
    }

    #endregion
}