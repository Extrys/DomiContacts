using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Domi.LowLevel;
using Domi.Utilities;


namespace Domi
{
	/// <summary>
	/// Emits events for bodyEntityId pairs <br/>
	/// Maintains minimal state to detect Enter/Exit and calculate aggregated manifold
	/// </summary>
	public sealed class DomiContactBus
	{
		// --- Public API ---
		public event ContactPairHandler OnPairEvent;
		public delegate void ContactPairHandler(in DomiContactEvent e);
		public DomiContactBus(Settings settings) 
		{
			this.settings = settings;

			_results = new NativeList<ContactHeaderEvaluationResult>(256, Allocator.Persistent);
			_eventQueue = new NativeQueue<DomiContactEvent>(Allocator.Persistent);

			DomiContactLoop.EnsureInjected();
			DomiContactLoop.Register(this);

			if (settings.autoPlay)
				Play();
		}
		public void Play(bool supressWaring = false)
		{
			if (isPlaying)
			{
				if (!supressWaring)
					Debug.LogWarning($"[DomiContactBus] Is Already playing.");
				return;
			}
			Physics.ContactEvent += OnContactEvent;
			isPlaying = true;
		}

		public void Stop()
		{
			if (!isPlaying)
			{
				Debug.LogWarning($"[DomiContactBus] Not playing.");
				return;
			}
			Physics.ContactEvent -= OnContactEvent;
			isPlaying = false;
		}

		public void Dispose()
		{
			if (isPlaying)
				Stop();
			if (_results.IsCreated)
				_results.Dispose();
		}

		// --- Internal state ---

		Settings settings;
		
		[Serializable]
		public struct Settings
		{
			public bool autoPlay;
			public bool emitStayEvents;
		}

		bool isPlaying;
		readonly Dictionary<BodyPairKey, BodyState> states = new(4096); // current states
		readonly Dictionary<int, HashSet<BodyPairKey>> pairsByBodyId = new(4096); // used for optimal incomplete exit handling
		readonly HashSet<int> perCallbackSeenBodies = new(4096);
		NativeList<ContactHeaderEvaluationResult> _results; // reusable job output
		NativeQueue<DomiContactEvent> _eventQueue;
		const int kBatchSize = 32; // size of job batches

		void OnContactEvent(PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly headerArray)
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			Profiler.BeginSample("[RigidbodyContactBus JOBIFIED] OnContactEvent");
#endif
			int headerCount = headerArray.Length;
			if (headerCount == 0)
			{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Profiler.EndSample();
#endif
				return;
			}

			_results.ResizeUninitialized(headerCount);
			var results = _results.AsArray();

			new EvaluateContactHeadersJob
			{
				headers = headerArray,
				results = results
			}.Schedule(headerCount, kBatchSize).Complete();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			int overflowHeaders = 0;
			int maxExitKeys = 0;
#endif

			perCallbackSeenBodies.Clear();
			for (int i = 0; i < headerCount; i++)
			{
#if UNITY_6000_3_OR_NEWER
				int a = headerArray[i].bodyEntityId;
				int b = headerArray[i].otherBodyEntityId;
#else
				int a = headerArray[i].bodyInstanceID;
				int b = headerArray[i].otherBodyInstanceID;
#endif
				if (a != 0) perCallbackSeenBodies.Add(a);
				if (b != 0) perCallbackSeenBodies.Add(b);
			}

			for (int i = 0; i < headerCount; i++)
			{
				var header = headerArray[i];
				var r = results[i];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				if (r.exitOverflow != 0)
				{
					overflowHeaders++;
					if (r.exitKeyCount > maxExitKeys) maxExitKeys = r.exitKeyCount;
				}
#endif

#if UNITY_6000_3_OR_NEWER
				int bodyIdA = header.bodyEntityId;
				int bodyIdB = header.otherBodyEntityId;
#else
				int bodyIdA = header.bodyInstanceID;
				int bodyIdB = header.otherBodyInstanceID;
#endif


				bool anyExit = r.anyExit != 0;
				bool anyManifold = r.anyManifold != 0;

				bool hasSideA = r.hasColliderSideA != 0;
				bool hasSideB = r.hasColliderSideB != 0;

				bool missingSideA = (bodyIdA == 0) && !hasSideA;
				bool missingSideB = (bodyIdB == 0) && !hasSideB;

				// Incomplete Exit: without components is not posible to filter "only invalids" => its needed to close all for the valid body
				if (anyExit && (missingSideA || missingSideB))
				{
					if (!missingSideA && bodyIdA != 0)
						CloseAllInvalidPairsForBody_SeenFilter(bodyIdA, perCallbackSeenBodies, DomiContactExitReason.IncompleteExitEvent);

					if (!missingSideB && bodyIdB != 0)
						CloseAllInvalidPairsForBody_SeenFilter(bodyIdB, perCallbackSeenBodies, DomiContactExitReason.IncompleteExitEvent);

					continue;
				}

				if (bodyIdA == 0 || bodyIdB == 0)
					continue;

				var key = new BodyPairKey(bodyIdA, bodyIdB);
				states.TryGetValue(key, out var st);

				// Update aggregated manifold (if exists)
				int pointCount = r.pointCount;
				float3 avgP = default;
				float3 avgN = default;
				float3 impSum = default;

				if (pointCount > 0)
				{
					float inv = 1f / pointCount;
					avgP = r.posSum * inv;
					avgN = math.normalizesafe(r.nrmSum * inv);
					impSum = r.impSum;
					st.lastPointCount = pointCount;
					st.lastPoint = avgP;
					st.lastNormal = avgN;
					st.lastImpulse = impSum;
				}

				// Enter
				if (!st.touching && anyManifold)
				{
					st.touching = true;
					states[key] = st;

					AddIndex(key.a, key);
					AddIndex(key.b, key);

					Enqueue(key, PairEventType.Enter, DomiContactExitReason.None, pointCount, avgP, avgN, impSum);
					continue;
				}

				// Stay (optional)
				if (st.touching && anyManifold)
				{
					states[key] = st;
					if (settings.emitStayEvents)
						Enqueue(key, PairEventType.Stay, DomiContactExitReason.None, pointCount, avgP, avgN, impSum);
				}
				else
				{
					states[key] = st;
				}

				// Exit by separation: touching with any exit flag and no manifolds
				if (st.touching && anyExit && !anyManifold)
					ClosePair(key, ref st, DomiContactExitReason.Separation);
			}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (overflowHeaders != 0)
				UnityEngine.Debug.LogWarning($"[RigidbodyContactBus] ExitKeys overflow en {overflowHeaders}/{headerCount} headers. maxExitKeys={maxExitKeys} cap={new FixedList512Bytes<int2>().Capacity}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			Profiler.EndSample();
#endif
		}


		void Enqueue(in BodyPairKey key, PairEventType type, DomiContactExitReason reason, int pointCount, float3 avgP, float3 avgN, float3 impSum)
		{
			_eventQueue.Enqueue(new DomiContactEvent(key.a, key.b, type, reason, pointCount, avgP, avgN, impSum));
		}
		internal void DispatchQueuedEvents()
		{
			var handler = OnPairEvent;
			if (handler == null)
			{
				// Still drain to avoid unbounded growth if nobody is listening.
				while (_eventQueue.TryDequeue(out _)) { }
				return;
			}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			Profiler.BeginSample("[DomiContactBus] Consumer Code");
#endif
			try
			{
				while (_eventQueue.TryDequeue(out var evData))
				{
					try { handler(in evData); }
					catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
				}
			}
			finally
			{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Profiler.EndSample();
#endif
			}
		}

		void AddIndex(int bodyId, BodyPairKey key)
		{
			if (bodyId == 0) return;
			if (!pairsByBodyId.TryGetValue(bodyId, out var set))
			{
				set = HashSetPool<BodyPairKey>.Get();
				pairsByBodyId.Add(bodyId, set);
			}
			set.Add(key);
		}

		void RemoveIndex(int bodyId, BodyPairKey key)
		{
			if (bodyId == 0) return;
			if (!pairsByBodyId.TryGetValue(bodyId, out var set)) return;

			set.Remove(key);
			if (set.Count == 0)
			{
				pairsByBodyId.Remove(bodyId);
				HashSetPool<BodyPairKey>.Release(set);
			}
		}

		void CloseAllInvalidPairsForBody_SeenFilter(int bodyId, HashSet<int> seenBodies, DomiContactExitReason reason)
		{
			if (bodyId == 0) return;
			if (!pairsByBodyId.TryGetValue(bodyId, out var set)) return;

			var tmp = ListPool<BodyPairKey>.Get();
			try
			{
				foreach (var k in set) tmp.Add(k);

				for (int i = 0; i < tmp.Count; i++)
				{
					var key = tmp[i];
					if (!states.TryGetValue(key, out var st)) continue;
					if (!st.touching) continue;

					// "other" is the one that is NOT bodyId
					int otherId = (key.a == bodyId) ? key.b : key.a;

					// If the other is still "alive" in this callback, dont close.
					// Deletion/disabling tends to not appear in headers.
					if (otherId != 0 && seenBodies.Contains(otherId))
						continue;

					ClosePair(key, ref st, reason);
				}
			}
			finally { ListPool<BodyPairKey>.Release(tmp); }
		}

		void ClosePair(BodyPairKey key, ref BodyState st, DomiContactExitReason reason)
		{
			if (!st.touching)
				return;

			st.touching = false;
			states[key] = st;

			RemoveIndex(key.a, key);
			RemoveIndex(key.b, key);

			Enqueue(key, PairEventType.Exit, reason, st.lastPointCount, st.lastPoint, st.lastNormal, st.lastImpulse);
		}
	}
}