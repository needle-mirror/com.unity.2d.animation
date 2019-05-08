using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Experimental.U2D.Animation
{
    [Serializable]
    public struct SpriteLibCategory
    {
        [SerializeField]
        public string name;
        [SerializeField]
        public List<string> spriteIds;
    }

    [Serializable]
    public struct SpriteLibrary
    {
        [SerializeField]
        public List<SpriteLibCategory> categories;
    }

    internal class SpriteLibraryCacheObject : SkinningObject
    {
        [SerializeField]
        public List<SpriteLibCategory> categories = new List<SpriteLibCategory>();

        public void CopyFrom(SpriteLibrary library)
        {
            categories.Clear();
            foreach (var cat in library.categories)
            {
                var spriteLibCategory = new SpriteLibCategory()
                {
                    name = cat.name,
                    spriteIds = new List<string>(cat.spriteIds)
                };
                categories.Add(spriteLibCategory);
            }
        }

        public SpriteLibrary ToSpriteLibrary()
        {
            var spriteLibrary = new SpriteLibrary();
            spriteLibrary.categories = new List<SpriteLibCategory>();
            foreach (var cat in categories)
            {
                var spriteLibCategory = new SpriteLibCategory()
                {
                    name = cat.name,
                    spriteIds = new List<string>(cat.spriteIds)
                };
                spriteLibrary.categories.Add(spriteLibCategory);
            }
            return spriteLibrary;
        }

        public void RemoveSpriteFromCategory(string sprite)
        {
            for (int i = 0; i < categories.Count; ++i)
            {
                var index = categories[i].spriteIds.FindIndex(x => x == sprite);
                if (index != -1)
                    categories[i].spriteIds.RemoveAt(index);
            }
        }

        public void AddSpriteToCategory(string category, string sprite)
        {
            if (string.IsNullOrEmpty(category))
            {
                // Remove sprite from category
                RemoveSpriteFromCategory(sprite);
            }
            else
            {
                //find cateogry
                var categoryIndex = categories.FindIndex(x => x.name == category);
                var insertCategory = categoryIndex != -1 ? categories[categoryIndex] : new SpriteLibCategory() { name = category, spriteIds = new List<string>() };
                if (insertCategory.spriteIds.FindIndex(x => x == sprite) == -1)
                    insertCategory.spriteIds.Add(sprite);

                // now remove everything that has this sprite
                foreach (var cat in categories)
                {
                    if (cat.name != category)
                        cat.spriteIds.Remove(sprite);
                }
                if (categoryIndex == -1)
                    categories.Add(insertCategory);
                else
                    categories[categoryIndex] = insertCategory;
            }
        }

        public void ChangeSpriteIndex(int index, string sprite)
        {
            // find category which contain sprite
            var categoryIndex = -1;
            var spriteIndex = -1;
            for (int i = 0; i < categories.Count; ++i)
            {
                spriteIndex = categories[i].spriteIds.FindIndex(x => x == sprite);
                if (spriteIndex != -1)
                {
                    categoryIndex = i;
                    break;
                }
            }

            if (categoryIndex != -1 && spriteIndex != -1)
            {
                var cat = categories[categoryIndex];
                cat.spriteIds[spriteIndex] = cat.spriteIds[index];
                cat.spriteIds[index] = sprite;
            }
        }
    }

    public interface ISpriteLibDataProvider
    {
        SpriteLibrary GetSpriteLibrary();
        void SetSpriteLibrary(SpriteLibrary spriteLibrary);
    }
}
