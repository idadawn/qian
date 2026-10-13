// 同名但与 Kun 无关的 Publish / Enqueue / Add 不受约束。
internal sealed class Channel
{
    private readonly List<string> _items = [];
    public void Publish<T>(T item) => _items.Add(item!.ToString()!);
    public void Enqueue(string item) => _items.Add(item);
}
internal static class Use { internal static void Run(Channel channel) { channel.Publish(1); channel.Enqueue("x"); } }
