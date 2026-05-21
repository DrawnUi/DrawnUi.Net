namespace DrawnUi.Views
{
    public partial class Canvas
    {
        private SkiaControl hasHover;
        private bool _hadHover;
        private bool _checkHover;

        public SkiaControl HasHover
        {
            get => hasHover;
            set
            {
                _hadHover = true;

                if (Equals(value, hasHover))
                {
                    return;
                }

                if (hasHover is SkiaControl previous)
                {
                    previous.IsHovered = previous.OnHover(false);
                }

                hasHover = value;
                if (hasHover is SkiaControl next)
                {
                    next.IsHovered = next.OnHover(true);
                }

                OnPropertyChanged();
            }
        }
    }
}
