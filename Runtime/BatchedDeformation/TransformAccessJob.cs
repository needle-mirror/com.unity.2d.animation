using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.Jobs;
using UnityEngine.Pool;

namespace UnityEngine.U2D.Animation
{
    // This class is used to manage the transforms and their access for jobs.
    // The takes a list of transforms and creates a TransformAccessArray for them.
    // The TransformAccessArray is used to schedule jobs that require access to the transforms.
    // It can run 2 jobs internally, one to return localToWorld matrices and one to return worldToLocal matrices.
    internal class TransformAccessJob
    {
        public struct TransformData
        {
            public int transformIndex;
            public int refCount;

            public TransformData(int index)
            {
                transformIndex = index;
                refCount = 1;
            }
        }
        // This is an array of transforms that are passed to the job.
        // It must be an array because the TransformAccessArray requires an array of transforms.
        Transform[] m_Transform;
        TransformAccessArray m_TransformAccessArray;
        NativeHashMap<EntityId, TransformData> m_TransformData;
        NativeArray<float4x4> m_TransformMatrix;
        // Per-transform k_Transform* state written by the local-to-world job. Jobs skip destroyed transforms, so an
        // entry left at k_TransformDestroyed marks one destroyed since the last sweep.
        NativeArray<byte> m_TransformState;
        bool m_Dirty;
        JobHandle m_JobHandle;

        internal const byte k_TransformDestroyed = 0;
        internal const byte k_TransformUnchanged = 1;
        internal const byte k_TransformChanged = 2;

        public TransformAccessJob()
        {
            InitializeDataStructures();

            m_Dirty = false;
            m_JobHandle = default(JobHandle);
        }

        public void Destroy()
        {
            ClearDataStructures();
        }

        void InitializeDataStructures()
        {
            m_TransformMatrix = new NativeArray<float4x4>(1, Allocator.Persistent);
            m_TransformData = new NativeHashMap<EntityId, TransformData>(1, Allocator.Persistent);
            m_Transform = Array.Empty<Transform>();
        }

        void ClearDataStructures()
        {
            if (m_TransformMatrix.IsCreated)
                m_TransformMatrix.Dispose();
            if (m_TransformState.IsCreated)
                m_TransformState.Dispose();
            if (m_TransformAccessArray.isCreated)
                m_TransformAccessArray.Dispose();
            if (m_TransformData.IsCreated)
                m_TransformData.Dispose();
            m_Transform = null;
        }

        public void ResetCache()
        {
#if UNITY_INCLUDE_TESTS
            ResetCacheCallCount++;
#endif
            ClearDataStructures();
            InitializeDataStructures();
        }

#if UNITY_INCLUDE_TESTS
        [NoAutoStaticsCleanup]
        internal static int ResetCacheCallCount { get; private set; }
        internal static void ResetResetCacheCallCount() => ResetCacheCallCount = 0;
#endif

        public NativeHashMap<EntityId, TransformData> transformData => m_TransformData;

        // This array can hold localToWorld or worldToLocal matrices depending on the job that was scheduled.
        public NativeArray<float4x4> transformMatrix => m_TransformMatrix;

        public NativeArray<byte> transformState => m_TransformState;
#if UNITY_INCLUDE_TESTS
        internal TransformAccessArray transformAccessArray => m_TransformAccessArray;
#endif

        public void AddTransform(Transform t)
        {
            if (t == null || !m_TransformData.IsCreated)
                return;
            m_JobHandle.Complete();
            EntityId entityId = t.GetEntityId();
            if (m_TransformData.ContainsKey(entityId))
            {
                TransformData transformData = m_TransformData[entityId];
                transformData.refCount += 1;
                m_TransformData[entityId] = transformData;
            }
            else
            {
                m_TransformData.TryAdd(entityId, new TransformData(-1));
                ArrayAdd(ref m_Transform, t);
                m_Dirty = true;
            }

        }

        static void ArrayAdd<T>(ref T[] array, T item)
        {
            int arraySize = array.Length;
            Array.Resize(ref array, arraySize + 1);
            array[arraySize] = item;
        }

        // if removing multiple items, it is more efficient to just set them to null and then call CompactArray.
        static void ArrayRemoveAt<T>(ref T[] array, int index)
        {
            int length = array.Length;
            if (index >= length)
                throw new ArgumentOutOfRangeException(nameof(index));

            // Shift elements up
            for (int i = index; i < length - 1; ++i)
                array[i] = array[i + 1];

            // Resize array, chopping off the last element
            Array.Resize(ref array, length - 1);
        }

        // This method is used to remove real nulls from the array and resize it.
        // Returns true if the array size was reduced.
        static bool CompactArray<T>(ref T[] array)
        {
            // iterate over array and remove nulls
            int writeIndex = 0;
            for (int i = 0; i < array.Length; i++)
            {
                // we use 'is not null' to avoid removing destroyed transforms, which are handled elsewhere.
                if (array[i] is not null)
                {
                    if (writeIndex != i)
                        array[writeIndex] = array[i];
                    writeIndex++;
                }
            }

            // Resize the array to the new length
            bool resized = writeIndex < array.Length;
            if (resized)
                Array.Resize(ref array, writeIndex);

            return resized;
        }

        void UpdateTransformIndex()
        {
            if (!m_Dirty)
                return;
            m_Dirty = false;
            UnityEngine.Profiling.Profiler.BeginSample("UpdateTransformIndex");

            // Always recreate matrix array when transform array changes to ensure clean state
            if (m_TransformMatrix.IsCreated)
                m_TransformMatrix.Dispose();

            // Initialize with zero matrices. Note: Unity Transform.localToWorldMatrix can never be all zeros
            // due to homogeneous coordinate requirements (last row is always [0,0,0,1]), so zero initialization
            // ensures proper change detection.
            m_TransformMatrix = new NativeArray<float4x4>(m_Transform.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (!m_TransformAccessArray.isCreated)
                TransformAccessArray.Allocate(m_Transform.Length, -1, out m_TransformAccessArray);
            else if (m_TransformAccessArray.capacity != m_Transform.Length)
                m_TransformAccessArray.capacity = m_Transform.Length;

            m_TransformAccessArray.SetTransforms(m_Transform);

            for (int i = 0; i < m_Transform.Length; ++i)
            {
                if (m_Transform[i] != null)
                {
                    EntityId entityId = m_Transform[i].GetEntityId();
                    TransformData transformData = m_TransformData[entityId];
                    transformData.transformIndex = i;
                    m_TransformData[entityId] = transformData;
                }
            }

            UnityEngine.Profiling.Profiler.EndSample();
        }

        public JobHandle StartLocalToWorldAndChangeDetectionJob()
        {
            m_JobHandle.Complete();
            // Cleared every frame, not only when resized: the job skips destroyed transforms, so their entries must
            // start at k_TransformDestroyed instead of keeping last frame's state.
            NativeArrayHelpers.ResizeIfNeeded(ref m_TransformState, m_Transform.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            ClearTransformState();

            if (m_Transform.Length > 0)
            {
                UpdateTransformIndex();

                UnityEngine.Profiling.Profiler.BeginSample("StartLocalToWorldAndChangeDetectionJob");
                LocalToWorldAndChangeDetectionTransformAccessJob job = new LocalToWorldAndChangeDetectionTransformAccessJob()
                {
                    outMatrix = transformMatrix,
                    state = transformState,
                };
                m_JobHandle = job.ScheduleReadOnly(m_TransformAccessArray, 16);
                UnityEngine.Profiling.Profiler.EndSample();
                return m_JobHandle;
            }

            return default(JobHandle);
        }

        public JobHandle StartWorldToLocalJob()
        {
            if (m_Transform.Length > 0)
            {
                m_JobHandle.Complete();
                UpdateTransformIndex();
                UnityEngine.Profiling.Profiler.BeginSample("StartWorldToLocalJob");
                WorldToLocalTransformAccessJob job = new WorldToLocalTransformAccessJob()
                {
                    outMatrix = transformMatrix,
                };
                m_JobHandle = job.ScheduleReadOnly(m_TransformAccessArray, 16);
                UnityEngine.Profiling.Profiler.EndSample();
                return m_JobHandle;
            }

            return default(JobHandle);
        }

        unsafe void ClearTransformState()
        {
            if (m_TransformState.Length > 0)
                UnsafeUtility.MemClear(m_TransformState.GetUnsafePtr(), m_TransformState.Length);
        }

        // For the local-to-world job: sweeps when its last job skipped a destroyed transform, or before indices are
        // rebuilt so that a destroyed transform never keeps an index another transform has moved into.
        internal void RemoveDestroyedTransformsIfNeeded()
        {
            m_JobHandle.Complete();
            if (m_Dirty || HasDestroyedTransforms())
                RemoveTransformsIfNull();
        }

        internal bool HasDestroyedTransforms()
        {
            if (m_Transform.Length == 0)
                return false;
            if (!m_TransformState.IsCreated || m_TransformState.Length != m_Transform.Length)
                return true;
            return TransformAccessJobUtilities.AnyDestroyed(m_TransformState);
        }

        internal string GetDebugLog()
        {
            string log = "";
            log += "TransformData Count: " + m_TransformData.Count + "\n";
            log += "Transform Count: " + m_Transform.Length + "\n";
            foreach (Transform ss in m_Transform)
            {
                log += ss == null ? "null" : ss.name + " " + ss.GetEntityId();
                log += "\n";
                if (ss != null)
                {
                    log += "RefCount: " + m_TransformData[ss.GetEntityId()].refCount + "\n";
                }

                log += "\n";
            }

            return log;
        }

        // Remove any destroyed transforms from m_Transform and keep the index in sync
        internal int RemoveTransformsIfNull()
        {
            int count = 0;
            // process in reverse order to preserve array integrity on removal.
            for (int i = m_Transform.Length - 1; i >= 0; i--)
            {
                // Is this transform destroyed?
                if (!m_Transform[i])
                {
                    // in the Player this Remove is a no-op (GetEntityId() returns None after
                    // destruction); such leaked entries are purged below.
                    m_TransformData.Remove(m_Transform[i].GetEntityId());
                    // remove from transform array by assigning a real null.
                    m_Transform[i] = null;
                    count++;
                }
            }

            if (CompactArray(ref m_Transform))
                m_Dirty = true;

            if (m_TransformData.Count > m_Transform.Length)
                RemoveLeakedTransformData();

            return count;
        }

        // Entries whose id could not be resolved on removal would keep serving a stale index for
        // a dead transform; drop everything that no longer matches a live transform.
        void RemoveLeakedTransformData()
        {
            HashSet<EntityId> alive = HashSetPool<EntityId>.Get();
            foreach (Transform transform in m_Transform)
                alive.Add(transform.GetEntityId());

            NativeArray<EntityId> keys = m_TransformData.GetKeyArray(Allocator.Temp);
            foreach (EntityId key in keys)
            {
                if (!alive.Contains(key))
                    m_TransformData.Remove(key);
            }

            keys.Dispose();
            HashSetPool<EntityId>.Release(alive);
        }

        // Deformation manager calls this with a list of ids to remove
        // Note: the list passed in is also modified by this method.
        // Note: this method assumes the list is sorted.
        internal void RemoveTransformsByIds(List<EntityId> idsToRemove)
        {
            if (!m_TransformData.IsCreated)
                return;
            m_JobHandle.Complete();

            // catch the indexes to remove from m_Transform here
            List<int> indexesToRemove = ListPool<int>.Get();

            // Remove any ids that we do not know about
            // Reduce refcount on ids that we do know about.
            for (int i = idsToRemove.Count - 1; i >= 0; --i)
            {
                EntityId id = idsToRemove[i];
                // if we don't know about this id, remove it from the list then ignore
                if (!m_TransformData.ContainsKey(id))
                {
                    idsToRemove.Remove(id);
                    continue;
                }

                TransformData transformData = m_TransformData[id];
                // reduce refcount if it is > 1
                if (transformData.refCount > 1)
                {
                    transformData.refCount -= 1;
                    m_TransformData[id] = transformData;
                    idsToRemove.Remove(id);
                }
                else // refcount will become 0 so remove it from the index, and add it to the list of indexes to remove from m_Transform
                {
                    m_TransformData.Remove(id);
                    if (0 <= transformData.transformIndex)
                        indexesToRemove.Add(transformData.transformIndex);
                }
            }

            if (indexesToRemove.Count > 0)
            {
                // remove the transforms from the transform array in reverse order
                // they appear to already be sorted however not sure we can assume that is always the case
                // so we sort them here to be safe
                indexesToRemove.Sort();
                for (int i = indexesToRemove.Count - 1; i >= 0; i--)
                {
                    int index = indexesToRemove[i];
                    // previous version of this code performed a linear search to find the index to remove
                    // by matching GetEntityId() of the transform.
                    // it did not remove the transform from the transform array if there was no match
                    // we do the same logic here by ignoring the index if it is out of bounds <gulp>
                    if (index < m_Transform.Length)
                        m_Transform[index] = null;
                }

                if (CompactArray(ref m_Transform))
                    m_Dirty = true;
            }
            ListPool<int>.Release(indexesToRemove);
        }

        internal void RemoveTransformById(EntityId transformId)
        {
            if (!m_TransformData.IsCreated)
                return;
            m_JobHandle.Complete();
            if (m_TransformData.TryGetValue(transformId, out TransformData transformData))
            {
                if (transformData.refCount == 1)
                {
                    m_TransformData.Remove(transformId);
                    int index = Array.FindIndex(m_Transform, t => t.GetEntityId() == transformId);
                    if (index >= 0)
                    {
                        ArrayRemoveAt(ref m_Transform, index);
                    }
                    m_Dirty = true;
                }
                else
                {
                    transformData.refCount -= 1;
                    m_TransformData[transformId] = transformData;
                }
            }
        }
    }

    [BurstCompile]
    internal static class TransformAccessJobUtilities
    {
        [BurstCompile]
        internal static bool AnyDestroyed(in NativeArray<byte> states)
        {
            for (int i = 0; i < states.Length; ++i)
            {
                if (states[i] == TransformAccessJob.k_TransformDestroyed)
                    return true;
            }

            return false;
        }
    }

    [BurstCompile]
    internal struct LocalToWorldAndChangeDetectionTransformAccessJob : IJobParallelForTransform
    {
        public NativeArray<float4x4> outMatrix;
        [WriteOnly]
        public NativeArray<byte> state;

        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                float4x4 localToWorldMatrix = transform.localToWorldMatrix;

                state[index] = outMatrix[index].Equals(localToWorldMatrix) ? TransformAccessJob.k_TransformUnchanged : TransformAccessJob.k_TransformChanged;
                outMatrix[index] = localToWorldMatrix;
            }
        }
    }

    [BurstCompile]
    internal struct WorldToLocalTransformAccessJob : IJobParallelForTransform
    {
        [WriteOnly]
        public NativeArray<float4x4> outMatrix;

        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
                outMatrix[index] = transform.worldToLocalMatrix;
        }
    }
}
