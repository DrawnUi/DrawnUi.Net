using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The value store of BindableObject (heads without MAUI): many properties set, read, cleared and set again, in an
/// order that fills, grows and empties its table, always give what a plain dictionary of the same operations gives.
/// </summary>
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
}
