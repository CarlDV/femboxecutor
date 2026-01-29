using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using XamlAnimatedGif;

namespace VelocityExecutor;

public partial class ConsoleWindow : Window, IComponentConnector
{
	public bool AllowClose { get; set; }

	public ConsoleWindow()
	{
		InitializeComponent();
		LoadBackground();
	}

	public void LoadBackground()
	{
		try
		{
			AppSettings settings = AppSettings.Load();
			if (!string.IsNullOrEmpty(settings.ConsoleBackground) && File.Exists(settings.ConsoleBackground))
			{
				Uri uri = new Uri(settings.ConsoleBackground);
				AnimationBehavior.SetSourceUri(BackgroundImage, uri);
			}
			else
			{
				AnimationBehavior.SetSourceUri(BackgroundImage, null);
			}
		}
		catch
		{
		}
	}

	public void UnloadBackground()
	{
		AnimationBehavior.SetSourceUri(BackgroundImage, null);
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!AllowClose)
		{
			e.Cancel = true;
			Hide();
		}
	}

	public void SetOpacity(double opacity)
	{
		if (base.Content is Border border)
		{
			border.Opacity = opacity;
		}
	}

	public void Log(string message)
	{
		base.Dispatcher.Invoke(delegate
		{
			string value = DateTime.Now.ToString("HH:mm:ss");
			OutputBox.AppendText($"[{value}] {message}{Environment.NewLine}");
			OutputBox.ScrollToEnd();
		});
	}

	public void Clear()
	{
		OutputBox.Clear();
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Hide();
	}
}
