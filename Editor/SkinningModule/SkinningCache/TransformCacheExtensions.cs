using System.Linq;

namespace UnityEditor.U2D.Animation
{
    internal static class TransformCacheExtensions
    {
        internal static bool IsDescendant<T>(this T transform, T ancestor) where T : TransformCache
        {
            if (ancestor != null)
            {
                TransformCache parent = transform.parent;

                while (parent != null)
                {
                    if (parent == ancestor)
                        return true;

                    parent = parent.parent;
                }
            }

            return false;
        }

        static bool IsDescendant<T>(this T transform, T[] ancestors) where T : TransformCache
        {
            return ancestors.FirstOrDefault(t => transform.IsDescendant<T>(t)) != null;
        }

        internal static T[] FindRoots<T>(this T[] transforms) where T : TransformCache
        {
            return transforms.Where(t => t.IsDescendant(transforms) == false).ToArray();
        }

        internal static T FindRoot<T>(this T transform, T[] transforms) where T : TransformCache
        {
            T[] roots = transforms.FindRoots<T>();
            return roots.FirstOrDefault(r => transform == r || IsDescendant<T>(transform, r));
        }
    }
}
