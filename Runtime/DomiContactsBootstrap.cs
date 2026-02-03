using UnityEngine;


namespace Domi
{
	public sealed class DomiContactsBootstrap : MonoBehaviour
	{
		[SerializeField] DomiContactBus.Settings settings = new DomiContactBus.Settings { autoPlay = true, emitStayEvents = false };
		public DomiContactBus Service { get; private set; }
		void Awake() => Service = new DomiContactBus(settings);
		void OnEnable() => Service?.Play(settings.autoPlay);
		void OnDisable() => Service?.Stop();
		void OnDestroy() => Service?.Dispose();
	}
}

 