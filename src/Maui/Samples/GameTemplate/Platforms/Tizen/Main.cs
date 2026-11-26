using System;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Breakout;

class Program : MauiApplication
{
	protected override MauiApp CreateMauiApp() => GameTemplate.MauiProgram.CreateMauiApp();

	static void Main(string[] args)
	{
		var app = new Program();
		app.Run(args);
	}
}
