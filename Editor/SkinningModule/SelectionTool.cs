using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D.Layout;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class SelectionTool : BaseTool
    {
        bool m_ForceSelectedToSpriteEditor = false;
        int m_LastMouseButtonDown = -1;

        public event Func<bool> CanSelect = () => true;
        List<SpriteCache> m_Sprites;
        public ISpriteEditor spriteEditor { get; set; }

        internal SpriteCache selectedSprite
        {
            get => skinningCache.selectedSprite;
            set
            {
                if (selectedSprite != value)
                {
                    skinningCache.vertexSelection.Clear();

                    if (skinningCache.mode == SkinningMode.SpriteSheet)
                    {
                        skinningCache.skeletonSelection.Clear();
                        skinningCache.events.boneSelectionChanged.Invoke();
                    }

                    skinningCache.selectedSprite = value;
                    SetToSpriteEditor();
                    skinningCache.events.selectedSpriteChanged.Invoke(value);
                }
            }
        }

        string selectedSpriteAssetID
        {
            get
            {
                Sprite sprite = Selection.activeObject as Sprite;

                if (sprite != null)
                    return sprite.GetSpriteID().ToString();

                return "";
            }
        }

        protected override void OnAfterDeserialize()
        {
            m_ForceSelectedToSpriteEditor = true;
        }

        public override void Initialize(LayoutOverlay layoutOverlay)
        {
            m_Sprites = new List<SpriteCache>(skinningCache.GetSprites());
            SetFromSpriteEditor();
        }

        protected override void OnActivate()
        {
            SetToSpriteEditor();
            skinningCache.events.selectedSpriteChanged.AddListener(OnSpriteSelectionChange);
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        protected override void OnDeactivate()
        {
            skinningCache.events.selectedSpriteChanged.RemoveListener(OnSpriteSelectionChange);
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSpriteSelectionChange(SpriteCache sprite)
        {
            skinningCache.events.selectedSpriteChanged.RemoveListener(OnSpriteSelectionChange);
            selectedSprite = sprite;
            skinningCache.events.selectedSpriteChanged.AddListener(OnSpriteSelectionChange);
        }

        void OnSelectionChanged()
        {
            if (m_ForceSelectedToSpriteEditor)
            {
                SetToSpriteEditor();
                m_ForceSelectedToSpriteEditor = false;
            }
            else
            {
                using (skinningCache.UndoScope(TextContent.selectionChange))
                {
                    SetFromSpriteEditor();
                }
            }
        }

        void SetFromSpriteEditor()
        {
            if (selectedSprite == null)
                selectedSprite = skinningCache.GetSprite(selectedSpriteAssetID);
            spriteEditor.RequestRepaint();
        }

        void SetToSpriteEditor()
        {
            string id = "";

            if (selectedSprite != null)
                id = selectedSprite.id;

            spriteEditor.selectedSpriteRect = new SpriteRect() { spriteID = new GUID(id) };
        }

        protected override void OnGUI()
        {
            HandleHoverEvent();
            HandleSpriteSelection();
        }

        void HandleHoverEvent()
        {
            Event e = Event.current;

            if (e.type != EventType.MouseMove)
                return;

            SpriteCache prevHoveredSprite = skinningCache.hoveredSprite;

            if (!CanSelect())
            {
                skinningCache.hoveredSprite = null;
            }
            else
            {
                Vector3 mousePosition = Handles.inverseMatrix.MultiplyPoint(e.mousePosition);

                if (selectedSprite != null && IsPositionInSprite(mousePosition, selectedSprite, inBounds: true))
                {
                    skinningCache.hoveredSprite = selectedSprite;
                }
                else
                {
                    skinningCache.hoveredSprite = TrySelect(mousePosition);
                }
            }

            if (prevHoveredSprite != skinningCache.hoveredSprite)
                spriteEditor.RequestRepaint();
        }

        void HandleSpriteSelection()
        {
            Debug.Assert(Event.current != null);

            if (Event.current.type == EventType.MouseDown)
            {
                bool canSelect = CanSelect();
                bool acceptsDeselectionOnly = !canSelect && selectedSprite != null;

                if (IsSelectionRequested() && (canSelect || acceptsDeselectionOnly))
                {
                    Vector3 mousePosition = Handles.inverseMatrix.MultiplyPoint(Event.current.mousePosition);
                    SpriteCache newSelected = TrySelect(mousePosition, true);

                    if (acceptsDeselectionOnly && newSelected != null)
                        return;

                    if (selectedSprite != newSelected)
                    {
                        using (skinningCache.UndoScope(TextContent.selectionChange))
                        {
                            selectedSprite = newSelected;
                            skinningCache.hoveredSprite = null;
                        }

                        Event.current.Use();
                    }
                }
                else
                    m_LastMouseButtonDown = Event.current.button;
            }
        }

        bool IsPositionInSprite(Vector2 position, SpriteCache sprite, bool inBounds = false)
        {
            MeshPreviewCache meshPreview = sprite.GetMeshPreview();
            Debug.Assert(meshPreview != null);

            Vector3 spritePosition = sprite.GetLocalToWorldMatrixFromMode().MultiplyPoint3x4(Vector3.zero);
            Ray ray = new Ray((Vector3)position - spritePosition + Vector3.back, Vector3.forward);
            Bounds bounds = meshPreview.mesh.bounds;

            MeshCache mesh = sprite.GetMesh();
            Debug.Assert(mesh != null);

            if (!inBounds && mesh.indices.Length >= 3)
            {
                if (bounds.IntersectRay(ray))
                {
                    int[] indices = mesh.indices;
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 p1 = meshPreview.vertices[indices[i]];
                        Vector3 p2 = meshPreview.vertices[indices[i + 1]];
                        Vector3 p3 = meshPreview.vertices[indices[i + 2]];

                        if (MathUtility.Intersect(p1, p2, p3, ray))
                            return true;
                    }
                }
            }
            else
            {
                return meshPreview.defaultMesh.bounds.IntersectRay(ray);
            }

            return false;
        }

        SpriteCache TrySelect(Vector2 mousePosition, bool allowsOverlappedSpriteSelection = false)
        {
            SpriteCache newSelected = null;

            if (selectedSprite != null && !allowsOverlappedSpriteSelection)
            {
                // Use bounds check for the selected sprite to prevent other sprites from being highlighted
                // within its bounds during hover, avoiding interference with operations on the selected sprite.
                if (IsPositionInSprite(mousePosition, selectedSprite, inBounds: true))
                    return selectedSprite;
            }

            IEnumerable<SpriteCache> notVisiblePart = skinningCache.hasCharacter && skinningCache.mode == SkinningMode.Character
                ? skinningCache.character.parts.Where(x => !x.isVisible).Select(x => x.sprite)
                : new SpriteCache[0];

            bool hasHitSelectedSprite = false;
            int firstHitIndex = -1;
            int selectedSpriteIndex = selectedSprite != null ? m_Sprites.IndexOf(selectedSprite) : -1;
            for (int index = 0; index < m_Sprites.Count; ++index)
            {
                SpriteCache sprite = m_Sprites[index];
                if (notVisiblePart.Contains(sprite))
                    continue;

                if (IsPositionInSprite(mousePosition, sprite))
                {
                    if (sprite == selectedSprite)
                        hasHitSelectedSprite = true;

                    if (firstHitIndex == -1)
                        firstHitIndex = index;

                    // Skip overlapped sprites in front of the selected sprite.
                    if (allowsOverlappedSpriteSelection && (!hasHitSelectedSprite || index <= selectedSpriteIndex))
                        continue;

                    newSelected = sprite;
                    break;
                }
            }

            // Enable cycling through overlapped sprites.
            if (allowsOverlappedSpriteSelection && newSelected == null && firstHitIndex != -1)
                newSelected = m_Sprites[firstHitIndex];

            return newSelected;
        }

        bool IsSelectionRequested()
        {
            return Event.current.button == 0 && m_LastMouseButtonDown == 0 && GUIUtility.hotControl == 0 &&
                !Event.current.alt && Event.current.clickCount == 2;
        }
    }
}
