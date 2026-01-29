using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using VelocityExecutor.Services;
using XamlAnimatedGif;

namespace VelocityExecutor;

public partial class SettingsWindow : Window, IComponentConnector
{
	private AppSettings _settings;

	private bool _isLoaded;

	public bool AllowClose { get; set; }

	public event EventHandler SettingsUpdated;

	public event EventHandler ConsoleBackgroundChanged;

	public SettingsWindow(AppSettings settings)
	{
		InitializeComponent();
		_settings = settings;
		LoadSettings();
		LoadBackground();
		_isLoaded = true;
	}


	public void LoadBackground()
	{
		try
		{
			if (!string.IsNullOrEmpty(_settings.SettingsBackground) && File.Exists(_settings.SettingsBackground))
			{
				Uri uri = new Uri(_settings.SettingsBackground);
				AnimationBehavior.SetSourceUri(BackgroundImage, uri);
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

	private void LoadSettings()
	{
		TopMostCheck.IsChecked = _settings.TopMost;
		AutoAttachCheck.IsChecked = _settings.AutoAttach;
		AutoUpdateCheck.IsChecked = _settings.AutoUpdate;
		RpcEnabledCheck.IsChecked = _settings.RpcEnabled;
		ShowGameCheck.IsChecked = _settings.ShowGameInRpc;
		RpcDetailsInput.Text = _settings.RpcDetails;
		RpcStateInput.Text = _settings.RpcState;
		BgPathText.Text = Path.GetFileName(_settings.BackgroundImagePath);
		SettingsBgPathText.Text = (string.IsNullOrEmpty(_settings.SettingsBackground) ? "Default" : Path.GetFileName(_settings.SettingsBackground));
		ConsoleBgPathText.Text = (string.IsNullOrEmpty(_settings.ConsoleBackground) ? "Default" : Path.GetFileName(_settings.ConsoleBackground));
		AccentColorInput.Text = _settings.BorderColor;
		AccentColorInput.Text = _settings.BorderColor;
		WindowOpacitySlider.Value = _settings.WindowOpacity;
		if (UserIdDisplay != null)
		{
			UserIdDisplay.Text = _settings.AnalyticsUserId;
		}
	}

	private void SettingChanged(object sender, RoutedEventArgs e)
	{
	}

	private void Palette_Click(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button { Background: SolidColorBrush { Color: var color } })
		{
			string hex = color.ToString();
			AccentColorInput.Text = hex;
		}
	}

	private void CustomColor_Click(object sender, RoutedEventArgs e)
	{
		ColorDialog dialog = new ColorDialog();
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			System.Drawing.Color dColor = dialog.Color;
			System.Windows.Media.Color wpfColor = System.Windows.Media.Color.FromRgb(dColor.R, dColor.G, dColor.B);
			AccentColorInput.Text = wpfColor.ToString();
		}
	}

	private void PickBackground_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Media Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
		};
		if (dialog.ShowDialog() == true)
		{
			_settings.BackgroundImagePath = dialog.FileName;
			BgPathText.Text = Path.GetFileName(dialog.FileName);
		}
	}

	private void ClearMainBg_Click(object sender, RoutedEventArgs e)
	{
		_settings.BackgroundImagePath = "Images/background.png";
		BgPathText.Text = "Default";
	}

	private void PickSettingsBg_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Media Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
		};
		if (dialog.ShowDialog() == true)
		{
			_settings.SettingsBackground = dialog.FileName;
			SettingsBgPathText.Text = Path.GetFileName(dialog.FileName);
			try
			{
				Uri uri = new Uri(dialog.FileName);
				AnimationBehavior.SetSourceUri(BackgroundImage, uri);
			}
			catch
			{
			}
		}
	}

	private void ClearSettingsBg_Click(object sender, RoutedEventArgs e)
	{
		_settings.SettingsBackground = "";
		SettingsBgPathText.Text = "Default";
		AnimationBehavior.SetSourceUri(BackgroundImage, null);
	}

	private void PickConsoleBg_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Media Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
		};
		if (dialog.ShowDialog() == true)
		{
			_settings.ConsoleBackground = dialog.FileName;
			ConsoleBgPathText.Text = Path.GetFileName(dialog.FileName);
			this.ConsoleBackgroundChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ClearConsoleBg_Click(object sender, RoutedEventArgs e)
	{
		_settings.ConsoleBackground = "";
		ConsoleBgPathText.Text = "Default";
		this.ConsoleBackgroundChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ApplySettings()
	{
		_settings.TopMost = TopMostCheck.IsChecked == true;
		_settings.AutoAttach = AutoAttachCheck.IsChecked == true;
		_settings.AutoUpdate = AutoUpdateCheck.IsChecked == true;
		_settings.RpcEnabled = RpcEnabledCheck.IsChecked ?? true;
		_settings.ShowGameInRpc = ShowGameCheck.IsChecked ?? true;
		_settings.RpcDetails = RpcDetailsInput.Text;
		_settings.RpcState = RpcStateInput.Text;
		_settings.WindowOpacity = WindowOpacitySlider.Value;
		if (!string.IsNullOrEmpty(AccentColorInput.Text))
		{
			_settings.BorderColor = AccentColorInput.Text;
		}
		_settings.Save();
		this.SettingsUpdated?.Invoke(this, EventArgs.Empty);
	}

	private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (WindowOpacityValue != null)
		{
			WindowOpacityValue.Text = $"{(int)(WindowOpacitySlider.Value * 100.0)}%";
		}
	}

	private void AccentColorInput_TextChanged(object sender, TextChangedEventArgs e)
	{
		try
		{
			if (AccentColorInput != null && ColorPreview != null)
			{
				string colorText = AccentColorInput.Text.Trim();
				if (colorText.StartsWith("#") && (colorText.Length == 7 || colorText.Length == 9))
				{
					System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorText);
					ColorPreview.Color = color;
				}
			}
		}
		catch
		{
		}
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		ApplySettings();
		Hide();
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Hide();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!AllowClose)
		{
			e.Cancel = true;
			Hide();
		}
	}

	private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
	{
		await Updater.ManualCheck("v2.0.2");
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	public void SetOpacity(double opacity)
	{
		if (base.Content is Border border)
		{
			border.Opacity = opacity;
		}
	}

	private void BtnColor_Click(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button { Tag: string colorCode })
		{
			try
			{
				_settings.ButtonColor = colorCode;
				_settings.Save();
				SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFrom(colorCode);
				System.Windows.Application.Current.Resources["ActionButtonBackground"] = brush;
			}
			catch
			{
			}
		}
	}

	private async void UpdateVelocityApi_Click(object sender, RoutedEventArgs e)
	{
		const string downloadUrl = "https://realvelocity.xyz/assets/VelocityAPI_net_8.0.dll";
		
		try
		{
			UpdateApiBtn.IsEnabled = false;
			UpdateApiStatus.Text = "Downloading latest VelocityAPI...";
			UpdateApiStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));

			string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string exeDir = Path.GetDirectoryName(exePath);
			string targetPath = Path.Combine(exeDir, "VelocityAPI.dll");
			string updatePath = targetPath + ".update";

			using (HttpClient client = new HttpClient())
			{
				client.Timeout = TimeSpan.FromMinutes(2);
				byte[] dllBytes = await client.GetByteArrayAsync(downloadUrl);
				UpdateApiStatus.Text = "Saving update...";
				await File.WriteAllBytesAsync(updatePath, dllBytes);
				
				UpdateApiStatus.Text = "✓ Update downloaded! Restart to apply.";
				UpdateApiStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x00));

				var result = System.Windows.MessageBox.Show(
					"VelocityAPI update downloaded successfully!\n\nThe update will be applied when you restart the application.\n\nWould you like to restart now?",
					"Update Ready",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result == MessageBoxResult.Yes)
				{
					string currentExe = Environment.ProcessPath;
					System.Diagnostics.Process.Start(currentExe);
					System.Windows.Application.Current.Shutdown();
				}
			}
		}
		catch (Exception ex)
		{
			UpdateApiStatus.Text = $"✗ Update failed: {ex.Message}";
			UpdateApiStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x55));
		}
		finally
		{
			UpdateApiBtn.IsEnabled = true;
		}
	}

}
