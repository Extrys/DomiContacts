using UnityEngine;
using System.Collections;
using TMPro;

namespace Domi.Debugging
{

	public sealed class ContactEventVisualizer : MonoBehaviour
	{
		[SerializeField] DomiContactBus bus;
		[SerializeField] float lifetime = 1.0f;
		[SerializeField] float riseSpeed = 0.5f;


		void OnEnable()
		{
			if (bus != null) bus.OnPairEvent += HandlePairEvent;
		}

		void OnDisable()
		{
			if (bus != null) bus.OnPairEvent -= HandlePairEvent;
		}

		void HandlePairEvent(in DomiContactEvent e)
		{
			if (e.type == PairEventType.Enter)
			{
				SpawnText($"ENTER {e.bodyA} <-> {e.bodyB}", (Vector3)e.avgPoint, true);
				return;
			}

			if (e.type == PairEventType.Exit)
			{
				SpawnText($"<size=1f>EXIT</size>\n{e.bodyA} <-> {e.bodyB}\n<size=0.7f>({e.exitReason})</size>", (Vector3)e.avgPoint, false);
				return;
			}
		}

		void SpawnText(string msg, Vector3 pos, bool enter)
		{
			var go = new GameObject("Evt");
			go.transform.position = pos;

			var tm = go.AddComponent<TextMeshPro>();
			tm.text = msg;
			tm.fontSize = 1.5f;
			tm.alignment = TextAlignmentOptions.Center;
			tm.color = enter ? Color.green : Color.red;

			var cam = Camera.main;
			if (cam != null) tm.transform.forward = cam.transform.forward;

			StartCoroutine(Animate(go.transform, tm));
		}

		IEnumerator Animate(Transform t, TextMeshPro tm)
		{
			float t0 = Time.time;
			var c = tm.color;

			while (Time.time - t0 < lifetime)
			{
				float u = (Time.time - t0) / lifetime;
				t.position += Vector3.up * (riseSpeed * Time.deltaTime);

				var cam = Camera.main;
				if (cam != null)
					tm.transform.forward = cam.transform.forward;

				c.a = 1f - u;
				tm.color = c;

				yield return null;
			}

			Destroy(t.gameObject);
		}
	}
}