using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.U2D;

namespace UnityEditor.Experimental.U2D.Animation
{
    public interface ICharacterDataProvider
    {
        CharacterData GetCharacterData();
        void SetCharacterData(CharacterData characterData);
    }

    [Serializable]
    public struct CharacterData
    {
        public SpriteBone[] bones;
        public CharacterPart[] parts;
        public Vector2Int dimension;
        public CharacterGroup[] characterGroups;
    }

    [Serializable]
    public struct CharacterGroup
    {
        public string name;
        public int parentGroup;
    }

    [Serializable]
    public struct CharacterPart
    {
        public RectInt spritePosition;
        public string spriteId;
        public int[] bones;
        public int parentGroup;
    }
}
