using System.ComponentModel;
using System.Diagnostics;
using DrawnUI.Tutorials.NewsFeed.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace DrawnUI.Tutorials.NewsFeed;

public partial class NewsCell : DrawnListCell
{
    public NewsCell()
    {
        InitializeComponent();

        DelayIncrementMs = 75;
        TimeAnimateMs = 150;
        TimeWindowMs = 100;
    }

 
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is NewsItem news)
        {
            ConfigureForContentType(news);
        }
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        PaintPlaceholderCard?.Dispose();
        PaintPlaceholderBone?.Dispose();
    }

    private SKPaint PaintPlaceholderCard;
    private SKPaint PaintPlaceholderBone;

    // Skeleton drawn for the frames this cell is not prepared yet (bound+measured off-thread by the
    // prepared-views pipeline): white card + gray bones mimicking the real layout (avatar, author/time,
    // content lines). Cheap direct canvas primitives — placeholder frames must not cost anything.
    public override void DrawPlaceholder(DrawingContext context)
    {
        var scale = context.Scale;
        var margins = BackgroundLayer.Padding;

        var card = new SKRect(
            (float)(context.Destination.Left + margins.Left * scale),
            (float)(context.Destination.Top + margins.Top * scale),
            (float)(context.Destination.Right - margins.Right * scale),
            (float)(context.Destination.Bottom - margins.Bottom * scale));

        PaintPlaceholderCard ??= new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        PaintPlaceholderBone ??= new SKPaint
        {
            Color = SKColor.Parse("#E2E2E2"), Style = SKPaintStyle.Fill, IsAntialias = true
        };

        var canvas = context.Context.Canvas;
        canvas.DrawRect(card, PaintPlaceholderCard);

        var pad = (float)(16 * scale); // inner content padding, mirrors XAML
        var left = card.Left + pad;
        var top = card.Top + pad;
        var right = card.Right - pad;
        var corner = (float)(4 * scale);

        // avatar circle 40x40
        var r = (float)(20 * scale);
        canvas.DrawCircle(left + r, top + r, r, PaintPlaceholderBone);

        // author + time bones
        var textLeft = left + r * 2 + (float)(8 * scale);
        canvas.DrawRoundRect(new SKRect(textLeft, top + (float)(4 * scale),
            textLeft + (float)(120 * scale), top + (float)(16 * scale)), corner, corner, PaintPlaceholderBone);
        canvas.DrawRoundRect(new SKRect(textLeft, top + (float)(22 * scale),
            textLeft + (float)(60 * scale), top + (float)(32 * scale)), corner, corner, PaintPlaceholderBone);

        // content lines, last one shorter; stop when running out of card
        var y = top + r * 2 + (float)(12 * scale);
        var lineH = (float)(12 * scale);
        for (int i = 0; i < 3 && y + lineH < card.Bottom - pad; i++)
        {
            var w = (i == 2) ? (right - left) * 0.6f : (right - left);
            canvas.DrawRoundRect(new SKRect(left, y, left + w, y + lineH), corner, corner, PaintPlaceholderBone);
            y += (float)(20 * scale);
        }
    }

    private void ConfigureForContentType(NewsItem news)
    {
        // Reset all content visibility
        HideAllContent();

        // Configure common elements

        //DebugId.Text = $"{news.Id}"; //for debugging

        AuthorLabel.Text = news.AuthorName;
        TimeLabel.Text = GetRelativeTime(news.PublishedAt);
        AvatarImage.Source = news.AuthorAvatarUrl;
        LikeButton.Text = $"👍 {news.LikesCount}";
        CommentButton.Text = $"💬 {news.CommentsCount}";

        // Configure based on content type
        switch (news.Type)
        {
            case NewsType.Text:
                ConfigureTextPost(news);
                break;

            case NewsType.Image:
                ConfigureImagePost(news);
                break;

            case NewsType.Video:
                ConfigureVideoPost(news);
                break;

            case NewsType.Article:
                ConfigureArticlePost(news);
                break;

            case NewsType.Ad:
                ConfigureAdPost(news);
                break;
        }
    }

    private void HideAllContent()
    {
        TitleLabel.IsVisible = false;
        ContentLabel.IsVisible = false;
        ContentImage.IsVisible = false;
        VideoLayout.IsVisible = false;
        ArticleLayout.IsVisible = false;
        AdLayout.IsVisible = false;
    }

    private void ConfigureTextPost(NewsItem news)
    {
        if (!string.IsNullOrEmpty(news.Title))
        {
            TitleLabel.Text = news.Title;
            TitleLabel.IsVisible = true;
        }

        ContentLabel.Text = news.Content;
        ContentLabel.IsVisible = true;
    }

    private void ConfigureImagePost(NewsItem news)
    {
        ContentImg.Source = news.ImageUrl;
        ContentImage.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureVideoPost(NewsItem news)
    {
        ContentImg.Source = ExtractVideoThumbnail(news.VideoUrl);
        ContentImage.IsVisible = true;
        VideoLayout.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureArticlePost(NewsItem news)
    {
        ArticleThumbnail.Source = news.ImageUrl;
        ArticleTitle.Text = news.Title;
        ArticleDescription.Text = news.Content;
        ArticleLayout.IsVisible = true;
    }

    private void ConfigureAdPost(NewsItem news)
    {
        AdImage.Source = news.ImageUrl;
        AdTitle.Text = news.Title;
        AdLayout.IsVisible = true;
    }

    private string GetRelativeTime(DateTime publishedAt)
    {
        var delta = DateTime.Now - publishedAt;
        return delta.TotalDays >= 1
            ? publishedAt.ToString("MMM dd")
            : delta.TotalHours >= 1
                ? $"{(int)delta.TotalHours}h"
                : $"{(int)delta.TotalMinutes}m";
    }

    private string ExtractVideoThumbnail(string videoUrl)
    {
        // Extract thumbnail from video URL or use placeholder
        return videoUrl; // For now, just use the same URL as it's from Picsum
    }

    private void NewsCell_OnTapped(object sender, ControlTappedEventArgs e)
    {
        if (BindingContext is NewsItem ctx)
        {
            Debug.WriteLine($"[TAPPED] cell {ctx.Id} {ctx.AuthorName}");
        }
    }
}
