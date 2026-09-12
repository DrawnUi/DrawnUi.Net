using System.Windows.Markup;

// One xmlns for the whole drawn surface, matching the URI the MAUI head uses, so XAML written for
// one head reads the same on the other:
//     xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi")]
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi.Draw")]
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi.Controls")]
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi.Views")]
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi.Models")]
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi.Infrastructure.Enums")]
[assembly: XmlnsDefinition("http://schemas.appomobi.com/drawnUi/2023/draw", "DrawnUi.Wpf")]
[assembly: XmlnsPrefix("http://schemas.appomobi.com/drawnUi/2023/draw", "draw")]
