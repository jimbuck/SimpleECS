namespace SimpleECS;

/// <summary>
/// Operates on all entities that match it's filters
/// </summary>
public partial class Query : IEnumerable<Archetype>
{
    private readonly TypeSignature include;
    private readonly TypeSignature exclude;

    private Archetype[] matching_archetypes = new Archetype[8];
    private int last_lookup;
    private int structure_update;
    private int archetype_count;

    private World world;

    /// <summary>
    /// the world the query operates on
    /// </summary>
    public World World
    {
        get => world;
        set
        {
            structure_update = -1;
            world = value;
        }
    }

    internal Query(World world)
    {
        this.world = world;
        include = new(world.typeIds);
        exclude = new(world.typeIds);
    }
           
    public static implicit operator bool(Query query) => query == null ? false : query.world != null;

    /// <summary>
    /// Returns a copy of all archetypes matching the query
    /// </summary>
    public Archetype[] GetArchetypes()
    {
        CheckQueryChanges();
        Archetype[] archetypes = new Archetype[archetype_count];
        for (int i = 0; i < archetype_count; ++i) archetypes[i] = matching_archetypes[i];
        return archetypes;
    }

    /// <summary>
    /// Returns a copy of all entities matching the query
    /// </summary>
    public Entity[] GetEntities()
    {
        CheckQueryChanges();
        Entity[] entities = new Entity[EntityCount];
        int count = 0;
        for (int i = 0; i < archetype_count; ++i)
        {
            if (matching_archetypes[i].TryGetArchetypeInfo(out var arch_info))
            {
                for (int e = 0; e < arch_info.EntityCount; ++e)
                {
                    entities[count] = arch_info.Entities[e];
                    count++;
                }
            }
        }
        return entities;
    }

    /// <summary>
    /// filters entities to those that have component
    /// </summary>
    public Query Has<T>()
    {
        archetype_count = 0;
        structure_update = -1;
        include.Add<T>();
        return this;
    }


    /// <summary>
    /// filters entities to those that do not have component
    /// </summary>
    public Query Not<T>()
    {
        archetype_count = 0;
        structure_update = -1;
        exclude.Add<T>();
        return this;
    }

    /// <summary>
    /// filters entities to those that have components
    /// </summary>
    public Query Has(params Type[] types)
    {
        archetype_count = 0;
        structure_update = -1;
        include.Add(types);
        return this;
    }

    /// <summary>
    /// filters entities to those that do not have components
    /// </summary>
    public Query Not(params Type[] types)
    {
        archetype_count = 0;
        structure_update = -1;
        exclude.Add(types);
        return this;
    }

    /// <summary>
    /// filters entities to those that have components
    /// </summary>
    public Query Has(IEnumerable<Type> types)
    {
        if (types != null)
            foreach (var type in types)
                Has(type);
        return this;
    }

    /// <summary>
    /// filters entities to those that do not have components
    /// </summary>
    public Query Not(IEnumerable<Type> types)
    {
        if (types != null)
            foreach (var type in types)
                Not(type);
        return this;
    }

    /// <summary>
    /// clears all previous filters on the query
    /// </summary>
    public Query Clear()
    {
        include.Clear();
        exclude.Clear();
        archetype_count = 0;
        structure_update = -1;
        return this;
    }

    /// <summary>
    /// iterates and peforms action on all entities that match the query
    /// </summary>
    public void Foreach(in Action<Entity> action)
    {
        CheckQueryChanges();
        world.StructureEvents.EnqueueEvents++;
        for (int archetype_index = 0; archetype_index < archetype_count; ++archetype_index)
        {
            var archetype = world.Archetypes[matching_archetypes[archetype_index]].data;
            int count = archetype.EntityCount;
            var entities = archetype.Entities;
            if (count > 0)
            {
                for (int e = 0; e < count; ++e)
                    action(entities[e]);
            }
        }
        world.StructureEvents.EnqueueEvents--;
    }

    /// <summary>
    /// Destroys matching archetypes along with their entities.
    /// Most efficient way to destroy entities.
    /// </summary>
    public void DestroyMatching()
    {
        CheckQueryChanges();
        foreach (var archetype in GetArchetypes()) archetype.Destroy();
    }

    // keeps the queried archtypes up to date, return false if the query is not valid
    private void CheckQueryChanges()
    {
        if (world.ArchetypeStructureUpdateCount != structure_update)
        {
            last_lookup = 0;
            archetype_count = 0;
            structure_update = world.ArchetypeStructureUpdateCount;
        }
        for (; last_lookup < world.ArchetypeTerminatingIndex; ++last_lookup)
        {
            var arch = world.Archetypes[last_lookup].data;
            if (arch == null) continue;
            if (arch.Signature.HasAll(include) && !arch.Signature.HasAny(exclude))
            {
                if (archetype_count == matching_archetypes.Length) Array.Resize(ref matching_archetypes, archetype_count * 2);
                matching_archetypes[archetype_count] = arch.Archetype;
                ++archetype_count;
            }
        }
    }

    /// <summary>
    /// the total number of entities that currently match the query
    /// </summary>
    /// <value></value>
    public int EntityCount
    {
        get
        {
            int count = 0;
            CheckQueryChanges();
            for (int i = 0; i < archetype_count; ++i) count += world.Archetypes[matching_archetypes[i]].data.EntityCount;
            return count;
        }
    }

    public override string ToString()
    {
        return "Query" +
        (include.Count > 0 ? $" -> Has {include.TypesToString()}" : "") +
        (exclude.Count > 0 ? $" -> Not {exclude.TypesToString()}" : "");
    }

    /// <summary>
    /// returns all the types in the queries' has filter
    /// </summary>
    public IReadOnlyList<Type> GetHasFilterTypes() => include.Types;

    /// <summary>
    /// returns all the types in the queries' not filter
    /// </summary>
    public IReadOnlyList<Type> GetNotFilterTypes() => exclude.Types;


    IEnumerator<Archetype> IEnumerable<Archetype>.GetEnumerator()
    {
        CheckQueryChanges();
        for (int i = 0; i < archetype_count; ++i)
        {
            yield return world.Archetypes[matching_archetypes[i]].data.Archetype;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        CheckQueryChanges();
        for (int i = 0; i < archetype_count; ++i)
        {
            yield return world.Archetypes[matching_archetypes[i]].data.Archetype;
        }
    }
}