using System.Text;
using DrawnUi.Views;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Represents a node in the inspected DOM tree with absolute coordinates
    /// </summary>
    public class DomNode
    {
        public string ControlType { get; set; }
        public string? Tag { get; set; }
        public string? Text { get; set; }
        public bool IsVisible { get; set; }
        public bool CanDraw { get; set; }

        /// <summary>
        /// Absolute position and size in canvas/screen coordinates
        /// </summary>
        public SKRect AbsoluteRect { get; set; }

        /// <summary>
        /// Local position and size within parent
        /// </summary>
        public SKRect LocalRect { get; set; }

        /// <summary>
        /// Accessibility information
        /// </summary>
        public string? AccessibilityRole { get; set; }
        public string? AccessibilityLabel { get; set; }
        public string? AccessibilityHint { get; set; }
        public bool? AccessibilityIsPressed { get; set; }
        public bool? AccessibilityCanInteract { get; set; }

        /// <summary>
        /// Whether this control has a RenderTransformMatrix applied
        /// </summary>
        public bool HasTransform { get; set; }

        /// <summary>
        /// Child nodes
        /// </summary>
        public List<DomNode> Children { get; set; } = new();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{ControlType}");
            if (!string.IsNullOrEmpty(Tag))
                sb.Append($" Tag='{Tag}'");
            if (!string.IsNullOrEmpty(Text))
                sb.Append($" Text='{Text.Trim()}'");
            sb.Append($" @({AbsoluteRect.Left:F0},{AbsoluteRect.Top:F0}) {AbsoluteRect.Width:F0}x{AbsoluteRect.Height:F0}");
            if (!string.IsNullOrEmpty(AccessibilityRole))
                sb.Append($" [{AccessibilityRole}]");
            return sb.ToString();
        }

        /// <summary>
        /// Returns a hierarchical string representation of the subtree
        /// </summary>
        public string DumpTree(int indent = 0)
        {
            var sb = new StringBuilder();
            var prefix = new string(' ', indent * 2);
            sb.AppendLine($"{prefix}{this}");
            foreach (var child in Children)
            {
                sb.Append(child.DumpTree(indent + 1));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Extension methods for inspecting the DrawnUI render tree as a DOM
    /// Uses the PROVEN working RenderTree + ProcessGestures transform mechanics
    /// </summary>
    public static partial class FluentExtensions
    {
        /// <summary>
        /// Builds a DOM tree from the render tree starting at this control
        /// Uses the same transform logic as ProcessGestures() for accurate positioning
        /// </summary>
        /// <param name="root">The root control (typically Canvas.Content)</param>
        /// <param name="parentAbsoluteOffset">Absolute offset of the root's parent (use SKPoint.Empty for Canvas root)</param>
        /// <returns>Root DomNode with absolute coordinates, or null if no render tree</returns>
        public static DomNode? BuildDomFromRenderTree(this SkiaControl root, SKPoint? parentAbsoluteOffset = null)
        {
            if (root == null)
                return null;

            var offset = parentAbsoluteOffset ?? SKPoint.Empty;

            // Start with the root control itself
            var node = new DomNode
            {
                ControlType = root.GetType().Name,
                Tag = root.Tag as string,
                IsVisible = root.IsVisible,
                CanDraw = root.CanDraw,
                HasTransform = root.HasTransform,
                AbsoluteRect = new SKRect(offset.X, offset.Y, offset.X + root.DrawingRect.Width, offset.Y + root.DrawingRect.Height),
                LocalRect = root.LastDrawnAt,
                AccessibilityRole = root.AccessibilityRole,
                AccessibilityLabel = root.AccessibilityLabel,
                AccessibilityHint = root.AccessibilityHint,
                AccessibilityIsPressed = root.AccessibilityIsPressed,
                AccessibilityCanInteract = root.AccessibilityCanInteract,
            };

            // Try to get Text from SkiaLabel-derived controls
            if (root is SkiaLabel label)
            {
                node.Text = label.Text;
            }
            else if (root is SkiaButton button)
            {
                node.Text = button.Text;
            }

            // Now walk through RenderTree for direct children
            // RenderTree is a FLAT list of SkiaControlWithRect for this control's direct children
            if (root.UsesRenderingTree && root.RenderTree != null && root.RenderTree.Count > 0)
            {
                foreach (var childItem in root.RenderTree)
                {
                    if (childItem.Control == null || childItem.Control.IsDisposed || childItem.Control.IsDisposing)
                        continue;

                    // Calculate child's absolute position using the same logic as ProcessGestures/IsGestureForChild
                    // In IsGestureForChild:
                    // - If HasTransform: inverse.MapPoint(point) maps parent space -> child space
                    // - For DOM, we need child space -> absolute parent space: forward transform
                    // - If no transform: ApplyTransforms(HitRect) gives rect in parent's coordinate space

                    SKRect childAbsoluteRect;

                    if (childItem.Control.HasTransform)
                    {
                        // The child has RenderTransformMatrix (rotation, scale, skew, complex transform)
                        // childItem.Rect = destination rect (where it renders in parent space)
                        // childItem.HitRect = hit rect in child's local space
                        
                        // For complex transforms, we use the drawing rect (which is already in parent space)
                        // and offset it by our absolute position
                        childAbsoluteRect = new SKRect(
                            offset.X + childItem.Rect.Left,
                            offset.Y + childItem.Rect.Top,
                            offset.X + childItem.Rect.Right,
                            offset.Y + childItem.Rect.Bottom);
                    }
                    else
                    {
                        // No complex transform - use ApplyTransforms like IsGestureForChild does
                        // ApplyTransforms returns the rect in parent's coordinate space
                        var transformedInParent = childItem.Control.ApplyTransforms(childItem.HitRect);
                        childAbsoluteRect = new SKRect(
                            offset.X + transformedInParent.Left,
                            offset.Y + transformedInParent.Top,
                            offset.X + transformedInParent.Right,
                            offset.Y + transformedInParent.Bottom);
                    }

                    // Recurse into child if it also has a RenderTree
                    var childOrigin = new SKPoint(childAbsoluteRect.Left, childAbsoluteRect.Top);
                    var childNode = BuildDomFromRenderTree(childItem.Control, childOrigin);
                    
                    if (childNode != null)
                    {
                        // Override with computed rect (more accurate)
                        childNode.AbsoluteRect = childAbsoluteRect;
                        childNode.LocalRect = childItem.Rect;
                        node.Children.Add(childNode);
                    }
                    else
                    {
                        // Child has no render tree but is still a node
                        var simpleChildNode = new DomNode
                        {
                            ControlType = childItem.Control.GetType().Name,
                            Tag = childItem.Control.Tag as string,
                            IsVisible = childItem.Control.IsVisible,
                            CanDraw = childItem.Control.CanDraw,
                            HasTransform = childItem.Control.HasTransform,
                            AbsoluteRect = childAbsoluteRect,
                            LocalRect = childItem.Rect,
                            AccessibilityRole = childItem.Control.AccessibilityRole,
                            AccessibilityLabel = childItem.Control.AccessibilityLabel,
                        };
                        if (childItem.Control is SkiaLabel childLabel)
                            simpleChildNode.Text = childLabel.Text;
                        else if (childItem.Control is SkiaButton childBtn)
                            simpleChildNode.Text = childBtn.Text;
                            
                        node.Children.Add(simpleChildNode);
                    }
                }
            }
            else
            {
                // No RenderTree - try walking Views list instead (fallback)
                // This handles controls that have UsesRenderingTree = false like SkiaScroll
                foreach (var child in root.Views)
                {
                    if (child == null || child.IsDisposed || child.IsDisposing)
                        continue;

                    // Fallback: use LastDrawnAt + manual offset calculation
                    // Note: this won't be as accurate as RenderTree for transforms
                    var childOffset = new SKPoint(
                        offset.X + child.LastDrawnAt.Left,
                        offset.Y + child.LastDrawnAt.Top);
                    
                    var childNode = BuildDomFromRenderTree(child, childOffset);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }
            }

            return node;
        }

        /// <summary>
        /// Builds a DOM tree from Canvas.Content using RenderTree
        /// Call this AFTER at least one render pass has occurred (so RenderTree is populated)
        /// </summary>
        public static DomNode? GetDomTree(this Canvas canvas)
        {
            if (canvas == null)
                return null;

            var root = canvas.Content;
            if (root == null)
                return null;

            // Canvas coordinate space starts at (0,0)
            return BuildDomFromRenderTree(root as SkiaControl, SKPoint.Empty);
        }

        /// <summary>
        /// Dumps the DOM tree as a human-readable string
        /// </summary>
        public static string DumpDom(this DomNode? root)
        {
            if (root == null)
                return "(null DOM tree - render may not have happened yet)";

            return root.DumpTree(0);
        }

        /// <summary>
        /// Finds all nodes matching a predicate
        /// </summary>
        public static List<DomNode> FindNodes(this DomNode? root, Func<DomNode, bool> predicate)
        {
            var results = new List<DomNode>();
            if (root == null)
                return results;

            if (predicate(root))
                results.Add(root);

            foreach (var child in root.Children)
            {
                results.AddRange(child.FindNodes(predicate));
            }

            return results;
        }

        /// <summary>
        /// Finds nodes by Tag value
        /// </summary>
        public static List<DomNode> FindByTag(this DomNode? root, string tag)
        {
            return root.FindNodes(n => n.Tag == tag);
        }

        /// <summary>
        /// Finds nodes by control type name
        /// </summary>
        public static List<DomNode> FindByType<T>(this DomNode? root) where T : SkiaControl
        {
            var typeName = typeof(T).Name;
            return root.FindNodes(n => n.ControlType == typeName);
        }

        /// <summary>
        /// Finds buttons by their text (case-insensitive contains)
        /// </summary>
        public static List<DomNode> FindButtonByText(this DomNode? root, string text, bool caseSensitive = false)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return root.FindNodes(n => 
                n.ControlType == nameof(SkiaButton) && 
                n.Text != null && 
                n.Text.Contains(text, comparison));
        }

        /// <summary>
        /// Finds all clickable/interactive elements (AccessibilityCanInteract = true)
        /// </summary>
        public static List<DomNode> FindInteractive(this DomNode? root)
        {
            return root.FindNodes(n => n.AccessibilityCanInteract == true);
        }

        /// <summary>
        /// Gets the center point of a node's absolute rect (for synthetic gestures)
        /// </summary>
        public static SKPoint GetCenter(this DomNode node)
        {
            return new SKPoint(
                node.AbsoluteRect.Left + node.AbsoluteRect.Width / 2,
                node.AbsoluteRect.Top + node.AbsoluteRect.Height / 2);
        }
    }
}
