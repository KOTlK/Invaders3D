using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static Assertions;

[System.Serializable]
public struct BulletConfig {
    public LayerMask LayerMask;
    public float     Speed;
    public float     Radius;
    public float     TimeToLive;
}

[System.Serializable]
public struct Bullet {
    public float3   Position;
    public float3   Direction;
    public float    SpawnTime;
    public float    Orientation;
}

[NativeContainerSupportsMinMaxWriteRestriction]
[NativeContainer]
public unsafe struct BulletsCollisions : IDisposable {
    [NativeContainer]
    [NativeContainerIsAtomicWriteOnly]
    public struct ParallelWriter {
        [NativeDisableUnsafePtrRestriction]
        public BulletsCollisions *Origin;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();

            internal  ParallelWriter(BulletsCollisions *origin, ref AtomicSafetyHandle safety)
            {
                Origin = origin;
                m_Safety = safety;
                CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref m_Safety, ref s_staticSafetyId.Data);
            }
#else
            internal unsafe ParallelWriter(BulletsCollisions *origin)
            {
                Origin = origin;
            }
#endif

        public void Add(SpherecastCommand command, int entity) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            var idx = Interlocked.Increment(ref Origin->m_Length) - 1;
            Origin->Casts[idx]    = command;
            Origin->Entities[idx] = entity;
        }
    }

    public NativeArray<SpherecastCommand> Casts;
    public NativeArray<int>               Entities;

    internal int                          m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal int                          m_MinIndex;
    internal int                          m_MaxIndex;
    internal AtomicSafetyHandle           m_Safety;

    // Statically register this type with the safety system, using a name derived from the type itself
    internal static readonly int          s_staticSafetyId = 
                                              AtomicSafetyHandle.NewStaticSafetyId<BulletsCollisions>();
#endif


    public BulletsCollisions(int capacity, Allocator allocator) {
        Casts       = new NativeArray<SpherecastCommand>(capacity, allocator);
        Entities    = new NativeArray<int>(capacity, allocator);
        m_Length    = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        m_MinIndex    = 0;
        m_MaxIndex    = capacity - 1;
        m_Safety      = AtomicSafetyHandle.Create();
        // Set the safety ID on the AtomicSafetyHandle so that error messages describe this container type properly.
        AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_staticSafetyId);

        // Automatically bump the secondary version any time this container is scheduled for writing in a job
        AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
    }

    public int Count => m_Length;

    //Single thread version
    public void Add(SpherecastCommand command, int entity) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif  
        Casts[m_Length]       = command;
        Entities[m_Length++]  = entity;
    }

    public void Dispose() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
        AtomicSafetyHandle.Release(m_Safety);
#endif
        Casts.Dispose();
        Entities.Dispose();
    }

    //Multithreaded version
    public ParallelWriter AsParallelWriter(BulletsCollisions *origin) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ParallelWriter(origin, ref m_Safety);
#else
            return new ParallelWriter(origin);
#endif                    
    }
}

[BurstCompile]
public struct BulletsMovementJob : IJobParallelFor {
                public NativeArray<Bullet>                          Bullets;
    [ReadOnly]  public NativeArray<BulletConfig>                    Configs;
    [WriteOnly] public NativeList<int>.ParallelWriter               RemoveQueue; 
    [WriteOnly] public BulletsCollisions.ParallelWriter             Collisions;
                public float                                        Dt;
                public float                                        Time;

    public void Execute(int i) {
        if(Bullets[i].SpawnTime + Configs[i].TimeToLive <= Time) {
            RemoveQueue.AddNoResize(i);
        } else {
            var distance = Configs[i].Speed * Dt;
            Collisions.Add(new SpherecastCommand(Bullets[i].Position, 
                                                 Configs[i].Radius,
                                                 Bullets[i].Direction,
                                                 new QueryParameters(Configs[i].LayerMask,
                                                                     true,
                                                                     QueryTriggerInteraction.Ignore,
                                                                     true),
                                                 distance), i);
            Bullets[i] = new Bullet {
                Direction   = Bullets[i].Direction,
                Position    = Bullets[i].Position + Bullets[i].Direction * distance,
                SpawnTime   = Bullets[i].SpawnTime,
                Orientation = Bullets[i].Orientation
            };
        }
    }
}

[BurstCompile]
public struct BulletsCollisionResponseJob : IJobParallelFor {
    [ReadOnly]  public NativeArray<RaycastHit>        Hits;
    [ReadOnly]  public NativeArray<int>               CastEntities;
    [WriteOnly] public NativeList<int>.ParallelWriter RemoveQueue;

    public void Execute(int i) {
        if(Hits[i].colliderInstanceID != 0) {
            RemoveQueue.AddNoResize(CastEntities[i]);
        }
    }
}

public struct SyncBulletsJob : IJobParallelForTransform
{
    [ReadOnly] public NativeList<Bullet> Bullets;

    public void Execute(int index, TransformAccess transform) {
        transform.SetPositionAndRotation(Bullets[index].Position, 
                                         Quaternion.AngleAxis(Bullets[index].Orientation, Vector3.up));
    }
}

public class Bullets : MonoBehaviour {
    public NativeList<Bullet>           Entities;
    public NativeList<BulletConfig>     Configs;
    public Transform[]                  Transforms = new Transform[128];
    public TransformAccessArray         TransformAccess;
    public NativeList<int>              RemoveQueue;

    private static Dictionary<ResourceLink, Stack<Transform>>   _pools;
    private static ResourceLink[]                               _prefabs;
    private static ResourceSystem                               _resources;

    public void Init() {
        Entities            = new NativeList<Bullet>(4096, Allocator.Persistent);
        Configs             = new NativeList<BulletConfig>(4096, Allocator.Persistent);
        RemoveQueue         = new NativeList<int>(2048, Allocator.Persistent);
        TransformAccess     = new TransformAccessArray(128);
        _resources          = Singleton<ResourceSystem>.Instance;
        _pools              = new();
        _prefabs            = new ResourceLink[128];
    }

    private void OnDestroy() {
        Entities.Dispose();
        Configs.Dispose();
        TransformAccess.Dispose();
        RemoveQueue.Dispose();
    }

    public void Create(Vector3 position, 
                       Vector3 direction, 
                       float orientation, 
                       BulletConfig config, 
                       ResourceLink prefab) {
        Assert(Entities.Length == Configs.Length);
        var index = Entities.Length;
        Entities.Add(new Bullet {
            Position    = position,
            Direction   = direction,
            SpawnTime   = Clock.Time,
            Orientation = orientation
        });
        Configs.Add(config);

        if(Entities.Length >= Transforms.Length) {
            Array.Resize(ref Transforms, Entities.Length << 1);
            Array.Resize(ref _prefabs, Entities.Length << 1);
        }

        Transforms[index]   = Get(prefab);
        _prefabs[index]     = prefab;
        TransformAccess.Add(Transforms[index]);
    }
    
    public unsafe void UpdateBehavior() {
        var dt          = Clock.Delta;
        var time        = Clock.Time;
        var physics     = new BulletsCollisions(Entities.Length, Allocator.TempJob);
        var physicsPtr  = &physics;
        var bulletsJob  = new BulletsMovementJob {
            Bullets         = Entities.AsArray(),
            Configs         = Configs.AsArray(),
            RemoveQueue     = RemoveQueue.AsParallelWriter(),
            Collisions      = physics.AsParallelWriter(physicsPtr),
            Dt              = dt,
            Time            = time
        };

        var bulletsHandle = bulletsJob.Schedule(Entities.Length, 32);

        bulletsHandle.Complete();

        var physicsResults  = new NativeArray<RaycastHit>(Entities.Length, Allocator.TempJob);
        var physicsHandle   = SpherecastCommand.ScheduleBatch(physics.Casts, physicsResults, 1, 1);

        physicsHandle.Complete();

        var physicsResponseJob = new BulletsCollisionResponseJob {
            Hits         = physicsResults,
            CastEntities = physics.Entities,
            RemoveQueue  = RemoveQueue.AsParallelWriter()
        };

        var responseHandle = physicsResponseJob.Schedule(physics.Count, 32);

        responseHandle.Complete();

        if(RemoveQueue.Length > 0) {
            RemoveQueue.Sort(new DescendingComparer());
            var previouslyRemoved = RemoveQueue[0];
            KillImmediate(0);

            for(var i = 1; i < RemoveQueue.Length; ++i) {
                if(previouslyRemoved != RemoveQueue[i]) {
                    KillImmediate(i);
                }
            }

            RemoveQueue.Clear();
        }

        var syncJob = new SyncBulletsJob {
            Bullets = Entities
        };

        var handle = syncJob.Schedule(TransformAccess);

        handle.Complete();

        physics.Dispose();
        physicsResults.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KillImmediate(int i) {
        Entities.RemoveAtSwapBack(RemoveQueue[i]);
        Configs.RemoveAtSwapBack(RemoveQueue[i]);
        Release(Transforms[RemoveQueue[i]], RemoveQueue[i]);
        Transforms[RemoveQueue[i]] = Transforms[Entities.Length];
        Transforms[Entities.Length] = null;
        TransformAccess.RemoveAtSwapBack(RemoveQueue[i]);    
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Transform Get(ResourceLink prefab) {
        if(_pools.ContainsKey(prefab) && _pools[prefab].Count > 0) {
            var a = _pools[prefab].Pop();
            a.gameObject.SetActive(true);
            return a;
        } else {
            return Instantiate(_resources.Load<Transform>(prefab), transform);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Release(Transform t, int index) {
        if(_pools.ContainsKey(_prefabs[index])) {
            _pools[_prefabs[index]].Push(t);
        } else {
            _pools.Add(_prefabs[index], new Stack<Transform>(128));
            _pools[_prefabs[index]].Push(t);
        }
        
        t.gameObject.SetActive(false);
    }
    
    private struct DescendingComparer : IComparer<int> {
        public int Compare(int x, int y) {
            var a = y - x;
            return a == 0 ? 0 : a / math.abs(a);
        }
    }
}