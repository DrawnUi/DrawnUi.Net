using System.Collections;

namespace DrawnUi.Draw
{
    public class RenderingSubTree : IEnumerable<SkiaControlWithRect>
    {
        public SKPoint AdjustOffset(SKPoint point)
        {
            return new SKPoint(point.X - Offset.X, point.Y - Offset.Y);
        }

        public GestureEventProcessingInfo OffsetGestures(GestureEventProcessingInfo apply)
        {
            return new GestureEventProcessingInfo()
            {
                MappedLocation = new (apply.MappedLocation.X+Offset.X, apply.MappedLocation.Y+Offset.Y),
                ChildOffset = new(apply.ChildOffset.X + Offset.X, apply.ChildOffset.Y + Offset.Y),
                ChildOffsetDirect = new(apply.ChildOffsetDirect.X + Offset.X, apply.ChildOffsetDirect.Y + Offset.Y),
            };
        }

        public SkiaControlWithRect this[int index]
        {
            get => Tree[index];
            set => Tree[index] = value;
        }

        public Span<SkiaControlWithRect> this[Range range]
        {
            get => CollectionsMarshal.AsSpan(Tree)[range];
        }
        public int Count => Tree.Count;
        public IEnumerator<SkiaControlWithRect> GetEnumerator()
        {
            return Tree!.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Span<SkiaControlWithRect> AsSpans()
        {
            return CollectionsMarshal.AsSpan(Tree);
        }

        public RenderingSubTree()
        {
            Tree = new();
        }
        public RenderingSubTree(List<SkiaControlWithRect> tree)
        {
            Tree = tree;
        }
        public SKPoint Offset { get; set; }
        public List<SkiaControlWithRect> Tree { get; protected set; }

        public void Clear()
        {
            Tree.Clear();
        }
    }
}
