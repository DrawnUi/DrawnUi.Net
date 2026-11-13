using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The value store of BindableObject (heads without MAUI): many properties set, read, cleared and set again, in an
/// order that fills, grows and empties its table, always give what a plain dictionary of the same operations gives.
/// Reads take no lock: a read on another thread while values are set, cleared and the table grows gets the property's
/// own value or its default, never another property's.
/// </summary>
[Collection("Multithreaded")]
public class BindableValuesTests
{
    sealed class Bag : BindableObject
    {
    }

    [Fact]
    public void Values_follow_every_set_and_clear()
    {
        var properties = Enumerable.Range(0, 300)
            .Select(i => BindableProperty.Create($"P{i}", typeof(int), typeof(Bag), i % 3 == 0 ? -1 : null))
            .ToArray();
        var bag = new Bag();
        var expected = new Dictionary<BindableProperty, int>();
        var random = new Random(7);

        for (var step = 0; step < 20_000; step++)
        {
            var p = properties[random.Next(properties.Length)];
            switch (random.Next(3))
            {
                case 0:
                case 1:
                    bag.SetValue(p, step);
                    expected[p] = step;
                    break;
                default:
                    bag.ClearValue(p);
                    expected.Remove(p);
                    break;
            }

            if (step % 97 == 0)
            {
                foreach (var q in properties)
                {
                    var set = expected.TryGetValue(q, out var value);
                    Assert.Equal(set, bag.IsSet(q));
                    Assert.Equal(set ? value : q.DefaultValue, bag.GetValue(q));
                }
            }
        }
    }

    [Fact]
    public void Reads_on_other_threads_never_get_another_propertys_value()
    {
        const int Count = 64;
        var properties = Enumerable.Range(0, Count)
            .Select(i => BindableProperty.Create($"T{i}", typeof(int), typeof(Bag), -(i + 1)))
            .ToArray();
        var wrong = 0;
        var reads = 0L;

        for (var round = 0; round < 20; round++)
        {
            var bag = new Bag();
            var stop = false;
            var readers = Enumerable.Range(0, 3).Select(r => Task.Run(() =>
            {
                var random = new Random(r);
                while (!Volatile.Read(ref stop))
                {
                    var i = random.Next(Count);
                    // a value of property i is i * 1_000_000 + n, its default -(i + 1)
                    if (bag.GetValue(properties[i]) is not int v || (v != -(i + 1) && v / 1_000_000 != i))
                        Interlocked.Increment(ref wrong);
                    Interlocked.Increment(ref reads);
                }
            })).ToArray();

            var writer = new Random(100 + round);
            for (var step = 0; step < 20_000; step++)
            {
                var i = writer.Next(Count);
                if (writer.Next(4) == 0)
                    bag.ClearValue(properties[i]);
                else
                    bag.SetValue(properties[i], i * 1_000_000 + step);
            }

            Volatile.Write(ref stop, true);
            Task.WaitAll(readers);
        }

        Assert.True(reads > 0);
        Assert.Equal(0, wrong);
    }
}
