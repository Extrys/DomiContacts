using System.Collections.Generic;


namespace Domi.Utilities
{
	static class HashSetPool<T>
	{
		static readonly Stack<HashSet<T>> pool = new Stack<HashSet<T>>(64);
		internal static HashSet<T> Get() => pool.Count > 0 ? pool.Pop() : new HashSet<T>();
		internal static void Release(HashSet<T> set) { set.Clear(); pool.Push(set); }
	}
	static class ListPool<T>
	{
		static readonly Stack<List<T>> pool = new(64);
		internal static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(128);
		internal static void Release(List<T> list) { list.Clear(); pool.Push(list); }
	}
}

