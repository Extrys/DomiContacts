using Unity.Mathematics;
using Domi.LowLevel;

namespace Domi
{
	public readonly struct DomiContactEvent
	{
		public readonly int bodyA;      // canonical: min(bodyIdA, bodyIdB)
		public readonly int bodyB;      // canonical: max(...)
		public readonly PairEventType type;
		public readonly DomiContactExitReason exitReason; // only relevant for Exit events (Maybe i can remove or generalize this?)
		public readonly int pointCount;
		public readonly float3 avgPoint;
		public readonly float3 avgNormal;
		public readonly float3 impulseSum;

		public BodyPairKey Key => new(bodyA, bodyB);

		public DomiContactEvent(int bodyA, int bodyB, PairEventType type, DomiContactExitReason exitReason, int pointCount, float3 avgPoint, float3 avgNormal, float3 impulseSum)
		{
			this.bodyA = bodyA;
			this.bodyB = bodyB;
			this.type = type;
			this.exitReason = exitReason;
			this.pointCount = pointCount;
			this.avgPoint = avgPoint;
			this.avgNormal = avgNormal;
			this.impulseSum = impulseSum;
		}
	}

	public enum PairEventType : byte { Enter, Stay, Exit }

	public enum DomiContactExitReason : byte
	{
		None = 0,
		Separation = 1,
		IncompleteExitEvent = 2,
		ForcedSeparation = 3
	}
}

