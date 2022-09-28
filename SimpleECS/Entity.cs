namespace SimpleECS;

/// <summary>
/// Acts as a container of a set of components. Can be filtered by queries to get entities that have speicified components.
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    /// <summary>
    /// The world this entity belongs to.
    /// </summary>
    public readonly int WorldId;

    /// <summary>
    /// the combination of the index and version act as a unique identifier for the entity
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// the combination of the index and version act as a unique identifier for the entity
    /// </summary>
    public readonly int Version;

    internal Entity(int index, int version, int worldId)
    {
        Index = index;
        Version = version;
        WorldId = worldId;
    }

    /// <summary>
    /// the archetype that the entity belongs to
    /// </summary>
    public Archetype Archetype
    {
        get
        {
            ref var info = ref World.All[WorldId].Entities[Index];
            if (info.version == Version) return info.ArchInfo.Archetype;
            return default;
        }
    }

    

    /// <summary>
    /// returns entity's string value if set
    /// </summary>
    public override string ToString()
    {
        TryGet(out string name);
        if (string.IsNullOrEmpty(name))
            name = IsValid() ? "Entity" : "~Entity";
        return $"{name} {Index}.{Version}";
    }

    /// <summary>
    /// returns true if the the entity is not destroyed or null
    /// </summary>
    public bool IsValid()
    {
        return World.All[WorldId] != null && World.All[WorldId].Entities[Index].version == Version;
    }

    /// <summary>
    /// returns true if the entity has the component
    /// </summary>
    public bool Has<Component>()
    {
        ref var info = ref World.All[WorldId].Entities[Index];
        if (info.version == Version)
            return info.ArchInfo.Has(World.All[WorldId].TypeIds.Get<Component>());
        return false;
    }

    /// <summary>
    /// [structural]
    /// removes the component from the entity.
    /// if component was removed will trigger the corresponding onremove component event
    /// </summary>
    public Entity Remove<Component>()
    {
        World.All[WorldId]?.StructureEvents.Remove<Component>(this);
        return this;
    }

    /// <summary>
    /// [structural]
    /// sets the entity's component to value. 
    /// If the entity does not have the component, will move the entity to an archetype that does.
    /// will trigger the onset component event if component was set
    /// </summary>
    public Entity Set<Component>(in Component component)
    {
        World.All[WorldId]?.StructureEvents.Set(this, component);
        return this;
    }

    /// <summary>
    /// returns true if the entity has component, outputs the component
    /// </summary>
    public bool TryGet<Component>(out Component component)
    {
        ref var info = ref World.All[WorldId].Entities[Index];
        if (info.version == Version)
        {
            if (info.ArchInfo.TryGetArray<Component>(out var buffer))
            {
                component = buffer[info.arch_index];
                return true;
            }
        }
        component = default;
        return false;
    }

    /// <summary>
    /// gets the reference to the component on the entity.
    /// throws an exception if the entity is invalid or does not have the component
    /// </summary>
    public ref Component Get<Component>()
    {
        ref var entity_info = ref World.All[WorldId].Entities[Index];
        if (entity_info.version == Version)
        {
            if (entity_info.ArchInfo.TryGetArray<Component>(out var buffer)) return ref buffer[entity_info.arch_index];
            throw new Exception($"{this} does not contain {typeof(Component).Name}");
        }
        throw new Exception($"{this} is not a valid entity, cannot get {typeof(Component).Name}");
    }

    /// <summary>
    /// [structural]
    /// destroys the entity
    /// </summary>
    public void Destroy()
    {
        World.All[WorldId]?.StructureEvents.Destroy(this);
    }

    bool IEquatable<Entity>.Equals(Entity other) => Index == other.Index && Version == other.Version;

    public override bool Equals(object obj) => obj is Entity e ? e == this : false;

    public static bool operator ==(Entity a, Entity b) => a.Index == b.Index && a.Version == b.Version;

    public static bool operator !=(Entity a, Entity b) => !(a == b);

    public override int GetHashCode() => Index;

    public static implicit operator bool(Entity entity) => entity.IsValid();

    /// <summary>
    /// returns how many components are attached to the entity
    /// </summary>
    public int GetComponentCount()
    {
        ref var info = ref World.All[WorldId].Entities[Index];
        if (info.version == Version) return info.ArchInfo.ComponentCount;
        return 0;
    }
}