namespace SimpleECS.Tests;

public class WorldTests
{
	[Fact]
	public void IsValid_DefaultWorld_NotValid()
	{
		var world = new World();

		Assert.False(world.IsValid());
	}

	[Fact]
	public void IsValid_StaticCreate_Valid()
	{
		var world = World.Create();

		Assert.True(world.IsValid());
	}

	[Fact]
	public void IsValid_DestroyedWorld_NotValid()
	{
		var world = World.Create();
		Assert.True(world.IsValid());

		world.Destroy();
		Assert.False(world.IsValid());
	}

	[Fact]
	public void Create_TwoCreatedWorldAreDifferent()
	{
		var world1 = World.Create();
		var world2 = World.Create();

		Assert.NotEqual(world1, world2);
	}

	[Fact]
	public void GetOrCreate_SameName_ReturnsSameWorld()
	{
		var sharedName = "TestWorld";
		var world1 = World.Create(sharedName);
		var world2 = World.GetOrCreate(sharedName);

		Assert.Equal(world1, world2);
	}

	[Fact]
	public void GetOrCreate_DifferentName_ReturnsNewWorld()
	{
		var world1 = World.Create("World1");
		var world2 = World.GetOrCreate("World2");

		Assert.NotEqual(world1, world2);
	}

	[Fact]
	public void Name_ChangeNameSameWorld()
	{
		var originalName = "OldName";

		var world1 = World.Create(originalName);
		var world2 = World.GetOrCreate(originalName);

		var updatedName = "UpdatedName";
		world1.Name = updatedName;

		Assert.Equal(updatedName, world2.Name);
	}
}