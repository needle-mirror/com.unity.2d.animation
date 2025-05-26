using System;
using Unity.Mathematics;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class SpriteMeshController
    {
        const float k_SnapDistance = 10f;

        struct EdgeIntersectionResult
        {
            public int startVertexIndex;
            public int endVertexIndex;
            public int intersectEdgeIndex;
            public Vector2 endPosition;
        }

        SpriteMeshDataController m_SpriteMeshDataController = new();
        EdgeIntersectionResult m_EdgeIntersectionResult;

        public ISpriteMeshView spriteMeshView { get; set; }
        public BaseSpriteMeshData spriteMeshData { get; set; }
        public ISelection<int> selection { get; set; }
        public ICacheUndo cacheUndo { get; set; }
        public ITriangulator triangulator { get; set; }

        public bool disable { get; set; }
        public Rect frame { get; set; }

        public void OnGUI()
        {
            m_SpriteMeshDataController.spriteMeshData = spriteMeshData;

            Debug.Assert(spriteMeshView != null);
            Debug.Assert(spriteMeshData != null);
            Debug.Assert(selection != null);
            Debug.Assert(cacheUndo != null);

            ValidateSelectionValues();

            spriteMeshView.selection = selection;
            spriteMeshView.frame = frame;

            EditorGUI.BeginDisabledGroup(disable);

            spriteMeshView.BeginLayout();

            if (spriteMeshView.CanLayout())
            {
                LayoutVertices();
                LayoutEdges();
            }

            spriteMeshView.EndLayout();

            if (spriteMeshView.CanRepaint())
            {
                DrawEdges();

                if (GUI.enabled)
                {
                    PreviewCreateVertex();
                    PreviewCreateEdge();
                    PreviewSplitEdge();
                }

                DrawVertices();
            }


            HandleSplitEdge();
            HandleCreateEdge();
            HandleCreateVertex();

            EditorGUI.EndDisabledGroup();

            HandleSelectVertex();
            HandleSelectEdge();

            EditorGUI.BeginDisabledGroup(disable);

            HandleMoveVertexAndEdge();

            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(disable);

            HandleRemoveVertices();

            spriteMeshView.DoRepaint();

            EditorGUI.EndDisabledGroup();
        }

        void ValidateSelectionValues()
        {
            foreach (int index in selection.elements)
            {
                if (index >= spriteMeshData.vertexCount)
                {
                    selection.Clear();
                    break;
                }
            }
        }

        void LayoutVertices()
        {
            for (int i = 0; i < spriteMeshData.vertexCount; i++)
            {
                spriteMeshView.LayoutVertex(spriteMeshData.vertices[i], i);
            }
        }

        void LayoutEdges()
        {
            for (int i = 0; i < spriteMeshData.edges.Length; i++)
            {
                int2 edge = spriteMeshData.edges[i];
                Vector2 startPosition = spriteMeshData.vertices[edge.x];
                Vector2 endPosition = spriteMeshData.vertices[edge.y];

                spriteMeshView.LayoutEdge(startPosition, endPosition, i);
            }
        }

        void DrawEdges()
        {
            UpdateEdgeIntersection();

            spriteMeshView.BeginDrawEdges();

            for (int i = 0; i < spriteMeshData.edges.Length; ++i)
            {
                if (SkipDrawEdge(i))
                    continue;

                int2 edge = spriteMeshData.edges[i];
                Vector2 startPosition = spriteMeshData.vertices[edge.x];
                Vector2 endPosition = spriteMeshData.vertices[edge.y];

                if (selection.Contains(edge.x) && selection.Contains(edge.y))
                    spriteMeshView.DrawEdgeSelected(startPosition, endPosition);
                else
                    spriteMeshView.DrawEdge(startPosition, endPosition);
            }

            if (spriteMeshView.IsActionActive(MeshEditorAction.SelectEdge))
            {
                int2 hoveredEdge = spriteMeshData.edges[spriteMeshView.hoveredEdge];
                Vector2 startPosition = spriteMeshData.vertices[hoveredEdge.x];
                Vector2 endPosition = spriteMeshData.vertices[hoveredEdge.y];

                spriteMeshView.DrawEdgeHovered(startPosition, endPosition);
            }

            spriteMeshView.EndDrawEdges();
        }

        bool SkipDrawEdge(int edgeIndex)
        {
            if (GUI.enabled == false)
                return false;

            return edgeIndex == -1 ||
                spriteMeshView.hoveredEdge == edgeIndex && spriteMeshView.IsActionActive(MeshEditorAction.SelectEdge) ||
                spriteMeshView.hoveredEdge == edgeIndex && spriteMeshView.IsActionActive(MeshEditorAction.CreateVertex) ||
                spriteMeshView.closestEdge == edgeIndex && spriteMeshView.IsActionActive(MeshEditorAction.SplitEdge) ||
                edgeIndex == m_EdgeIntersectionResult.intersectEdgeIndex && spriteMeshView.IsActionActive(MeshEditorAction.CreateEdge);
        }

        void PreviewCreateVertex()
        {
            if (spriteMeshView.mode == SpriteMeshViewMode.CreateVertex &&
                spriteMeshView.IsActionActive(MeshEditorAction.CreateVertex))
            {
                Vector2 clampedMousePos = ClampToFrame(spriteMeshView.mouseWorldPosition);

                if (spriteMeshView.hoveredEdge != -1)
                {
                    int2 edge = spriteMeshData.edges[spriteMeshView.hoveredEdge];

                    spriteMeshView.BeginDrawEdges();

                    spriteMeshView.DrawEdge(spriteMeshData.vertices[edge.x], clampedMousePos);
                    spriteMeshView.DrawEdge(spriteMeshData.vertices[edge.y], clampedMousePos);

                    spriteMeshView.EndDrawEdges();
                }

                spriteMeshView.DrawVertex(clampedMousePos);
            }
        }

        void PreviewCreateEdge()
        {
            if (!spriteMeshView.IsActionActive(MeshEditorAction.CreateEdge))
                return;

            spriteMeshView.BeginDrawEdges();

            spriteMeshView.DrawEdge(spriteMeshData.vertices[m_EdgeIntersectionResult.startVertexIndex], m_EdgeIntersectionResult.endPosition);

            if (m_EdgeIntersectionResult.intersectEdgeIndex != -1)
            {
                int2 intersectingEdge = spriteMeshData.edges[m_EdgeIntersectionResult.intersectEdgeIndex];
                spriteMeshView.DrawEdge(spriteMeshData.vertices[intersectingEdge.x], m_EdgeIntersectionResult.endPosition);
                spriteMeshView.DrawEdge(spriteMeshData.vertices[intersectingEdge.y], m_EdgeIntersectionResult.endPosition);
            }

            spriteMeshView.EndDrawEdges();

            if (m_EdgeIntersectionResult.endVertexIndex == -1)
                spriteMeshView.DrawVertex(m_EdgeIntersectionResult.endPosition);
        }

        void PreviewSplitEdge()
        {
            if (!spriteMeshView.IsActionActive(MeshEditorAction.SplitEdge))
                return;

            Vector2 clampedMousePos = ClampToFrame(spriteMeshView.mouseWorldPosition);

            int2 closestEdge = spriteMeshData.edges[spriteMeshView.closestEdge];

            spriteMeshView.BeginDrawEdges();

            spriteMeshView.DrawEdge(spriteMeshData.vertices[closestEdge.x], clampedMousePos);
            spriteMeshView.DrawEdge(spriteMeshData.vertices[closestEdge.y], clampedMousePos);

            spriteMeshView.EndDrawEdges();

            spriteMeshView.DrawVertex(clampedMousePos);
        }

        void DrawVertices()
        {
            for (int i = 0; i < spriteMeshData.vertexCount; i++)
            {
                Vector2 position = spriteMeshData.vertices[i];

                if (selection.Contains(i))
                    spriteMeshView.DrawVertexSelected(position);
                else if (i == spriteMeshView.hoveredVertex && spriteMeshView.IsActionHot(MeshEditorAction.None))
                    spriteMeshView.DrawVertexHovered(position);
                else
                    spriteMeshView.DrawVertex(position);
            }
        }

        void HandleSelectVertex()
        {
            if (spriteMeshView.DoSelectVertex(out bool additive))
                SelectVertex(spriteMeshView.hoveredVertex, additive);
        }

        void HandleSelectEdge()
        {
            if (spriteMeshView.DoSelectEdge(out bool additive))
                SelectEdge(spriteMeshView.hoveredEdge, additive);
        }

        void HandleMoveVertexAndEdge()
        {
            if (selection.Count == 0)
                return;

            if (spriteMeshView.DoMoveVertex(out Vector2 finalDeltaPos) || spriteMeshView.DoMoveEdge(out finalDeltaPos))
            {
                int[] selectionArray = selection.elements;

                finalDeltaPos = MathUtility.MoveRectInsideFrame(CalculateRectFromSelection(), frame, finalDeltaPos);
                Vector2[] movedVertexSelection = GetMovedVertexSelection(in selectionArray, spriteMeshData.vertices, finalDeltaPos);

                if (IsMovedEdgeIntersectingWithOtherEdge(in selectionArray, in movedVertexSelection, spriteMeshData.edges, spriteMeshData.vertices))
                    return;
                if (IsMovedVertexIntersectingWithOutline(in selectionArray, in movedVertexSelection, spriteMeshData.outlineEdges, spriteMeshData.vertices))
                    return;

                cacheUndo.BeginUndoOperation(TextContent.moveVertices);
                MoveSelectedVertices(in movedVertexSelection);
            }
        }

        void HandleCreateVertex()
        {
            if (spriteMeshView.DoCreateVertex())
            {
                Vector2 position = ClampToFrame(spriteMeshView.mouseWorldPosition);
                int edgeIndex = spriteMeshView.hoveredEdge;
                if (spriteMeshView.hoveredEdge != -1)
                    CreateVertex(position, edgeIndex);
                else if (m_SpriteMeshDataController.FindTriangle(position, out Vector3Int indices, out Vector3 barycentricCoords))
                    CreateVertex(position, indices, barycentricCoords);
            }
        }

        void HandleSplitEdge()
        {
            if (spriteMeshView.DoSplitEdge())
                SplitEdge(ClampToFrame(spriteMeshView.mouseWorldPosition), spriteMeshView.closestEdge);
        }

        void HandleCreateEdge()
        {
            if (spriteMeshView.DoCreateEdge())
            {
                Vector2 clampedMousePosition = ClampToFrame(spriteMeshView.mouseWorldPosition);
                EdgeIntersectionResult edgeIntersectionResult = CalculateEdgeIntersection(selection.activeElement, spriteMeshView.hoveredVertex, spriteMeshView.hoveredEdge, clampedMousePosition);

                if (edgeIntersectionResult.endVertexIndex != -1)
                {
                    CreateEdge(selection.activeElement, edgeIntersectionResult.endVertexIndex);
                }
                else
                {
                    if (edgeIntersectionResult.intersectEdgeIndex != -1)
                    {
                        CreateVertex(edgeIntersectionResult.endPosition, edgeIntersectionResult.intersectEdgeIndex);
                        CreateEdge(selection.activeElement, spriteMeshData.vertexCount - 1);
                    }
                    else if (m_SpriteMeshDataController.FindTriangle(edgeIntersectionResult.endPosition, out Vector3Int indices, out Vector3 barycentricCoords))
                    {
                        CreateVertex(edgeIntersectionResult.endPosition, indices, barycentricCoords);
                        CreateEdge(selection.activeElement, spriteMeshData.vertexCount - 1);
                    }
                }
            }
        }

        void HandleRemoveVertices()
        {
            if (spriteMeshView.DoRemove())
                RemoveSelectedVertices();
        }

        void CreateVertex(Vector2 position, Vector3Int indices, Vector3 barycentricCoords)
        {
            EditableBoneWeight bw1 = spriteMeshData.vertexWeights[indices.x];
            EditableBoneWeight bw2 = spriteMeshData.vertexWeights[indices.y];
            EditableBoneWeight bw3 = spriteMeshData.vertexWeights[indices.z];

            EditableBoneWeight result = new EditableBoneWeight();

            foreach (BoneWeightChannel channel in bw1)
            {
                if (!channel.enabled)
                    continue;

                float weight = channel.weight * barycentricCoords.x;
                if (weight > 0f)
                    result.AddChannel(channel.boneIndex, weight, true);
            }

            foreach (BoneWeightChannel channel in bw2)
            {
                if (!channel.enabled)
                    continue;

                float weight = channel.weight * barycentricCoords.y;
                if (weight > 0f)
                    result.AddChannel(channel.boneIndex, weight, true);
            }

            foreach (BoneWeightChannel channel in bw3)
            {
                if (!channel.enabled)
                    continue;

                float weight = channel.weight * barycentricCoords.z;
                if (weight > 0f)
                    result.AddChannel(channel.boneIndex, weight, true);
            }

            result.UnifyChannelsWithSameBoneIndex();
            result.FilterChannels(0f);
            result.Clamp(4, true);

            BoneWeight boneWeight = result.ToBoneWeight(true);

            cacheUndo.BeginUndoOperation(TextContent.createVertex);

            m_SpriteMeshDataController.CreateVertex(position, -1);
            spriteMeshData.vertexWeights[spriteMeshData.vertexCount - 1].SetFromBoneWeight(boneWeight);
            Triangulate();
        }

        void CreateVertex(Vector2 position, int edgeIndex)
        {
            int2 edge = spriteMeshData.edges[edgeIndex];
            Vector2 pos1 = spriteMeshData.vertices[edge.x];
            Vector2 pos2 = spriteMeshData.vertices[edge.y];
            Vector2 dir1 = (position - pos1);
            Vector2 dir2 = (pos2 - pos1);
            float t = Vector2.Dot(dir1, dir2.normalized) / dir2.magnitude;
            t = Mathf.Clamp01(t);
            BoneWeight bw1 = spriteMeshData.vertexWeights[edge.x].ToBoneWeight(true);
            BoneWeight bw2 = spriteMeshData.vertexWeights[edge.y].ToBoneWeight(true);

            BoneWeight boneWeight = EditableBoneWeightUtility.Lerp(bw1, bw2, t);

            cacheUndo.BeginUndoOperation(TextContent.createVertex);

            m_SpriteMeshDataController.CreateVertex(position, edgeIndex);
            spriteMeshData.vertexWeights[spriteMeshData.vertexCount - 1].SetFromBoneWeight(boneWeight);
            Triangulate();
        }

        void SelectVertex(int index, bool additiveToggle)
        {
            if (index < 0)
                throw new ArgumentException("Index out of range");

            bool selected = selection.Contains(index);
            if (selected)
            {
                if (additiveToggle)
                {
                    cacheUndo.BeginUndoOperation(TextContent.selection);
                    selection.Select(index, false);
                }
            }
            else
            {
                cacheUndo.BeginUndoOperation(TextContent.selection);

                if (!additiveToggle)
                    ClearSelection();

                selection.Select(index, true);
            }

            cacheUndo.IncrementCurrentGroup();
        }

        void SelectEdge(int index, bool additiveToggle)
        {
            Debug.Assert(index >= 0);

            int2 edge = spriteMeshData.edges[index];

            cacheUndo.BeginUndoOperation(TextContent.selection);

            bool selected = selection.Contains(edge.x) && selection.Contains(edge.y);
            if (selected)
            {
                if (additiveToggle)
                {
                    selection.Select(edge.x, false);
                    selection.Select(edge.y, false);
                }
            }
            else
            {
                if (!additiveToggle)
                    ClearSelection();

                selection.Select(edge.x, true);
                selection.Select(edge.y, true);
            }

            cacheUndo.IncrementCurrentGroup();
        }

        void ClearSelection()
        {
            cacheUndo.BeginUndoOperation(TextContent.selection);
            selection.Clear();
        }

        void MoveSelectedVertices(in Vector2[] movedVertices)
        {
            for (int i = 0; i < selection.Count; ++i)
            {
                int index = selection.elements[i];
                spriteMeshData.vertices[index] = movedVertices[i];
            }

            Triangulate();
        }

        void CreateEdge(int fromVertexIndex, int toVertexIndex)
        {
            cacheUndo.BeginUndoOperation(TextContent.createEdge);

            m_SpriteMeshDataController.CreateEdge(fromVertexIndex, toVertexIndex);
            Triangulate();
            ClearSelection();
            selection.Select(toVertexIndex, true);

            cacheUndo.IncrementCurrentGroup();
        }

        void SplitEdge(Vector2 position, int edgeIndex)
        {
            cacheUndo.BeginUndoOperation(TextContent.splitEdge);

            CreateVertex(position, edgeIndex);

            cacheUndo.IncrementCurrentGroup();
        }

        bool IsEdgeSelected()
        {
            if (selection.Count != 2)
                return false;

            int[] indices = selection.elements;

            int index1 = indices[0];
            int index2 = indices[1];

            int2 edge = new int2(index1, index2);
            return spriteMeshData.edges.ContainsAny(edge);
        }

        void RemoveSelectedVertices()
        {
            cacheUndo.BeginUndoOperation(IsEdgeSelected() ? TextContent.removeEdge : TextContent.removeVertices);

            int[] verticesToRemove = selection.elements;

            int noOfVertsToDelete = verticesToRemove.Length;
            int noOfVertsInMesh = m_SpriteMeshDataController.spriteMeshData.vertexCount;
            bool shouldClearMesh = (noOfVertsInMesh - noOfVertsToDelete) < 3;

            if (shouldClearMesh)
            {
                m_SpriteMeshDataController.spriteMeshData.Clear();
                m_SpriteMeshDataController.CreateQuad();
            }
            else
                m_SpriteMeshDataController.RemoveVertex(verticesToRemove);

            Triangulate();

            selection.Clear();
        }

        void Triangulate()
        {
            m_SpriteMeshDataController.Triangulate(triangulator);
            m_SpriteMeshDataController.SortTrianglesByDepth();
        }

        Vector2 ClampToFrame(Vector2 position)
        {
            return MathUtility.ClampPositionToRect(position, frame);
        }

        Rect CalculateRectFromSelection()
        {
            Rect rect = new Rect();

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            int[] indices = selection.elements;

            foreach (int index in indices)
            {
                Vector2 v = spriteMeshData.vertices[index];

                min.x = Mathf.Min(min.x, v.x);
                min.y = Mathf.Min(min.y, v.y);

                max.x = Mathf.Max(max.x, v.x);
                max.y = Mathf.Max(max.y, v.y);
            }

            rect.min = min;
            rect.max = max;

            return rect;
        }

        void UpdateEdgeIntersection()
        {
            if (selection.Count == 1)
                m_EdgeIntersectionResult = CalculateEdgeIntersection(selection.activeElement, spriteMeshView.hoveredVertex, spriteMeshView.hoveredEdge, ClampToFrame(spriteMeshView.mouseWorldPosition));
        }

        EdgeIntersectionResult CalculateEdgeIntersection(int vertexIndex, int hoveredVertexIndex, int hoveredEdgeIndex, Vector2 targetPosition)
        {
            Debug.Assert(vertexIndex >= 0);

            EdgeIntersectionResult edgeIntersection = new EdgeIntersectionResult
            {
                startVertexIndex = vertexIndex,
                endVertexIndex = hoveredVertexIndex,
                endPosition = targetPosition,
                intersectEdgeIndex = -1
            };

            Vector2 startPoint = spriteMeshData.vertices[edgeIntersection.startVertexIndex];

            bool intersectsEdge = false;
            int lastIntersectingEdgeIndex = -1;

            do
            {
                lastIntersectingEdgeIndex = edgeIntersection.intersectEdgeIndex;

                if (intersectsEdge)
                {
                    Vector2 dir = edgeIntersection.endPosition - startPoint;
                    edgeIntersection.endPosition += dir.normalized * 10f;
                }

                intersectsEdge = SegmentIntersectsEdge(startPoint, edgeIntersection.endPosition, vertexIndex, ref edgeIntersection.endPosition, out edgeIntersection.intersectEdgeIndex);

                //if we are hovering a vertex and intersect an edge indexing it we forget about the intersection
                int2[] edges = spriteMeshData.edges;
                int2 edge = intersectsEdge ? edges[edgeIntersection.intersectEdgeIndex] : default;
                if (intersectsEdge && (edge.x == edgeIntersection.endVertexIndex || edge.y == edgeIntersection.endVertexIndex))
                {
                    edgeIntersection.intersectEdgeIndex = -1;
                    intersectsEdge = false;
                    edgeIntersection.endPosition = spriteMeshData.vertices[edgeIntersection.endVertexIndex];
                }

                if (intersectsEdge)
                {
                    edgeIntersection.endVertexIndex = -1;

                    int2 intersectingEdge = spriteMeshData.edges[edgeIntersection.intersectEdgeIndex];
                    Vector2 newPointScreen = spriteMeshView.WorldToScreen(edgeIntersection.endPosition);
                    Vector2 edgeV1 = spriteMeshView.WorldToScreen(spriteMeshData.vertices[intersectingEdge.x]);
                    Vector2 edgeV2 = spriteMeshView.WorldToScreen(spriteMeshData.vertices[intersectingEdge.y]);

                    if ((newPointScreen - edgeV1).magnitude <= k_SnapDistance)
                        edgeIntersection.endVertexIndex = intersectingEdge.x;
                    else if ((newPointScreen - edgeV2).magnitude <= k_SnapDistance)
                        edgeIntersection.endVertexIndex = intersectingEdge.y;

                    if (edgeIntersection.endVertexIndex != -1)
                    {
                        edgeIntersection.intersectEdgeIndex = -1;
                        intersectsEdge = false;
                        edgeIntersection.endPosition = spriteMeshData.vertices[edgeIntersection.endVertexIndex];
                    }
                }
            } while (intersectsEdge && lastIntersectingEdgeIndex != edgeIntersection.intersectEdgeIndex);

            edgeIntersection.intersectEdgeIndex = intersectsEdge ? edgeIntersection.intersectEdgeIndex : hoveredEdgeIndex;

            if (edgeIntersection.endVertexIndex != -1 && !intersectsEdge)
                edgeIntersection.endPosition = spriteMeshData.vertices[edgeIntersection.endVertexIndex];

            return edgeIntersection;
        }

        bool SegmentIntersectsEdge(Vector2 p1, Vector2 p2, int ignoreIndex, ref Vector2 point, out int intersectingEdgeIndex)
        {
            intersectingEdgeIndex = -1;

            float sqrDistance = float.MaxValue;

            for (int i = 0; i < spriteMeshData.edges.Length; i++)
            {
                int2 edge = spriteMeshData.edges[i];
                Vector2 v1 = spriteMeshData.vertices[edge.x];
                Vector2 v2 = spriteMeshData.vertices[edge.y];
                Vector2 pointTmp = Vector2.zero;

                if (edge.x != ignoreIndex && edge.y != ignoreIndex &&
                    MathUtility.SegmentIntersection(p1, p2, v1, v2, ref pointTmp))
                {
                    float sqrMagnitude = (pointTmp - p1).sqrMagnitude;
                    if (sqrMagnitude < sqrDistance)
                    {
                        sqrDistance = sqrMagnitude;
                        intersectingEdgeIndex = i;
                        point = pointTmp;
                    }
                }
            }

            return intersectingEdgeIndex != -1;
        }


        static Vector2[] GetMovedVertexSelection(in int[] selection, in Vector2[] vertices, Vector2 deltaPosition)
        {
            Vector2[] movedVertices = new Vector2[selection.Length];
            for (int i = 0; i < selection.Length; i++)
            {
                int index = selection[i];
                movedVertices[i] = vertices[index] + deltaPosition;
            }

            return movedVertices;
        }

        static bool IsMovedEdgeIntersectingWithOtherEdge(in int[] selection, in Vector2[] movedVertices, in int2[] meshEdges, in Vector2[] meshVertices)
        {
            int edgeCount = meshEdges.Length;
            Vector2 edgeIntersectionPoint = Vector2.zero;

            for (int i = 0; i < edgeCount; i++)
            {
                int2 selectionIndex = FindSelectionIndexFromEdge(selection, meshEdges[i]);
                if (selectionIndex.x == -1 && selectionIndex.y == -1)
                    continue;

                Vector2 edgeStart = selectionIndex.x != -1 ? movedVertices[selectionIndex.x] : meshVertices[meshEdges[i].x];
                Vector2 edgeEnd = selectionIndex.y != -1 ? movedVertices[selectionIndex.y] : meshVertices[meshEdges[i].y];

                for (int o = 0; o < edgeCount; o++)
                {
                    if (o == i)
                        continue;

                    if (meshEdges[i].x == meshEdges[o].x || meshEdges[i].y == meshEdges[o].x ||
                        meshEdges[i].x == meshEdges[o].y || meshEdges[i].y == meshEdges[o].y)
                        continue;

                    int2 otherSelectionIndex = FindSelectionIndexFromEdge(in selection, meshEdges[o]);
                    Vector2 otherEdgeStart = otherSelectionIndex.x != -1 ? movedVertices[otherSelectionIndex.x] : meshVertices[meshEdges[o].x];
                    Vector2 otherEdgeEnd = otherSelectionIndex.y != -1 ? movedVertices[otherSelectionIndex.y] : meshVertices[meshEdges[o].y];

                    if (MathUtility.SegmentIntersection(edgeStart, edgeEnd, otherEdgeStart, otherEdgeEnd, ref edgeIntersectionPoint))
                        return true;
                }
            }

            return false;
        }

        static int2 FindSelectionIndexFromEdge(in int[] selection, int2 edge)
        {
            int2 selectionIndex = new int2(-1, -1);
            for (int m = 0; m < selection.Length; ++m)
            {
                if (selection[m] == edge.x)
                {
                    selectionIndex.x = m;
                    break;
                }

                if (selection[m] == edge.y)
                {
                    selectionIndex.y = m;
                    break;
                }
            }

            return selectionIndex;
        }

        static bool IsMovedVertexIntersectingWithOutline(in int[] selection, in Vector2[] movedVertices, in int2[] outlineEdges, in Vector2[] meshVertices)
        {
            Vector2 edgeIntersectionPoint = Vector2.zero;

            for (int i = 0; i < selection.Length; ++i)
            {
                Vector2 edgeStart = meshVertices[selection[i]];
                Vector2 edgeEnd = movedVertices[i];

                for (int m = 0; m < outlineEdges.Length; ++m)
                {
                    if (selection[i] == outlineEdges[m].x || selection[i] == outlineEdges[m].y)
                        continue;

                    int2 otherSelectionIndex = FindSelectionIndexFromEdge(in selection, outlineEdges[m]);
                    Vector2 otherEdgeStart = otherSelectionIndex.x != -1 ? movedVertices[otherSelectionIndex.x] : meshVertices[outlineEdges[m].x];
                    Vector2 otherEdgeEnd = otherSelectionIndex.y != -1 ? movedVertices[otherSelectionIndex.y] : meshVertices[outlineEdges[m].y];

                    if (MathUtility.SegmentIntersection(edgeStart, edgeEnd, otherEdgeStart, otherEdgeEnd, ref edgeIntersectionPoint))
                        return true;
                }
            }

            return false;
        }
    }
}
