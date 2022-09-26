namespace SimpleECS.Tests;

public class WorldTests
{
    #region IsValid

    [Fact]
    public void IsValid_WorldConstructor_NotValid()
    {
        var world = new World();

        Assert.False(world.IsValid());
    }

    [Fact]
    public void IsValid_StaticCreate_Valid()
    {
        var world = World.Create(nameof(IsValid_StaticCreate_Valid));

        Assert.True(world.IsValid());
    }

    [Fact]
    public void IsValid_DestroyedWorld_NotValid()
    {
        var world = World.Create(nameof(IsValid_DestroyedWorld_NotValid));
        Assert.True(world.IsValid());

        world.Destroy();
        Assert.False(world.IsValid());
    }

    #endregion

    #region Destroy

    [Fact]
    public void Destroy_Entities_NotValid()
    {
        var world = World.Create(nameof(Destroy_Entities_NotValid));
        Assert.True(world.IsValid());

        var entity = world.CreateEntity("my entity", 3, 5f);
        Assert.True(entity.IsValid());

        world.Destroy();
        Assert.False(world.IsValid());
        Assert.False(entity.IsValid());
    }

    #endregion

    #region Create/GetOrCreate

    [Fact]
    public void Create_SameName_DifferentWorlds()
    {
        var world1 = World.Create(nameof(Create_SameName_DifferentWorlds));
        var world2 = World.Create(nameof(Create_SameName_DifferentWorlds));

        Assert.NotEqual(world1, world2);
    }

    [Fact]
    public void GetOrCreate_SameName_ReturnsSameWorld()
    {
        var sharedName = nameof(GetOrCreate_SameName_ReturnsSameWorld);
        var world1 = World.Create(sharedName);
        var world2 = World.GetOrCreate(sharedName);

        Assert.Equal(world1, world2);
    }

    [Fact]
    public void GetOrCreate_DifferentName_ReturnsNewWorld()
    {
        var world1 = World.Create(nameof(GetOrCreate_DifferentName_ReturnsNewWorld) + "1");
        var world2 = World.GetOrCreate(nameof(GetOrCreate_DifferentName_ReturnsNewWorld) + "2");

        Assert.NotEqual(world1, world2);
    }

    #endregion

    #region Name

    [Fact]
    public void Name_ChangeNameSameWorld()
    {
        var originalName = nameof(Name_ChangeNameSameWorld) + "Old";

        var world1 = World.Create(originalName);
        var world2 = World.GetOrCreate(originalName);

        var updatedName = nameof(Name_ChangeNameSameWorld) + "New";
        world1.Name = updatedName;

        Assert.Equal(updatedName, world2.Name);
    }

    #endregion

    #region WorldData

    [Fact]
    public void WorldData_SetThenGet()
    {
        var world = World.Create(nameof(WorldData_SetThenGet));
        var str = "Test String";
        world.SetData(str);
        Assert.Equal(str, world.GetData<string>());
    }

    [Fact]
    public void WorldData_InQuery()
    {
        var world = World.Create(nameof(WorldData_SetThenGet));
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
        var world = World.Create(nameof(WorldData_GetAllWorldData));
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
        var world = World.Create(nameof(WorldData_GetAllWorldDataTypes));
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
        var world = World.Create(nameof(OnSet_NewValueOnly));
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
        var world = World.Create(nameof(OnSet_EntityAndNewValue));
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
        var world = World.Create(nameof(OnSet_EntityNewAndOldValue));
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
        var world = World.Create(nameof(OnSet_EntityAndNewValue));
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
        var world = World.Create(nameof(OnRemove_ValueOnly));
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
        var world = World.Create(nameof(OnRemove_EntityAndValue));
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
        var world = World.Create(nameof(OnRemove_NamedCallback));
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