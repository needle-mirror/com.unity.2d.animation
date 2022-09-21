using System.Collections.Generic;
using UnityEngine.U2D.Animation;

namespace UnityEngine.U2D.IK
{
    internal class SpriteSkinVisibilityCullingStrategy : BaseCullingStrategy
    {
        public override bool Culled(IList<int> transformIds)
        {
            return !SpriteSkinVisibilityCulling.instance.IsAnyBoneInfluencingVisibleSprite(transformIds);
        }

        protected override void OnInitialize()
        {
            SpriteSkinVisibilityCulling.instance.RequestBoneVisibilityCheck(this);
        }

        protected override void OnDisable()
        {
            SpriteSkinVisibilityCulling.instance.RemoveBoneVisibilityCheckRequest(this);
        }
    }
}