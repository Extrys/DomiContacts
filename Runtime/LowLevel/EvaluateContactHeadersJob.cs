using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace Domi.LowLevel
{


	[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
	internal struct EvaluateContactHeadersJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<ContactPairHeader>.ReadOnly headers;
		[WriteOnly] public NativeArray<ContactHeaderEvaluationResult> results;

		public void Execute(int i)
		{
			var header = headers[i];
			int pairCount = header.pairCount;

			byte anyExit = 0;
			byte anyManifold = 0;
			byte hasSideA = 0;
			byte hasSideB = 0;

			int pointCount = 0;
			float3 pSum = float3.zero;
			float3 nSum = float3.zero;
			float3 impSum = float3.zero;

			FixedList512Bytes<int2> exitKeys = default;
			byte exitOverflow = 0;
			ushort exitKeyCount = 0;

			// pass 1: flags + exitKeys
			for (int j = 0; j < pairCount; j++)
			{
				ref readonly var p = ref header.GetContactPair(j);

#if UNITY_6000_3_OR_NEWER
				int rawA = p.colliderEntityId;
				int rawB = p.otherColliderEntityId;
#else
				int rawA = p.colliderInstanceID;
				int rawB = p.otherColliderInstanceID;
#endif

				if (rawA != 0) hasSideA = 1;
				if (rawB != 0) hasSideB = 1;

				if (!p.isCollisionExit)
					continue;

				anyExit = 1;

				// exit key solo si ambos collider ids existen
				if (rawA == 0 || rawB == 0)
					continue;

				exitKeyCount++;

				int a = rawA, b = rawB;
				if (a > b) { int t = a; a = b; b = t; }

				if (exitKeys.Length < exitKeys.Capacity) exitKeys.Add(new int2(a, b));
				else exitOverflow = 1;
			}

			// pass 2: manifolds + sums
			for (int j = 0; j < pairCount; j++)
			{
				ref readonly var p = ref header.GetContactPair(j);

				int cc = p.contactCount;
				if (cc <= 0)
					continue;

				// exclusion antes de acumular
				if (!p.isCollisionExit && anyExit != 0)
				{
#if UNITY_6000_3_OR_NEWER
					int rawA = p.colliderEntityId;
					int rawB = p.otherColliderEntityId;
#else
					int rawA = p.colliderInstanceID;
					int rawB = p.otherColliderInstanceID;
#endif

					if (rawA != 0 && rawB != 0)
					{
						int a = rawA, b = rawB;
						if (a > b) { int t = a; a = b; b = t; }

						bool excluded = false;

						if (exitOverflow == 0)
						{
							for (int e = 0; e < exitKeys.Length; e++)
							{
								var ex = exitKeys[e];
								if (ex.x == a && ex.y == b) { excluded = true; break; }
							}
						}
						else
						{
							// fallback: scan exits en header (raro)
							for (int k = 0; k < pairCount; k++)
							{
								ref readonly var exP = ref header.GetContactPair(k);
								if (!exP.isCollisionExit) continue;

#if UNITY_6000_3_OR_NEWER
								int ea = exP.colliderEntityId;
								int eb = exP.otherColliderEntityId;
#else
								int ea = exP.colliderInstanceID;
								int eb = exP.otherColliderInstanceID;
#endif

								if (ea == 0 || eb == 0) continue;
								if (ea > eb) { int t = ea; ea = eb; eb = t; }

								if (ea == a && eb == b) { excluded = true; break; }
							}
						}

						if (excluded)
							continue;
					}
				}

				anyManifold = 1;
				pointCount += cc;

				for (int k = 0; k < cc; k++)
				{
					ref readonly var cp = ref p.GetContactPoint(k);
					pSum += (float3)cp.position;
					nSum += (float3)cp.normal;
					impSum += (float3)cp.impulse;
				}
			}

			results[i] = new ContactHeaderEvaluationResult
			{
				anyExit = anyExit,
				anyManifold = anyManifold,
				hasColliderSideA = hasSideA,
				hasColliderSideB = hasSideB,
				pointCount = pointCount,
				posSum = pSum,
				nrmSum = nSum,
				impSum = impSum,
				exitOverflow = exitOverflow,
				exitKeyCount = exitKeyCount
			};
		}
	}

	internal struct ContactHeaderEvaluationResult
	{
		public byte anyExit, anyManifold;
		public byte hasColliderSideA, hasColliderSideB; // raw presence por lado (no canonical)

		public int pointCount;
		public float3 posSum, nrmSum, impSum;

		// opcional: diagnóstico de overflow
		public byte exitOverflow;
		public ushort exitKeyCount;
	}
}

