using UnityEngine;

namespace Domi.Utilities
{
	/// <summary>
	/// Convenience accessor for rapid auto-instanced <see cref="DomiContactBus"/> singleton-like usage. <br/>
	/// </summary>
	/// <remarks>
	/// Avoid relying on this in production: the singleton-like behavior can hide lifecycle ownership,
	/// cause unexpected state sharing, and make disposal/order-of-init issues harder to track. <br/>
	/// Prefer injecting your own <see cref="DomiContactBus"/> instance, since it is designed to be created and disposed deterministically. <br/>
	/// Alternatively, use <see cref="DomiContactsBootstrap"/> to manage a shared instance with explicit lifecycle control.
	/// </remarks>
	public sealed class DomiContactsStaticHandle : MonoBehaviour
	{
		static GameObject handle;
		static DomiContactBus currentInstance;
		public static DomiContactBus Service
		{
			get
			{
				if (currentInstance != null && handle != null)
					return currentInstance;
				handle = new("DomiContactsHandle", typeof(DomiContactsStaticHandle)) { hideFlags = HideFlags.HideAndDontSave };
				return currentInstance = new DomiContactBus(new DomiContactBus.Settings { autoPlay = true, emitStayEvents = false });
			}
			private set => currentInstance = value;
		}
		void OnDestroy() => Service?.Dispose();
	}
}