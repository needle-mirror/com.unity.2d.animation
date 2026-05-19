using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation.Profiler;

namespace UnityEditor.U2D.Animation.Profiler.UI
{
    record SpriteSkinHierarchyNodeData
    {
        public readonly string name;
        public readonly EntityId entityId;
        public int boneCount;
        public readonly int id;
        public string icon;
        public readonly string deformationType;

        public List<SpriteSkinHierarchyNodeData> children = new();

        public SpriteSkinHierarchyNodeData(string name, EntityId entityId, int boneCount, int id, int deformationType, string icon)
        {
            this.name = name;
            this.entityId = entityId;
            this.boneCount = boneCount;
            this.id = id;
            this.icon = icon;
            this.deformationType = SpriteSkinProfilerFrameData.GetSpriteSkinTypeName(deformationType);
        }

        public virtual bool Equals(SpriteSkinHierarchyNodeData other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return entityId == other.entityId;
        }

        public override int GetHashCode()
        {
            return entityId.GetHashCode();
        }

        public void AddChild(SpriteSkinHierarchyNodeData spriteSkinHierarchyNodeData)
        {
            children.Add(spriteSkinHierarchyNodeData);
            boneCount += spriteSkinHierarchyNodeData.boneCount;
        }

        public static int Compare(SpriteSkinHierarchyNodeData a, SpriteSkinHierarchyNodeData b, string propertyToCompare)
        {
            switch (propertyToCompare)
            {
                case "name":
                case null:
                    return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                case "boneCount":
                    return a.boneCount.CompareTo(b.boneCount);
                case "deformationType":
                    return string.Compare(a.deformationType, b.deformationType, StringComparison.OrdinalIgnoreCase);
                default:
                    return 0;
            }
        }
    }
}
