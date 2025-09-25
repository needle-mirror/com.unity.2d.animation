using Unity.Burst;
using Unity.Mathematics;

namespace UnityEngine.U2D.IK
{
    [BurstCompile]
    internal static class IKMathUtility
    {
        internal const float kEpsilonNormalSqrt = 1e-15F;

        [BurstCompile]
        internal static float Angle(in float3 from, in float3 to)
        {
            // sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
            float denominator = math.sqrt(math.lengthsq(from) * math.lengthsq(to));
            if (denominator < kEpsilonNormalSqrt)
                return 0F;

            float dot = math.clamp(math.dot(from, to) / denominator, -1F, 1F);
            return math.acos(dot);
        }

        [BurstCompile]
        internal static float Angle(in float2 from, in float2 to)
        {
            // sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
            float denominator = math.sqrt(math.lengthsq(from) * math.lengthsq(to));
            if (denominator < kEpsilonNormalSqrt)
                return 0F;

            float dot = math.clamp(math.dot(from, to) / denominator, -1F, 1F);
            return math.acos(dot);
        }

        [BurstCompile]
        internal static float SignedAngle(in float3 from, in float3 to, in float3 axis)
        {
            float unsignedAngle = Angle(from, to);

            float3 cross = math.cross(from, to);
            float sign = math.sign(math.dot(axis, cross));
            return unsignedAngle * sign;
        }

        [BurstCompile]
        internal static float SignedAngle(in float2 from, in float2 to)
        {
            float unsignedAngle = Angle(from, to);
            float sign = math.sign(from.x * to.y - from.y * to.x);
            return unsignedAngle * sign;
        }
    }
}
