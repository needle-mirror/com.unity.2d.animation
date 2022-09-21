using System.Collections.Generic;

namespace UnityEngine.U2D.IK
{
    internal class DisabledCullingStrategy : BaseCullingStrategy
    {
        public override bool Culled(IList<int> transformIds) => false;
    }
}
