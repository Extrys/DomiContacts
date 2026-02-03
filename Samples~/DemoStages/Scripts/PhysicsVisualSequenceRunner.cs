using System.Collections;
using UnityEngine;

namespace Domi.DemoStages
{
	public sealed class PhysicsVisualSequenceRunner : MonoBehaviour
	{
		private void Start()
		{
			Physics.defaultMaxDepenetrationVelocity = 0;
			var allRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (var rb in allRigidbodies)
			{
				rb.maxDepenetrationVelocity = .2f;
				rb.sleepThreshold = 0;
			}
		}
	}
}