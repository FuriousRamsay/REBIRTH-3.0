using System.Collections.Generic;

#nullable disable

/// <summary>
/// Small caller-locked round-robin set. Removing an id removes its physical queue node,
/// so active-count bounds also bound retained scheduling metadata.
/// </summary>
internal sealed class RebirthNpcFairRoundRobin<TKey>
{
    private readonly LinkedList<TKey> order = new LinkedList<TKey>();
    private readonly Dictionary<TKey, LinkedListNode<TKey>> nodes =
        new Dictionary<TKey, LinkedListNode<TKey>>();

    public int Count { get { return nodes.Count; } }

    public bool Add(TKey key)
    {
        if (nodes.ContainsKey(key)) return false;
        nodes[key] = order.AddLast(key);
        return true;
    }

    public bool Remove(TKey key)
    {
        LinkedListNode<TKey> node;
        if (!nodes.TryGetValue(key, out node)) return false;
        nodes.Remove(key);
        order.Remove(node);
        return true;
    }

    public bool TryTake(out TKey key)
    {
        LinkedListNode<TKey> node = order.First;
        if (node == null)
        {
            key = default(TKey);
            return false;
        }
        key = node.Value;
        order.RemoveFirst();
        nodes.Remove(key);
        return true;
    }

    public void Return(TKey key)
    {
        Add(key);
    }

    public void Clear()
    {
        nodes.Clear();
        order.Clear();
    }
}
