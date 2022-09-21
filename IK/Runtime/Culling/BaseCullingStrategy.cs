using System;
using System.Collections.Generic;

namespace UnityEngine.U2D.IK
{
    /// <summary>
    /// Base class used for defining culling strategies for IKManager2D.
    /// </summary>
    internal abstract class BaseCullingStrategy
    {
        protected IKManager2D m_IkManager2D;

        public void Initialize(IKManager2D ikManager2D)
        {
            m_IkManager2D = ikManager2D;
            
            OnInitialize();
        }
        
        /// <summary>
        /// Used to check if any bone is influencing a visible SpriteSkin
        /// </summary>
        /// <param name="transformIds">A collection of bones' transform ids.</param>
        /// <returns>True if bones are culled.</returns>
        public abstract bool Culled(IList<int> transformIds);

        public void Disable()
        {
            OnDisable();
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnDisable() { }
    }
}
