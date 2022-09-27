namespace SimpleECS;

/// <summary>
/// Operates on all entities that match it's filters
/// </summary>
public partial class Query : IEnumerable<Archetype>
{
    private readonly TypeSignature _include;
    private readonly TypeSignature _exclude;

    private Archetype[] _matchingArchetypes = new Archetype[8];
    private int _lastLookup;
    private int _structureUpdate;
    private int _archetypeCount;

    private World _world;

    /// <summary>
    /// the world the query operates on
    /// </summary>
    public World World
    {
        get => _world;
        set
        {
            _structureUpdate = -1;
            _world = value;
        }
    }

    internal Query(World world)
    {
        _world = world;
        _include = new(world.TypeIds);
        _exclude = new(world.TypeIds);
    }
           
    public static implicit operator bool(Query query) => query == null ? false : query._world != null;

    /// <summary>
    /// Returns a copy of all archetypes matching the query
    /// </summary>
    public Archetype[] GetArchetypes()
    {
        CheckQueryChanges();
        Archetype[] archetypes = new Archetype[_archetypeCount];
        for (int i = 0; i < _archetypeCount; ++i) archetypes[i] = _matchingArchetypes[i];
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
        for (int i = 0; i < _archetypeCount; ++i)
        {
            if (_matchingArchetypes[i].TryGetArchetypeInfo(out var arch_info))
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
        _archetypeCount = 0;
        _structureUpdate = -1;
        _include.Add<T>();
        return this;
    }


    /// <summary>
    /// filters entities to those that do not have component
    /// </summary>
    public Query Not<T>()
    {
        _archetypeCount = 0;
        _structureUpdate = -1;
        _exclude.Add<T>();
        return this;
    }

    /// <summary>
    /// filters entities to those that have components
    /// </summary>
    public Query Has(params Type[] types)
    {
        _archetypeCount = 0;
        _structureUpdate = -1;
        _include.Add(types);
        return this;
    }

    /// <summary>
    /// filters entities to those that do not have components
    /// </summary>
    public Query Not(params Type[] types)
    {
        _archetypeCount = 0;
        _structureUpdate = -1;
        _exclude.Add(types);
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
        _include.Clear();
        _exclude.Clear();
        _archetypeCount = 0;
        _structureUpdate = -1;
        return this;
    }

    /// <summary>
    /// iterates and peforms action on all entities that match the query
    /// </summary>
    public void Foreach(in Action<Entity> action)
    {
        CheckQueryChanges();
        _world.StructureEvents.EnqueueEvents++;
        for (int archetype_index = 0; archetype_index < _archetypeCount; ++archetype_index)
        {
            var archetype = _world.Archetypes[_matchingArchetypes[archetype_index]].data;
            int count = archetype.EntityCount;
            var entities = archetype.Entities;
            if (count > 0)
            {
                for (int e = 0; e < count; ++e)
                    action(entities[e]);
            }
        }
        _world.StructureEvents.EnqueueEvents--;
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
        if (_world.ArchetypeStructureUpdateCount != _structureUpdate)
        {
            _lastLookup = 0;
            _archetypeCount = 0;
            _structureUpdate = _world.ArchetypeStructureUpdateCount;
        }
        for (; _lastLookup < _world.ArchetypeTerminatingIndex; ++_lastLookup)
        {
            var arch = _world.Archetypes[_lastLookup].data;
            if (arch == null) continue;
            if (arch.Signature.HasAll(_include) && !arch.Signature.HasAny(_exclude))
            {
                if (_archetypeCount == _matchingArchetypes.Length) Array.Resize(ref _matchingArchetypes, _archetypeCount * 2);
                _matchingArchetypes[_archetypeCount] = arch.Archetype;
                ++_archetypeCount;
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
            for (int i = 0; i < _archetypeCount; ++i) count += _world.Archetypes[_matchingArchetypes[i]].data.EntityCount;
            return count;
        }
    }

    public override string ToString()
    {
        return "Query" +
        (_include.Count > 0 ? $" -> Has {_include.TypesToString()}" : "") +
        (_exclude.Count > 0 ? $" -> Not {_exclude.TypesToString()}" : "");
    }

    /// <summary>
    /// returns all the types in the queries' has filter
    /// </summary>
    public IReadOnlyList<Type> GetHasFilterTypes() => _include.Types;

    /// <summary>
    /// returns all the types in the queries' not filter
    /// </summary>
    public IReadOnlyList<Type> GetNotFilterTypes() => _exclude.Types;


    IEnumerator<Archetype> IEnumerable<Archetype>.GetEnumerator()
    {
        CheckQueryChanges();
        for (int i = 0; i < _archetypeCount; ++i)
        {
            yield return _world.Archetypes[_matchingArchetypes[i]].data.Archetype;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        CheckQueryChanges();
        for (int i = 0; i < _archetypeCount; ++i)
        {
            yield return _world.Archetypes[_matchingArchetypes[i]].data.Archetype;
        }
    }
}