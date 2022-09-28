namespace SimpleECS;

public delegate void SetComponentEvent<T>(Entity entity, T old_comp, ref T new_comp);
public delegate void SetComponentEventRefOnly<T>(Entity entity, ref T new_comp);
public delegate void SetComponentEventCompOnly<T>(ref T new_comp);
public delegate void RemoveComponentEvent<T>(Entity entity, T component);
public delegate void RemoveComponentEventCompOnly<T>(T component);

/// <summary>
/// manages all entities and archetype information
/// </summary>
public partial class World : IEnumerable<Archetype>, IDisposable
{
    internal static World[] All = new World[2];
    private static readonly object _lockObject = new();
    private static IdPool _worldIds = new();

    internal int WorldId;

    internal Entity_Info[] Entities;
    internal Queue<int> FreeIds;

    internal int LastEntityId { get; set; }

    private bool _cacheStructuralChanges;
    internal bool CacheStructuralChanges
    {
        get => _cacheStructuralChanges;
        set
        {
            StructureEvents.EnqueueEvents += value ? 1 : -1;
            _cacheStructuralChanges = value;
        }
    }

    internal int ArchetypeCount => ArchetypeTerminatingIndex - FreeArchetypes.Count;
    internal int ArchetypeTerminatingIndex { get; set; }
    internal int ArchetypeStructureUpdateCount { get; set; }

    internal Stack<int> FreeArchetypes = new();
    internal WorldData[] WorldData = new WorldData[32];

    internal List<int> AssignedWorldData = new();

    internal (Archetype_Info data, int version)[] Archetypes = new (Archetype_Info, int)[32];
    internal Dictionary<TypeSignature, int> SignatureToArchetypeIndex = new();

    internal TypeIdentifier TypeIds = new();
    internal TypeSignature BufferSignature; // just a scratch signature so that I'm not making new ones all the time

    /// <summary>
    /// Handles all structural changes to the ecs world
    /// </summary>
    internal StructureEventHandler StructureEvents;

    private bool _isDisposed = false;

    /// <summary>
    /// Name of the world
    /// </summary>
    public string Name { get; private set; }

    public int EntityCount { get; internal set; }

    public World(string name = null)
    {        
        lock (_lockObject)
        {
            WorldId = _worldIds.Next();
            if (WorldId >= All.Length) Array.Resize(ref All, All.Length * 2);

            All[WorldId] = this;
        }

        Name = name ?? $"World_{WorldId}";
        BufferSignature = new(TypeIds);

        // this is just to prevent default archetype from being valid
        Archetypes[0].version++;

        Entities = new Entity_Info[1024];
        FreeIds = new Queue<int>(128);

        // this is just to prevent default entity from being valid
        Entities[0].version++;

        StructureEvents = new StructureEventHandler(WorldId);
    }        

    /// <summary>
    /// Returns a copy of all the archetypes in the current world
    /// </summary>
    public Archetype[] GetArchetypes()
    {
        var archs = new Archetype[ArchetypeCount];
        int count = 0;
        foreach (var (data, version) in Archetypes)
        {
            if (data != null) archs[count++] = data.Archetype;
        }
            
        return archs;
    }

    /// <summary>
    /// Returns a copy of all the entities in the the current world
    /// </summary>
    public Entity[] GetEntities()
    {
        Entity[] entities = new Entity[EntityCount];
        int count = 0;
        foreach (var (data, version) in Archetypes)
        {
            if (data != null)
            {
                for (int e = 0; e < data.EntityCount; ++e) entities[count++] = data.Entities[e];
            }  
        }
        return entities;
    }

    /// <summary>
    /// Destroys the world along with all it's archetypes and entities
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        foreach (var archetype in Archetypes)   // delete all entities first
        {
            var arche_info = archetype.data;
            if (arche_info == null) continue;

            for (int i = 0; i < arche_info.EntityCount; ++i)
            {
                var index = arche_info.Entities[i].Index;
                ref var info = ref Entities[index];
                info.version++;
                info.ArchInfo = default;
                FreeIds.Enqueue(index);
            }
        }

        foreach (var archetype in Archetypes) // then do all their callbacks
        {
            var arche_info = archetype.data;
            if (arche_info == null) continue;
            for (int i = 0; i < arche_info.ComponentCount; ++i)
            {
                var pool = arche_info.ComponentBuffers[i];
                var world_data = GetWorldData(pool.type_id);
                if (world_data.has_remove_callback)
                {
                    world_data.InvokeRemoveCallbackAll(arche_info.Entities, pool.buffer.array, arche_info.EntityCount);
                }
            }
        }

        lock (_lockObject)
        {
            All[WorldId] = null;
            _worldIds.Release(WorldId);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// [structural]
    /// Creates an entity in this world
    /// </summary>
    public Entity CreateEntity()
    {
        return StructureEvents.CreateEntity(GetArchetypeData(BufferSignature.Clear()));
    }

    /// <summary>
    /// Creates a query that operates on this world
    /// </summary>
    public Query CreateQuery() => new(this);

    /// <summary>
    /// Tries to get the archetype that matches the supplied TypeSignature.
    /// Returns false if the world is destroyed or null
    /// </summary>
    internal bool TryGetArchetype(out Archetype archetype, TypeSignature signature)
    {
        archetype = GetArchetypeData(signature).Archetype;
        return true;
    }

    /// <summary>
    /// Tries to get an archetype that has the supplied types.
    /// Returns false if the world is destroyed or null
    /// </summary>
    public bool TryGetArchetype(out Archetype archetype, params Type[] types) => TryGetArchetype(out archetype, new TypeSignature(TypeIds, types));

    /// <summary>
    /// Tries to get an archetype that has the supplied types.
    /// Returns false if the world is destroyed or null
    /// </summary>
    public bool TryGetArchetype(out Archetype archetype, IEnumerable<Type> types) => TryGetArchetype(out archetype, new TypeSignature(TypeIds, types));


    internal Archetype_Info GetArchetypeData(TypeSignature signature)
    {
        if (!SignatureToArchetypeIndex.TryGetValue(signature, out var index))
        {
            if (FreeArchetypes.Count > 0)
            {
                index = FreeArchetypes.Pop();
                ArchetypeStructureUpdateCount++;
            }
            else
            {
                if (ArchetypeTerminatingIndex == Archetypes.Length) Array.Resize(ref Archetypes, ArchetypeTerminatingIndex * 2);
                index = ArchetypeTerminatingIndex;
                ArchetypeTerminatingIndex++;
            }
            var sig = new TypeSignature(TypeIds, signature);
            SignatureToArchetypeIndex[sig] = index;
            Archetypes[index].data = new Archetype_Info(WorldId, sig, index, Archetypes[index].version);
        }
        return Archetypes[index].data;
    }

    /// <summary>
    /// WorldData is data unique to this world
    /// Set's the world's data to value.
    /// </summary>
    public World SetData<WorldData>(WorldData world_data)
    {
        var stored = GetWorldData<WorldData>();
        stored.assigned_data = true;
        stored.data = world_data;
        return this;
    }

    /// <summary>
    /// WorldData is data unique to this world
    /// Get's a reference to the data which can be assigned.
    /// Throws an exception if the world is destroyed or null
    /// </summary>
    public ref WorldData GetData<WorldData>()
    {
        var stored = GetWorldData<WorldData>();
        stored.assigned_data = true;
        return ref GetWorldData<WorldData>().data;
    }

    internal WorldData<T> GetWorldData<T>()
    {
        int type_id = TypeIds.Get<T>();
        if (type_id >= WorldData.Length)
        {
            var size = WorldData.Length;
            while (size <= type_id) size *= 2;
            Array.Resize(ref WorldData, size);
        }
        if (WorldData[type_id] == null)
            WorldData[type_id] = new WorldData<T>();
        return (WorldData<T>)WorldData[type_id];
    }

    internal WorldData GetWorldData(int type_id)
    {
        if (type_id >= WorldData.Length)
        {
            var size = WorldData.Length;
            while (size <= type_id) size *= 2;
            Array.Resize(ref WorldData, size);
        }
        if (WorldData[type_id] == null)
        {
            var type = TypeIds.Get(type_id);
            WorldData[type_id] = (WorldData)Activator.CreateInstance(typeof(WorldData<>).MakeGenericType(type));
        }
        return WorldData[type_id];
    }

    /// <summary>
    /// Returns a copy of all the world data currently assigned in the world
    /// </summary>
    public object[] GetAllWorldData()
    {
        List<object> all = new();
        foreach (var stored in WorldData)
        {
            if (stored != null && stored.assigned_data) all.Add(stored.GetData());
        }
        return all.ToArray();
    }

    /// <summary>
    /// Retuns a copy of all the Types of world data currently assigned in the world
    /// </summary>
    public Type[] GetAllWorldDataTypes()
    {
        List<Type> all = new();
        foreach (var stored in WorldData)
        {
            if (stored != null && stored.assigned_data) all.Add(stored.data_type);
        }
        return all.ToArray();
    }

    /// <summary>
    /// Adds a callback to be invoked whenever an entity sets a component of type
    /// </summary>
    /// <param name="callback">callback to invoke</param>
    /// <param name="register">set true to add callback, false to remove the callback</param>
    public World OnSet<Component>(SetComponentEvent<Component> callback, bool register = true)
    {
        var data = GetWorldData<Component>();
        if (register) data.set_callback += callback;
        else data.set_callback -= callback;
        data.has_set_callback = data.set_callback != null;
        return this;
    }

    /// <summary>
    /// Adds a callback to be invoked whenever an entity sets a component of type
    /// </summary>
    /// <param name="callback">callback to invoke</param>
    /// <param name="register">set true to add callback, false to remove the callback</param>
    public World OnSet<Component>(SetComponentEventRefOnly<Component> callback, bool register = true)
    {
        var data = GetWorldData<Component>();
        if (register)
        {
            if (data.set_ref_callback == null)
                data.set_callback += data.CallSetRefCallback;
            data.set_ref_callback += callback;
        }
        else
        {
            data.set_ref_callback -= callback;
            if (data.set_ref_callback == null)
                data.set_callback -= data.CallSetRefCallback;
        }

        data.has_set_callback = data.set_callback != null;
        return this;
    }

    /// <summary>
    /// Adds a callback to be invoked whenever an entity sets a component of type
    /// </summary>
    /// <param name="callback">callback to invoke</param>
    /// <param name="register">set true to add callback, false to remove the callback</param>
    public World OnSet<Component>(SetComponentEventCompOnly<Component> callback, bool register = true)
    {
        var data = GetWorldData<Component>();
        if (register)
        {
            if (data.set_comp_callback == null)
                data.set_callback += data.CallSetCompCallback;
            data.set_comp_callback += callback;
        }
        else
        {
            data.set_comp_callback -= callback;
            if (data.set_comp_callback == null)
                data.set_callback -= data.CallSetCompCallback;
        }

        data.has_set_callback = data.set_callback != null;
        return this;
    }

    /// <summary>
    /// Adds a callback to be invoked whenever an entity removes a component of type
    /// </summary>
    /// <param name="callback">callback to invoke</param>
    /// <param name="register">set true to add callback, false to remove the callback</param>
    public World OnRemove<Component>(RemoveComponentEvent<Component> callback, bool register = true)
    {
        var data = GetWorldData<Component>();
        if (register) data.remove_callback += callback;
        else data.remove_callback -= callback;
        data.has_remove_callback = data.remove_callback != null;
        return this;
    }

    /// <summary>
    /// Adds a callback to be invoked whenever an entity removes a component of type
    /// </summary>
    /// <param name="callback">callback to invoke</param>
    /// <param name="register">set true to add callback, false to remove the callback</param>
    public World OnRemove<Component>(RemoveComponentEventCompOnly<Component> callback, bool register = true)
    {
        var data = GetWorldData<Component>();
        if (register)
        {
            if (data.remove_comp_callback == null)
                data.remove_callback += data.CallRemoveCompCallback;
            data.remove_comp_callback += callback;
        }
        else
        {
            data.remove_comp_callback -= callback;
            if (data.remove_comp_callback == null)
                data.remove_callback -= data.CallRemoveCompCallback;
        }
        data.has_remove_callback = data.remove_callback != null;
        return this;
    }

    /// <summary>
    /// [structural]
    /// Resizes all archetype's backing arrays to the minimum power of 2 needed to store their entities
    /// </summary>
    public void ResizeBackingArrays()
    {
        foreach (var archetype in GetArchetypes())
            archetype.ResizeBackingArrays();
    }

    /// <summary>
    /// [structural]
    /// Destroys all archetypes with 0 entities
    /// </summary>
    public void DestroyEmptyArchetypes()
    {
        foreach (var archetype in GetArchetypes())
        {
            if (archetype.EntityCount == 0) archetype.Destroy();
        }
    }

    /// <summary>
    /// When enabled all structural methods will be cached like they are when iterating a query.
    /// They will be applied once you disable caching.
    /// Use to prevent iterator invalidation when manually iterating over entity or component buffers.
    /// </summary>
    public void CacheStructuralEvents(bool enable)
    {
        CacheStructuralChanges = enable;
    }

    /// <summary>
    /// Returns true if the world is currently caching structural changes
    /// </summary>
    public bool IsCachingStructuralEvents()
    {
        return StructureEvents.EnqueueEvents > 0;
    }

    public override string ToString() => $"{Name} ({ArchetypeCount}a {EntityCount}e)";


    IEnumerator<Archetype> IEnumerable<Archetype>.GetEnumerator()
    {
        for (int i = 0; i < ArchetypeTerminatingIndex; ++i)
        {
            var arche_info = Archetypes[i].data;
            if (arche_info != null)
                yield return arche_info.Archetype;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        for (int i = 0; i < ArchetypeTerminatingIndex; ++i)
        {
            var arche_info = Archetypes[i].data;
            if (arche_info != null)
                yield return arche_info.Archetype;
        }
    }
}

internal struct Entity_Info
{
    public Archetype_Info ArchInfo;
    public int version;
    public int arch_index;
}

internal abstract class WorldData
{
    public bool has_remove_callback, has_set_callback, assigned_data;
    public abstract void Set(in Entity entity, in StructureEventHandler handler);
    public abstract void Set(in Entity entity, object component, in StructureEventHandler handler);
    public abstract void Remove(in Entity entity, in StructureEventHandler handler);
    public abstract void InvokeRemoveCallbackAll(in Entity[] entities, in object buffer, int count);
    public abstract void InvokeRemoveCallback(in Entity entity, in object component);

    public abstract object GetData();
    public abstract Type data_type { get; }
}

internal sealed class WorldData<T> : WorldData
{
    public T data;
    public SetComponentEvent<T> set_callback;
    public SetComponentEventRefOnly<T> set_ref_callback;
    public SetComponentEventCompOnly<T> set_comp_callback;
    public RemoveComponentEvent<T> remove_callback;
    public RemoveComponentEventCompOnly<T> remove_comp_callback;

    public Queue<T> set_queue = new Queue<T>();

    public override void Set(in Entity entity, in StructureEventHandler handler)
    {
        handler.Set(entity, set_queue.Dequeue());
    }

    public override void Set(in Entity entity, object component, in StructureEventHandler handler)
    {
        handler.Set(entity, (T)component);
    }

    public override void Remove(in Entity entity, in StructureEventHandler handler) => handler.Remove<T>(entity);
    public override void InvokeRemoveCallbackAll(in Entity[] entities, in object buffer, int count)
    {
        T[] array = (T[])buffer;
        for (int i = 0; i < count; ++i)
            remove_callback?.Invoke(entities[i], array[i]);
    }

    public override void InvokeRemoveCallback(in Entity entity, in object comp)
        => remove_callback?.Invoke(entity, (T)comp);


    public void CallSetRefCallback(Entity entity, T old_comp, ref T new_comp)
    {
        set_ref_callback.Invoke(entity, ref new_comp);
    }

    public void CallSetCompCallback(Entity entity, T old_comp, ref T new_comp)
    {
        set_comp_callback.Invoke(ref new_comp);
    }

    public void CallRemoveCompCallback(Entity entity, T component)
        => remove_comp_callback.Invoke(component);

    public override object GetData() => data;

    public override Type data_type => typeof(T);
}

internal struct StructureEventHandler
{
    private readonly int _worldId;
    private readonly Queue<EventData> _events = new();

    private int _cacheEvents = 0;
    public int EnqueueEvents
    {
        get => _cacheEvents;
        set
        {
            _cacheEvents = value;
            ExecuteEventPlayback();
        }
    }

    public StructureEventHandler(int worldId)
    {
        _worldId = worldId;
    }

    struct EventData
    {
        public EventType type;
        public Entity entity;
        public int type_id;
        public Archetype archetype;
    }

    enum EventType
    {
        CreateEntity,
        DestroyEntity,
        SetComponent,
        RemoveComponent,
        DestroyArchetype,
        ResizeBackingArrays,
    }

    public void ExecuteEventPlayback()
    {
        while (_cacheEvents == 0 && _events.Count > 0)
        {
            var e = _events.Dequeue();
            switch (e.type)
            {
                case EventType.CreateEntity:
                    {
                        ref var arch_data = ref World.All[_worldId].Archetypes[e.archetype.Index];
                        if (arch_data.version == e.archetype.Version)
                            _setUpEntity(e.entity, World.All[_worldId].Archetypes[e.archetype.Index].data);
                        else
                        {
                            World.All[_worldId].FreeIds.Enqueue(e.entity.Index);
                        }
                    }
                    break;

                case EventType.SetComponent:
                    World.All[_worldId].GetWorldData(e.type_id).Set(e.entity, this);
                    break;

                case EventType.RemoveComponent:
                    World.All[_worldId].GetWorldData(e.type_id).Remove(e.entity, this);
                    break;

                case EventType.DestroyEntity:
                    Destroy(e.entity);
                    break;

                case EventType.DestroyArchetype:
                    DestroyArchetype(e.archetype);
                    break;

                case EventType.ResizeBackingArrays:
                    ResizeBackingArrays(e.archetype);
                    break;
            }
        }
    }

    public Entity CreateEntity(Archetype_Info archetype_data)
    {
        int index;
        if (World.All[_worldId].FreeIds.Count > 0)
            index = World.All[_worldId].FreeIds.Dequeue();
        else
        {
            index = World.All[_worldId].LastEntityId;
            if (index == World.All[_worldId].Entities.Length) Array.Resize(ref World.All[_worldId].Entities, index * 2);
            World.All[_worldId].LastEntityId++;
        }
        var version = World.All[_worldId].Entities[index].version;
        var entity = new Entity(index, version, World.All[_worldId].WorldId);

        if (_cacheEvents > 0)
        {
            World.All[_worldId].Entities[index].version++;
            _events.Enqueue(new EventData { type = EventType.CreateEntity, entity = entity, archetype = archetype_data.Archetype });
        }
        else
        {
            _setUpEntity(entity, archetype_data);
        }

        return entity;
    }

    private void _setUpEntity(Entity entity, Archetype_Info archetype_data)
    {
        ref var entity_data = ref World.All[_worldId].Entities[entity.Index];
        entity_data.version = entity.Version;
        entity_data.ArchInfo = archetype_data;
        var arch_index = entity_data.arch_index = archetype_data.EntityCount;
        archetype_data.EntityCount++;
        World.All[archetype_data.WorldId].EntityCount++;
        archetype_data.EnsureCapacity(arch_index);
        archetype_data.Entities[arch_index] = entity;
    }

    public void Set<Component>(in Entity entity, in Component component)
    {
        var world_data = World.All[_worldId].GetWorldData<Component>();
        if (_cacheEvents > 0)
        {
            world_data.set_queue.Enqueue(component);
            _events.Enqueue(new EventData { type = EventType.SetComponent, entity = entity, type_id = World.All[_worldId].TypeIds.Get<Component>() });
            return;
        }

        ref var entity_info = ref World.All[_worldId].Entities[entity.Index];
        if (entity_info.version == entity.Version)
        {
            if (entity_info.ArchInfo.TryGetArray<Component>(out var buffer))
            {
                int index = entity_info.arch_index;
                Component old = buffer[index];
                buffer[index] = component;
                world_data.set_callback?.Invoke(entity, old, ref buffer[index]);
            }
            else
            {
                var old_index = entity_info.arch_index;
                var archetype = entity_info.ArchInfo;
                var last_index = --archetype.EntityCount;
                var last = archetype.Entities[old_index] = archetype.Entities[last_index];
                World.All[_worldId].Entities[last.Index].arch_index = old_index; // reassign moved entity to to index

                // adding entity to target archetype
                var target_archetype = entity_info.ArchInfo = World.All[_worldId].GetArchetypeData(World.All[_worldId].BufferSignature.Copy(archetype.Signature).Add<Component>());
                var target_index = entity_info.arch_index = target_archetype.EntityCount;
                target_archetype.EnsureCapacity(target_index);
                target_archetype.EntityCount++;

                // moving components over
                target_archetype.Entities[target_index] = entity;
                for (int i = 0; i < archetype.ComponentCount; ++i)
                    archetype.ComponentBuffers[i].buffer.Move(old_index, last_index, target_archetype, target_index);

                // setting the added component and calling the callback event
                if (target_archetype.TryGetArray<Component>(out var target_buffer))
                {
                    target_buffer[target_index] = component;
                    world_data.set_callback?.Invoke(entity, default, ref target_buffer[target_index]);
                }
                else
                    throw new Exception("Frame Work Bug");
            }
        }
    }

    public void Remove<Component>(in Entity entity)
    {
        int type_id = World.All[_worldId].TypeIds.Get<Component>();
        if (_cacheEvents > 0)
        {
            _events.Enqueue(new EventData { type = EventType.RemoveComponent, entity = entity, type_id = type_id });
        }
        else
        {
            ref var entity_info = ref World.All[_worldId].Entities[entity.Index];
            if (entity.Version == entity_info.version)
            {
                var old_arch = entity_info.ArchInfo;
                if (old_arch.TryGetArray<Component>(out var old_buffer))  // if archetype already has component, just set and fire event
                {
                    var old_index = entity_info.arch_index;

                    var target_arch = World.All[_worldId].GetArchetypeData(World.All[_worldId].BufferSignature.Copy(old_arch.Signature).Remove(type_id));
                    var target_index = target_arch.EntityCount;
                    target_arch.EntityCount++;
                    target_arch.EnsureCapacity(target_index);

                    old_arch.EntityCount--;
                    var last_index = old_arch.EntityCount;
                    var last = old_arch.Entities[old_index] = old_arch.Entities[last_index];
                    World.All[_worldId].Entities[last.Index].arch_index = old_index;

                    entity_info.arch_index = target_index;
                    entity_info.ArchInfo = target_arch;

                    target_arch.Entities[target_index] = entity;
                    var removed = old_buffer[old_index];
                    for (int i = 0; i < old_arch.ComponentCount; ++i)
                        old_arch.ComponentBuffers[i].buffer.Move(old_index, last_index, target_arch, target_index);
                    World.All[_worldId].GetWorldData<Component>().remove_callback?.Invoke(entity, removed);
                }
            }
        }
    }

    public void Destroy(Entity entity)
    {
        if (_cacheEvents > 0)
            _events.Enqueue(new EventData { type = EventType.DestroyEntity, entity = entity });
        else
        {
            ref var entity_info = ref World.All[_worldId].Entities[entity.Index];
            if (entity_info.version == entity.Version)
            {
                entity_info.version++;
                var old_arch = entity_info.ArchInfo;
                var old_index = entity_info.arch_index;
                --old_arch.EntityCount;
                --World.All[_worldId].EntityCount;
                var last_index = old_arch.EntityCount;
                var last = old_arch.Entities[old_index] = old_arch.Entities[last_index];    // swap 
                World.All[_worldId].Entities[last.Index].arch_index = old_index;

                (WorldData callback, object value)[] removed =          // this causes allocations
                    new (WorldData, object)[old_arch.ComponentCount];  // but other means are quite convuluted
                int length = 0;

                for (int i = 0; i < old_arch.ComponentCount; ++i)
                {
                    var pool = old_arch.ComponentBuffers[i];
                    var callback = World.All[_worldId].GetWorldData(pool.type_id);
                    if (callback.has_remove_callback)
                    {
                        removed[length] = (callback, pool.buffer.array[entity_info.arch_index]); // this causes boxing :(
                        length++;
                    }
                    pool.buffer.Remove(old_index, last_index);
                }
                entity_info.version++;
                entity_info.ArchInfo = default;
                World.All[_worldId].FreeIds.Enqueue(entity.Index);

                for (int i = 0; i < length; ++i)
                    removed[i].callback.InvokeRemoveCallback(entity, removed[i].value);
            }
        }
    }

    public void DestroyArchetype(Archetype archetype)
    {
        if (_cacheEvents > 0)
        {
            _events.Enqueue(new EventData { type = EventType.DestroyArchetype, archetype = archetype });
        }
        else
        {
            if (archetype.TryGetArchetypeInfo(out var arch_info))
            {
                World.All[_worldId].EntityCount -= arch_info.EntityCount;
                World.All[_worldId].SignatureToArchetypeIndex.Remove(arch_info.Signature);   // update archetype references
                World.All[_worldId].Archetypes[archetype.Index].version++;
                World.All[_worldId].Archetypes[archetype.Index].data = default;
                World.All[_worldId].FreeArchetypes.Push(archetype.Index);
                World.All[_worldId].ArchetypeStructureUpdateCount++;

                for (int i = 0; i < arch_info.EntityCount; ++i)    // remove entities from world
                {
                    var entity = arch_info.Entities[i];
                    ref var info = ref World.All[_worldId].Entities[entity.Index];
                    info.version++;
                    info.ArchInfo = default;
                    World.All[_worldId].FreeIds.Enqueue(entity.Index);
                }

                for (int i = 0; i < arch_info.ComponentCount; ++i) // invoke callbacks
                {
                    var pool = arch_info.ComponentBuffers[i];
                    var callback = World.All[_worldId].GetWorldData(pool.type_id);
                    if (callback.has_remove_callback)
                    {
                        callback.InvokeRemoveCallbackAll(arch_info.Entities, pool.buffer.array, arch_info.EntityCount);
                    }
                }
            }
        }
    }

    public void ResizeBackingArrays(Archetype archetype)
    {
        if (_cacheEvents > 0)
            _events.Enqueue(new EventData { type = EventType.ResizeBackingArrays, archetype = archetype });
        else
            if (archetype.TryGetArchetypeInfo(out var info))
            info.ResizeBackingArrays();
    }
}