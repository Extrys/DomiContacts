using Unity.Mathematics;


namespace Domi.LowLevel
{
	internal struct BodyState
	{
		public bool touching;

		// last known manifold
		public int lastPointCount;
		public float3 lastPoint;
		public float3 lastNormal;
		public float3 lastImpulse;

		public void SetManifold(int pointCount, float3 p, float3 n, float3 imp)
		{
			lastPointCount = pointCount;
			lastPoint = p;
			lastNormal = n;
			lastImpulse = imp;
		}
	}
}

