namespace SimpleECS;

/// <summary>
/// stores component data of entities that matches the archetype's type signature
/// </summary>
public readonly struct Archetype : IEquatable<Archetype>, IEnumerable<Entity> 
{
    /// <summary>
    /// The world this archetype belongs to.
    /// </summary>
    public readonly int WorldId;

    /// <summary>
    /// the index and version create a unique identifier for the archetype
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// the index and version create a unique identifier for the archetype
    /// </summary>
    public readonly int Version;

    internal Archetype(int worldId, int index, int version)
    {
        WorldId = worldId;
        Index = index;
        Version = version;
    }

    /// <summary>
    /// returns a copy of archetype's type signature
    /// </summary>
    internal TypeSignature GetTypeSignature() => TryGetArchetypeInfo(out var archetype_Info) ? new TypeSignature(World.All[WorldId].TypeIds, archetype_Info.Signature) : new TypeSignature(World.All[WorldId].TypeIds);

    /// <summary>
    /// returns a copy of component types in this archetype
    /// </summary>
    public Type[] GetTypes() => TryGetArchetypeInfo(out var archetype_Info) ? archetype_Info.GetComponentTypes() : Array.Empty<Type>();
       

    /// <summary>
    /// [structural]
    /// creates an entity that matches this archetype
    /// </summary>
    public Entity CreateEntity()
    {
        if (World.All[WorldId] != null && TryGetArchetypeInfo(out var archetype_info))
            return World.All[WorldId].StructureEvents.CreateEntity(archetype_info);
        return default;
    }

    /// <summary>
    /// returns a copy of all the entities stored in the archetype
    /// </summary>
    public Entity[] GetEntities()
    {
        Entity[] entities = new Entity[EntityCount];
        if (TryGetArchetypeInfo(out var archetype_info))
            for (int i = 0; i < archetype_info.EntityCount; ++i)
                entities[i] = archetype_info.Entities[i];
        return entities;
    }

    /// <summary>
    /// returns the total amount of entities stored in the archetype
    /// </summary>
    public int EntityCount => TryGetArchetypeInfo(out var archetype_Info) ? archetype_Info.EntityCount : 0;

    /// <summary>
    /// returns false if the archetype is invalid or destroyed.
    /// outputs the raw entity storage buffer.
    /// </summary>
    public bool TryGetEntityBuffer(out ReadOnlySpan<Entity> entity_buffer)
    {
        if (TryGetArchetypeInfo(out var data))
        {
            entity_buffer = new ReadOnlySpan<Entity>(data.Entities, 0, EntityCount);
            return true;
        }
        entity_buffer = default;
        return false;
    }

    /// <summary>
    /// returns false if the archetype is invalid or does not store the component buffer
    /// outputs the raw component storage buffer.
    /// entities in the entity buffer that share the same index as the component in the component buffer own that component
    /// </summary>
    public bool TryGetComponentBuffer<Component>(out Span<Component> comp_buffer)
    {
        if (TryGetArchetypeInfo(out var data) && data.TryGetArray(out Component[] componentArray))
        {
            comp_buffer = new Span<Component>(componentArray, 0, EntityCount);
            return true;
        }

        comp_buffer = default;
        return false;
    }

    /// <summary>
    /// [structural]
    /// destroys the archetype along with all the entities within it
    /// </summary>
    public void Destroy()
    {
        World.All[WorldId]?.StructureEvents.DestroyArchetype(this);
    }

    /// <summary>
    /// [structural]
    /// resizes the archetype's backing arrays to the minimum number of 2 needed to store the entities
    /// </summary>
    public void ResizeBackingArrays()
    {
        World.All[WorldId]?.StructureEvents.ResizeBackingArrays(this);
    }

    bool IEquatable<Archetype>.Equals(Archetype other) => WorldId == other.WorldId && Index == other.Index && Version == other.Version;

    /// <summary>
    /// returns true if the archetype is not null or destroyed
    /// </summary>
    public bool IsValid() => World.All[WorldId] != null && World.All[WorldId].Archetypes[Index].version == Version;

    public static implicit operator bool(Archetype archetype) => archetype.IsValid();

    public override bool Equals(object obj) => obj is Archetype a && a == this;

    public static implicit operator int(Archetype a) => a.Index;

    public static bool operator ==(Archetype a, Archetype b) => a.WorldId == b.WorldId && a.Index == b.Index && a.Version == b.Version;

    public static bool operator !=(Archetype a, Archetype b) => !(a == b);

    public override int GetHashCode() => Index;

    public override string ToString() => $"{(IsValid() ? "" : "~")}Arch [{GetTypeString()}]";

    string GetTypeString()
    {
        string val = "";
        if (TryGetArchetypeInfo(out var archetype_info))
        {
            for(int i = 0; i < archetype_info.ComponentCount; ++ i)
            {
                val += $" {World.All[WorldId].TypeIds.Get(archetype_info.ComponentBuffers[i].type_id).Name}";
            }
        }
        return val;
    }

    internal bool TryGetArchetypeInfo(out Archetype_Info arch_info)
    {
        var arch = World.All[WorldId]?.Archetypes[Index];
        if (arch.HasValue && arch.Value.version == Version)
        {
            arch_info = arch.Value.data;
            return true;
        }

        arch_info = default;
        return false;
    }

    IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator()
    {
        if (TryGetArchetypeInfo(out var info))
            for(int i = 0; i < info.EntityCount; ++ i)
                yield return info.Entities[i];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        if (TryGetArchetypeInfo(out var info))
            for(int i = 0; i < info.EntityCount; ++ i)
                yield return info.Entities[i];
    }
}

internal class Archetype_Info
{
    public readonly int WorldId;
    public readonly TypeSignature Signature;
    public readonly Archetype Archetype;
    public readonly int ComponentCount;

    public Entity[] Entities = new Entity[8];
    public int EntityCount;
    public CompBufferData[] ComponentBuffers { get; }

    public Archetype_Info(int worldId, TypeSignature signature, int arch_index, int arch_version)
    {
        WorldId = worldId;
        Signature = signature;
        Archetype = new Archetype(worldId, arch_index, arch_version);

        ComponentBuffers = new CompBufferData[signature.Count == 0 ? 1 : signature.Count];
        ComponentCount = signature.Count;

        for (int i = 0; i < ComponentBuffers.Length; ++i)
            ComponentBuffers[i].next = -1;

        // add components into empty bucket, skip if bucket is occupied
        for (int i = 0; i < ComponentCount; ++i)
        {
            var type = signature.Types[i];
            var type_id = World.All[WorldId].TypeIds.Get(type);
            var index = type_id % ComponentBuffers.Length;
            ref var buffer_data = ref ComponentBuffers[index];
            if (buffer_data.type_id == 0)
            {
                buffer_data.type_id = type_id;
                buffer_data.buffer = CreatePool(type);
            }
        }

        // add skipped components into buckets not filled in first pass
        // hopefully this minimizes lookup time
        for (int i = 0; i < ComponentCount; ++i)
        {
            var type = signature.Types[i];
            var type_id = World.All[WorldId].TypeIds.Get(type);
            if (ContainsType(type_id)) continue;
            var index = GetEmptyIndex(type_id % ComponentBuffers.Length);
            ref var buffer_data = ref ComponentBuffers[index];
            buffer_data.type_id = type_id;
            buffer_data.buffer = CreatePool(type);
        }

        bool ContainsType(int type_id)
        {
            foreach (var val in ComponentBuffers)
                if (val.type_id == type_id) return true;
            return false;
        }

        // if current index is filled, will return an empty index with a way to get to that index from the provided one
        int GetEmptyIndex(int current_index)
        {
            if (ComponentBuffers[current_index].type_id == 0)
                return current_index;

            while (ComponentBuffers[current_index].next >= 0)
            {
                current_index = ComponentBuffers[current_index].next;
            }

            for (int i = 0; i < ComponentCount; ++i)
            {
                if (ComponentBuffers[i].type_id == 0)
                {
                    ComponentBuffers[current_index].next = i;
                    return i;
                }
            }

            throw new Exception("FRAMEWORK BUG: not enough components in archetype");
        }

        CompBuffer CreatePool(Type type) => (Activator.CreateInstance(typeof(CompBuffer<>).MakeGenericType(type)) is CompBuffer buffer) ? buffer : throw new Exception("Failed to create CompBuffer!");
    }



    public struct CompBufferData
    {
        public int next;
        public int type_id;
        public CompBuffer buffer;
    }

    /// <summary>
    /// resizes all backing arrays to minimum power of 2
    /// </summary>
    public void ResizeBackingArrays()
    {
        int size = 8;
        while (size <= EntityCount) size *= 2;
        Array.Resize(ref Entities, size);
        for (int i = 0; i < ComponentCount; ++i)
            ComponentBuffers[i].buffer.Resize(size);
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity >= Entities.Length)
        {
            int size = Entities.Length;
            while (capacity >= size) size *= 2;
            Array.Resize(ref Entities, size);
            for (int i = 0; i < ComponentCount; ++i)
                ComponentBuffers[i].buffer.Resize(size);
        }
    }

    public bool Has(int type_id)
    {
        var data = ComponentBuffers[type_id % ComponentBuffers.Length];
        if (data.type_id == type_id)
            return true;

        while (data.next >= 0)
        {
            data = ComponentBuffers[data.next];
            if (data.type_id == type_id)
                return true;
        }
        return false;
    }

    public bool TryGetArray<Component>(out Component[] components)
    {
        int type_id = World.All[WorldId].TypeIds.Get<Component>();
        var data = ComponentBuffers[type_id % ComponentBuffers.Length];
        if (data.type_id == type_id)
        {
            components = (Component[])data.buffer.array;
            return true;
        }
        while (data.next >= 0)
        {
            data = ComponentBuffers[data.next];
            if (data.type_id == type_id)
            {
                components = (Component[])data.buffer.array;
                return true;
            }
        }
        components = default;
        return false;
    }

    public object[] GetAllComponents(int entity_arch_index)
    {
        object[] components = new object[ComponentCount];

        for (int i = 0; i < ComponentCount; ++i)
            components[i] = ComponentBuffers[i].buffer.array[entity_arch_index];

        return components;
    }

    public Type[] GetComponentTypes()
    {
        Type[] components = new Type[ComponentCount];
        for (int i = 0; i < ComponentCount; ++i)
            components[i] = World.All[WorldId].TypeIds.Get(ComponentBuffers[i].type_id);
        return components;
    }

    public abstract class CompBuffer    //handles component data
    {
        public IList array;
        public abstract void Resize(int capacity);
        /// <summary>
        /// returns removed component
        /// </summary>
        public abstract object Remove(int entity_arch_index, int last);
        public abstract void Move(int entity_arch_index, int last_entity_index, Archetype_Info target_archetype, int target_index);
        public abstract void Move(int entity_arch_index, int last_entity_index, object buffer, int target_index);
    }

    public sealed class CompBuffer<Component> : CompBuffer
    {
        public CompBuffer()
        {
            array = components;
        }

        public Component[] components = new Component[8];

        public override void Resize(int capacity)
        {
            Array.Resize(ref components, capacity);
            array = components;
        }

        public override object Remove(int entity_arch_index, int last)
        {
            var comp = components[entity_arch_index];
            components[entity_arch_index] = components[last];
            components[last] = default;
            return comp;
        }

        public override void Move(int entity_arch_index, int last_entity_index, Archetype_Info target_archetype, int target_index)
        {
            if (target_archetype.TryGetArray<Component>(out var target_array))
            {
                target_array[target_index] = components[entity_arch_index];
            }
            components[entity_arch_index] = components[last_entity_index];
            components[last_entity_index] = default;
        }

        public override void Move(int entity_arch_index, int last_entity_index, object buffer, int target_index)
        {
            ((Component[])buffer)[target_index] = components[entity_arch_index];
            components[entity_arch_index] = components[last_entity_index];
            components[last_entity_index] = default;
        }
    }
}