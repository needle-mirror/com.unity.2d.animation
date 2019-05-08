using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.U2D.Animation;

namespace UnityEditor.Experimental.U2D.Animation.Test.SpriteLibraryComponentTests
{
    public class SpriteLibraryComponentTests
    {
        SpriteLibraryAsset m_SpriteLibrary;
        List<Sprite> m_Sprites;
        Texture2D m_Texture;
        GameObject m_GameObject;
        SpriteLibraryComponent m_Component;

        [OneTimeSetUp]
        public void Setup()
        {
            m_Texture = new Texture2D(64, 64);
            m_SpriteLibrary = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
            m_Sprites = new List<Sprite>()
            {
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero),
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero),
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero),
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero),
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero),
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero),
                Sprite.Create(m_Texture, new Rect(0, 0, 64, 64), Vector2.zero)
            };

            m_SpriteLibrary.entries = new List<LibEntry>()
            {
                new LibEntry()
                {
                    category = "3Sprites",
                    categoryHash = SpriteLibraryAsset.GetCategoryHash("3Sprites"),
                    spriteList = new List<Sprite>()
                    {
                        m_Sprites[0],m_Sprites[1], m_Sprites[2]
                    }
                },

                new LibEntry()
                {
                    category = "2Sprites",
                    categoryHash = SpriteLibraryAsset.GetCategoryHash("2Sprites"),
                    spriteList = new List<Sprite>()
                    {
                        m_Sprites[3],m_Sprites[4]
                    }
                },

                new LibEntry()
                {
                    category = "0Sprites",
                    categoryHash = SpriteLibraryAsset.GetCategoryHash("0Sprites"),
                    spriteList = new List<Sprite>()
                }
            };

            m_GameObject = new GameObject("Test");
            m_Component = m_GameObject.AddComponent<SpriteLibraryComponent>();
            m_Component.spriteLib = m_SpriteLibrary;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_SpriteLibrary);
            m_SpriteLibrary = null;
            foreach (var sprite in m_Sprites)
                Object.DestroyImmediate(sprite);
            m_Sprites = null;
            Object.DestroyImmediate(m_Texture);
            m_Texture = null;
            Object.DestroyImmediate(m_GameObject);
        }

        [Test]
        [TestCase("3Sprites", 0, 3)]
        [TestCase("2Sprites", 3, 2)]
        [TestCase("0Sprites", 5, 0)]
        public void GetSpriteByCategoryNameReturnsCorrectSprite(string categoryName, int spriteListStartIndex, int spriteCount)
        {
            for (int i = 0; i < spriteCount; ++i)
            {
                var sprite = m_Component.GetSprite(categoryName, i);
                Assert.NotNull(sprite);
                Assert.AreEqual(m_Sprites[spriteListStartIndex + i], sprite);
            }

            Assert.IsNull(m_Component.GetSprite(categoryName, spriteCount + 1));
        }

        [Test]
        [TestCase("3Sprites", 0, 3)]
        [TestCase("2Sprites", 3, 2)]
        [TestCase("0Sprites", 5, 0)]
        public void GetSpriteByCategoryHashReturnsCorrectSprite(string categoryName, int spriteListStartIndex, int spriteCount)
        {
            for (int i = 0; i < spriteCount; ++i)
            {
                string categoryNameActual = "";
                var sprite = m_Component.GetSprite(SpriteLibraryAsset.GetCategoryHash(categoryName), i, ref categoryNameActual);
                Assert.NotNull(sprite);
                Assert.AreEqual(m_Sprites[spriteListStartIndex + i], sprite);
                Assert.AreEqual(categoryName, categoryNameActual);
            }

            Assert.IsNull(m_Component.GetSprite(categoryName, spriteCount + 1));
        }

        [Test]
        [TestCase("3Sprites", 0)]
        [TestCase("2Sprites", 3)]
        [TestCase("0Sprites", -1)]
        public void GetSpriteByOverridesReturnsCorrectSprite(string categoryName, int spriteListStartIndex)
        {
            m_Component.AddOverrides(m_Sprites[5], categoryName, 0);
            var sprite = m_Component.GetSprite(categoryName, 0);
            Assert.NotNull(sprite);
            Assert.AreEqual(m_Sprites[5], sprite);

            // Removing it
            m_Component.RemoveOverrides(categoryName, 0);

            sprite = m_Component.GetSprite(categoryName, 0);
            if (spriteListStartIndex == -1)
                Assert.IsNull(sprite);
            else
            {
                Assert.NotNull(sprite);
                Assert.AreEqual(m_Sprites[spriteListStartIndex], sprite);
            }
        }
    }

}