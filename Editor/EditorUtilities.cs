using System;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal static class EditorUtilities
    {
        /// <summary>
        /// Checks if element exists in array independent of the order of X & Y. 
        /// </summary>
        public static bool ContainsAny(this Vector2Int[] array, Vector2Int element)
        {
            return Array.FindIndex(array, e =>
                (e.x == element.x && e.y == element.y) ||
                (e.y == element.x && e.x == element.y)) != -1;
        }          
    }
}