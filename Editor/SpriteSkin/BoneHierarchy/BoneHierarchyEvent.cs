using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Static events for Bone overlay and bone gizmo synchronization.
    /// </summary>
    internal static class BoneHierarchyEvent
    {
        [Flags]
        internal enum Types
        {
            None = 0,
            HierarchyStructureChanged = 1 << 0,
            ContextRebuilt = 1 << 1,
            BoneColorsPersisted = 1 << 2,
            BoneColorsRefreshed = 1 << 3,
        }

        /// <summary>
        /// Fired when bone hierarchy structure may have changed (e.g. SpriteSkin added or removed).
        /// Subscribers should perform a full selection rebuild equivalent to OnSelectionChanged.
        /// </summary>
        internal static event Action HierarchyStructureChanged;

        /// <summary>
        /// Fired after <see cref="BoneHierarchyContext.BuildFromSelections"/> finishes updating <see cref="BoneHierarchyContext.Current"/>.
        /// </summary>
        internal static event Action ContextRebuilt;

        /// <summary>
        /// Fired when serialized hierarchy color data may have changed. Raised from <see cref="BoneHierarchyObjectChangeListener"/> when ObjectChangeEvents report relevant edits (writes such as <see cref="BoneHierarchyDataBridge"/> rely on those notifications rather than invoking this event directly).
        /// </summary>
        internal static event Action BoneColorsPersisted;

        /// <summary>
        /// Fired after <see cref="BoneHierarchyContext.RefreshResolvedBoneColors"/> updates resolved bone colors without a full context rebuild (Bone overlay color field refresh).
        /// </summary>
        internal static event Action BoneColorsRefreshed;

        internal static void RaiseHierarchyStructureChanged()
        {
            HierarchyStructureChanged?.Invoke();
        }

        internal static void RaiseContextRebuilt()
        {
            ContextRebuilt?.Invoke();
        }

        internal static void RaiseBoneColorsPersisted()
        {
            BoneColorsPersisted?.Invoke();
        }

        internal static void RaiseBoneColorsRefreshed()
        {
            BoneColorsRefreshed?.Invoke();
        }
    }

    /// <summary>
    /// Subscribes to <see cref="ObjectChangeEvents.changesPublished"/>, walks the full <see cref="ObjectChangeEventStream"/>, aggregates <see cref="BoneHierarchyEvent.Types"/> flags, then raises at most one event: <see cref="BoneHierarchyEvent.HierarchyStructureChanged"/> if that flag is set, otherwise <see cref="BoneHierarchyEvent.BoneColorsPersisted"/> if that flag is set (both flags never result in two invocations in the same callback).
    /// </summary>
    [InitializeOnLoad]
    internal static class BoneHierarchyObjectChangeListener
    {
        static BoneHierarchyObjectChangeListener()
        {
            ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
        }

        /// <summary>
        /// Aggregates <see cref="BoneHierarchyEvent.Types"/> across the entire stream and invokes the matching <see cref="BoneHierarchyEvent"/> handlers. When <see cref="BoneHierarchyContext.Current"/> has no tracked bone hosts, only <see cref="ObjectChangeKind.ChangeGameObjectStructure"/> events are inspected so that a newly added <see cref="SpriteSkin"/> on a selected GameObject (Add Component or undo of Remove Component) still triggers a rebuild; other kinds are noise we can safely ignore in that state.
        /// </summary>
        static void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
        {
            bool hasContext = BoneHierarchyContext.Current.BoneHosts.Count > 0;

            BoneHierarchyEvent.Types events = BoneHierarchyEvent.Types.None;
            for (int i = 0; i < stream.length; i++)
            {
                ObjectChangeKind kind = stream.GetEventType(i);
                if (!hasContext && kind != ObjectChangeKind.ChangeGameObjectStructure)
                    continue;

                switch (kind)
                {
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        events |= TryHandleChangeGameObjectStructureHierarchy(ref stream, i);
                        break;
                    case ObjectChangeKind.ChangeGameObjectStructure:
                        events |= TryHandleChangeGameObjectStructure(ref stream, i);
                        break;
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        events |= TryHandleChangeGameObjectOrComponentProperties(ref stream, i);
                        break;
                    case ObjectChangeKind.UpdatePrefabInstances:
                        events |= TryHandleUpdatePrefabInstances(ref stream, i);
                        break;
                }
            }

            if (events.HasFlag(BoneHierarchyEvent.Types.HierarchyStructureChanged))
                BoneHierarchyEvent.RaiseHierarchyStructureChanged();
            else if (events.HasFlag(BoneHierarchyEvent.Types.BoneColorsPersisted))
                BoneHierarchyEvent.RaiseBoneColorsPersisted();
        }

        /// <summary>
        /// Handles <see cref="ObjectChangeKind.ChangeGameObjectStructureHierarchy"/> (reparenting). Typical cases include prefab override reverts and undo/redo on instances that store bone colors or contain nested prefabs or sprites used with bone colors.
        /// </summary>
        static BoneHierarchyEvent.Types TryHandleChangeGameObjectStructureHierarchy(ref ObjectChangeEventStream stream, int eventIndex)
        {
            return BoneHierarchyEvent.Types.HierarchyStructureChanged;
        }

        /// <summary>
        /// Handles <see cref="ObjectChangeKind.ChangeGameObjectStructure"/> (component add/remove on a GameObject). Detects <see cref="SpriteSkin"/> removal on a tracked GameObject (any tracked entry becomes Unity-null) and <see cref="SpriteSkin"/> appearance / restoration via undo (event's GameObject now hosts a <see cref="SpriteSkin"/> that the context does not track yet) and returns <see cref="BoneHierarchyEvent.Types.HierarchyStructureChanged"/> in either case. Falls back to <see cref="BoneHierarchyEvent.Types.BoneColorsPersisted"/> via <see cref="CanPersistBoneColors"/> for first bone color edits and their undo/redo, adding or removing <see cref="SpriteBoneHierarchyData"/>, and undo/redo of those operations.
        /// </summary>
        static BoneHierarchyEvent.Types TryHandleChangeGameObjectStructure(ref ObjectChangeEventStream stream, int eventIndex)
        {
            stream.GetChangeGameObjectStructureEvent(eventIndex, out ChangeGameObjectStructureEventArgs ev);

            {
                foreach (SpriteSkin spriteSkin in BoneHierarchyContext.Current.SpriteSkins)
                {
                    if (spriteSkin == null)
                    {
                        // Tracked SpriteSkin removed (entry became Unity-null).
                        return BoneHierarchyEvent.Types.HierarchyStructureChanged;
                    }
                }
            }

            UnityEngine.Object obj = EditorUtility.EntityIdToObject(ev.entityId);

            if (obj is GameObject go)
            {
                SpriteSkin spriteSkin = go.GetComponent<SpriteSkin>();
                if (spriteSkin != null && !BoneHierarchyContext.Current.ContainsSpriteSkin(spriteSkin))
                {
                    // New SpriteSkin appeared on event's GameObject.
                    return BoneHierarchyEvent.Types.HierarchyStructureChanged;
                }
            }

            if (CanPersistBoneColors(obj))
                return BoneHierarchyEvent.Types.BoneColorsPersisted;

            return BoneHierarchyEvent.Types.None;
        }

        /// <summary>
        /// Handles <see cref="ObjectChangeKind.ChangeGameObjectOrComponentProperties"/>: returns <see cref="BoneHierarchyEvent.Types.BoneColorsPersisted"/> for <see cref="SpriteBoneHierarchyData"/> edits, and <see cref="BoneHierarchyEvent.Types.HierarchyStructureChanged"/> for property changes on a <see cref="SpriteSkin"/> already tracked by <see cref="BoneHierarchyContext.Current"/> (covers bone removal / replacement on the selected skin via the Inspector). Property changes on untracked <see cref="SpriteSkin"/> components are ignored to avoid scene-wide rebuilds.
        /// </summary>
        static BoneHierarchyEvent.Types TryHandleChangeGameObjectOrComponentProperties(ref ObjectChangeEventStream stream, int eventIndex)
        {
            stream.GetChangeGameObjectOrComponentPropertiesEvent(eventIndex, out ChangeGameObjectOrComponentPropertiesEventArgs ev);

            UnityEngine.Object obj = EditorUtility.EntityIdToObject(ev.entityId);
            if (obj is SpriteBoneHierarchyData)
                return BoneHierarchyEvent.Types.BoneColorsPersisted;

            if (obj is SpriteSkin spriteSkin && BoneHierarchyContext.Current.ContainsSpriteSkin(spriteSkin))
                return BoneHierarchyEvent.Types.HierarchyStructureChanged;

            return BoneHierarchyEvent.Types.None;
        }

        /// <summary>
        /// Handles <see cref="ObjectChangeKind.UpdatePrefabInstances"/> when prefab instances are refreshed, including undo/redo that affects nested prefabs or bone color overrides under the prefab hierarchy.
        /// </summary>
        static BoneHierarchyEvent.Types TryHandleUpdatePrefabInstances(ref ObjectChangeEventStream stream, int eventIndex)
        {
            stream.GetUpdatePrefabInstancesEvent(eventIndex, out UpdatePrefabInstancesEventArgs ev);

            foreach (EntityId entityId in ev.entityIds)
            {
                UnityEngine.Object obj = EditorUtility.EntityIdToObject(entityId);
                if (CanPersistBoneColors(obj))
                    return BoneHierarchyEvent.Types.BoneColorsPersisted;
            }
            return BoneHierarchyEvent.Types.None;
        }

        static bool CanPersistBoneColors(UnityEngine.Object obj)
        {
            if (obj is GameObject go)
            {
                if (BoneHierarchyUtilities.ResolveBoneHost(go.transform, out Transform boneHost))
                    return boneHost != null;
            }
            return false;
        }
    }
}
