using System;

namespace Domi.LowLevel
{
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEngine.LowLevel;
	using UnityEngine.PlayerLoop;

	/// <summary>
	/// Injects a FixedUpdate-end hook once, and dispatches queued contact events for all registered buses.
	/// </summary>
	internal static class DomiContactLoop
	{
		static bool injected;
		static readonly List<DomiContactBus> buses = new(8);

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStatics()
		{
			injected = false;
			buses.Clear();
		}

		internal static void Register(DomiContactBus bus)
		{
			if (bus == null) return;
			if (!buses.Contains(bus))
				buses.Add(bus);
		}

		internal static void Unregister(DomiContactBus bus)
		{
			if (bus == null) return;
			int idx = buses.IndexOf(bus);
			if (idx >= 0) buses.RemoveAt(idx);
		}

		internal static void EnsureInjected()
		{
			if (injected) return;
			injected = true;

			var loop = PlayerLoop.GetCurrentPlayerLoop();
			InjectAtEndOfFixedUpdate(ref loop, DispatchAll);
			PlayerLoop.SetPlayerLoop(loop);
		}

		static void DispatchAll()
		{
			for (int i = 0; i < buses.Count; i++)
				buses[i].DispatchQueuedEvents();
		}

		static void InjectAtEndOfFixedUpdate(ref PlayerLoopSystem root, PlayerLoopSystem.UpdateFunction fn)
		{
			for (int i = 0; i < root.subSystemList.Length; i++)
			{
				ref var sys = ref root.subSystemList[i];
				if (sys.type != typeof(FixedUpdate))
					continue;

				var list = sys.subSystemList ?? Array.Empty<PlayerLoopSystem>();
				var newList = new PlayerLoopSystem[list.Length + 1];
				Array.Copy(list, newList, list.Length);

				newList[list.Length] = new PlayerLoopSystem
				{
					type = typeof(DomiContactLoop),
					updateDelegate = fn
				};

				sys.subSystemList = newList;
				return;
			}
		}
	}
}