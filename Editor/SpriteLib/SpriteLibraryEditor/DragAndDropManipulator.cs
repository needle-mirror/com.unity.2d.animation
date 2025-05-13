using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Animation.SpriteLibraryEditor
{
    internal class DragAndDropManipulator : Manipulator
    {
        public const string overlayClassName = "DragAndDropOverlay";
        const string k_DragReceiverClassName = "DragReceiver";

        event Action<IList<DragAndDropData>, bool> onDragPerform;
        VisualElement m_OverlayVisual;
        Func<bool> m_CanStartDrag;

        const string k_DragOverAddClassName = SpriteLibraryEditorWindow.editorWindowClassName + "__drag-over-add";
        static readonly List<string> k_SupportedPsdExtensions = new() { ".psd", ".psb" };
        bool m_IsDragging;
        bool m_IsChildDragged;

        bool isActiveDrag => m_IsDragging && !m_IsChildDragged;

        public DragAndDropManipulator(VisualElement overlayVisual, Func<bool> canDragStart, Action<IList<DragAndDropData>, bool> dragPerform)
        {
            m_OverlayVisual = overlayVisual;

            m_OverlayVisual.AddToClassList(overlayClassName);

            m_CanStartDrag = canDragStart;
            onDragPerform = dragPerform;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.AddToClassList(k_DragReceiverClassName);

            target.RegisterCallback<DragEnterEvent>(OnDragEnter);
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate, TrickleDown.TrickleDown);
            target.RegisterCallback<DragExitedEvent>(OnDragExit, TrickleDown.TrickleDown);
            target.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.RemoveFromClassList(k_DragReceiverClassName);

            target.UnregisterCallback<DragEnterEvent>(OnDragEnter);
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.UnregisterCallback<DragExitedEvent>(OnDragExit);
            target.UnregisterCallback<DragLeaveEvent>(OnDragLeave);
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        void OnDragEnter(DragEnterEvent evt)
        {
            if (evt.currentTarget == evt.target)
                TryStartDrag();
        }

        void OnDragUpdate(DragUpdatedEvent evt)
        {
            if (evt.currentTarget == evt.target)
                TryStartDrag();

            m_IsChildDragged = evt.currentTarget != evt.target;

            if (isActiveDrag)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            UpdateVisuals();
            if (isActiveDrag)
                evt.StopImmediatePropagation();
        }

        void OnDragExit(DragExitedEvent evt)
        {
            StopDragging();
        }

        void OnDragLeave(DragLeaveEvent evt)
        {
            StopDragging();
        }

        void OnDragPerform(DragPerformEvent evt)
        {
            if (!isActiveDrag)
                return;

            DragAndDrop.AcceptDrag();
            List<DragAndDropData> spritesData = RetrieveDraggedSprites(DragAndDrop.objectReferences);
            if (spritesData.Count > 0)
                onDragPerform?.Invoke(spritesData, evt.altKey);

            DragAndDrop.objectReferences = new Object[] { };

            StopDragging();
        }

        void TryStartDrag()
        {
            if (m_IsDragging)
                return;

            // Early out when list is reordered
            if (DragAndDrop.GetGenericData("user_data") != null)
                return;

            if (!HasAnyDraggedSprites(DragAndDrop.objectReferences))
                return;

            if (!m_CanStartDrag())
                return;

            m_IsDragging = true;

            UpdateVisuals();
        }

        void StopDragging()
        {
            if (!m_IsDragging)
                return;

            m_IsDragging = false;

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            m_OverlayVisual.EnableInClassList(k_DragOverAddClassName, isActiveDrag);
        }

        static bool HasAnyDraggedSprites(Object[] objectReferences)
        {
            if (objectReferences == null || objectReferences.Length == 0)
                return false;

            foreach (Object objectReference in objectReferences)
            {
                switch (objectReference)
                {
                    case Sprite:
                        return true;
                    case Texture2D texture2D:
                    {
                        string texturePath = AssetDatabase.GetAssetPath(texture2D);
                        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(texturePath))
                        {
                            if (obj is Sprite)
                                return true;
                        }

                        break;
                    }
                    case GameObject gameObject:
                    {
                        bool isPsdGameObjectRoot = gameObject.transform.parent != null;
                        if (isPsdGameObjectRoot)
                            continue;

                        string psdFilePath = AssetDatabase.GetAssetPath(gameObject);
                        if (string.IsNullOrEmpty(psdFilePath))
                            continue;

                        string ext = Path.GetExtension(psdFilePath);
                        if (k_SupportedPsdExtensions.Contains(ext))
                        {
                            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(psdFilePath))
                            {
                                Sprite spriteObj = obj as Sprite;
                                if (spriteObj != null)
                                    return true;
                            }
                        }

                        break;
                    }
                }
            }

            return true;
        }

        static List<DragAndDropData> RetrieveDraggedSprites(Object[] objectReferences)
        {
            List<DragAndDropData> data = new List<DragAndDropData>();
            List<Sprite> unassociatedSprites = new List<Sprite>();
            foreach (Object objectReference in objectReferences)
            {
                switch (objectReference)
                {
                    case Sprite sprite:
                        unassociatedSprites.Add(sprite);
                        break;
                    case Texture2D texture2D:
                    {
                        string texturePath = AssetDatabase.GetAssetPath(texture2D);
                        List<Sprite> spritesFromTexture = new List<Sprite>();
                        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(texturePath))
                        {
                            if (obj is Sprite)
                                spritesFromTexture.Add((Sprite)obj);
                        }

                        DragAndDropData textureData = new DragAndDropData
                        {
                            name = Path.GetFileNameWithoutExtension(texturePath),
                            sprites = new List<Sprite>(spritesFromTexture),
                            spriteSourceType = SpriteSourceType.Sprite
                        };

                        data.Add(textureData);
                        break;
                    }
                    case GameObject gameObject:
                    {
                        bool isPsdGameObjectRoot = gameObject.transform.parent != null;
                        if (isPsdGameObjectRoot)
                            continue;

                        string psdFilePath = AssetDatabase.GetAssetPath(gameObject);
                        if (string.IsNullOrEmpty(psdFilePath))
                            continue;

                        string ext = Path.GetExtension(psdFilePath);
                        if (k_SupportedPsdExtensions.Contains(ext))
                        {
                            List<Sprite> psdSprites = new List<Sprite>();
                            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(psdFilePath))
                            {
                                Sprite spriteObj = obj as Sprite;
                                if (spriteObj != null)
                                    psdSprites.Add(spriteObj);
                            }

                            DragAndDropData psdData = new DragAndDropData
                            {
                                name = Path.GetFileNameWithoutExtension(psdFilePath),
                                sprites = new List<Sprite>(psdSprites),
                                spriteSourceType = SpriteSourceType.Psb
                            };

                            data.Add(psdData);
                        }

                        break;
                    }
                }
            }

            if (unassociatedSprites.Count > 0)
            {
                DragAndDropData spritesData = new DragAndDropData
                {
                    name = unassociatedSprites[0].name,
                    sprites = unassociatedSprites,
                    spriteSourceType = SpriteSourceType.Sprite
                };
                data.Add(spritesData);
            }

            return data;
        }
    }
}
