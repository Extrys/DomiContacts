using System;


namespace Domi.LowLevel
{
	public readonly struct BodyPairKey : IEquatable<BodyPairKey>
	{
		public readonly int a, b;
		public BodyPairKey(int a, int b)
		{
			if (a <= b) { this.a = a; this.b = b; }
			else { this.a = b; this.b = a; }
		}
		public bool Equals(BodyPairKey other) => a == other.a && b == other.b;
		public override bool Equals(object obj) => obj is BodyPairKey other && Equals(other);
		public override int GetHashCode() => unchecked(a * 73856093) ^ b;
	}
}

