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
		UpdateSyncStatus();
		_isLoaded = true;
	}

	private async void SyncFiles_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			SyncFilesButton.IsEnabled = false;
			SyncFilesButton.Content = "Syncing...";
			UpdateSyncStatus(SyncState.Syncing);
			bool num = await CloudSyncService.MergeSyncFiles();
			UpdateSyncStatus();
			if (num)
			{
				System.Windows.MessageBox.Show("✅ Sync complete!\n\nYour files are now identical on cloud and local.", "Cloud Sync", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			SyncStatus status = CloudSyncService.GetSyncStatus();
			System.Windows.MessageBox.Show("❌ Sync failed!\n\n" + status.ErrorMessage, "Cloud Sync Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("❌ Sync error: " + ex.Message, "Cloud Sync Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			SyncFilesButton.IsEnabled = true;
			SyncFilesButton.Content = "Sync My Files";
		}
	}

	private void UpdateSyncStatus(SyncState? overrideState = null)
	{
		SyncStatus status = CloudSyncService.GetSyncStatus();
		switch (overrideState ?? status.State)
		{
		case SyncState.Idle:
			SyncStatusIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
			SyncStatusText.Text = "Not synced";
			SyncDetailsText.Text = "";
			break;
		case SyncState.Syncing:
			SyncStatusIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 204, 0));
			SyncStatusText.Text = "Syncing...";
			SyncDetailsText.Text = "Please wait";
			break;
		case SyncState.Synced:
			SyncStatusIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 205, 65));
			SyncStatusText.Text = "Synced";
			if (status.LastSyncTime.HasValue)
			{
				string details = $"Last sync: {status.LastSyncTime.Value:HH:mm:ss}";
				if (status.FilesUploaded > 0 || status.FilesDownloaded > 0)
				{
					details += $" | ↑{status.FilesUploaded} ↓{status.FilesDownloaded}";
				}
				SyncDetailsText.Text = details;
			}
			break;
		case SyncState.Error:
			SyncStatusIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 59, 48));
			SyncStatusText.Text = "Sync failed";
			SyncDetailsText.Text = status.ErrorMessage ?? "Unknown error";
			break;
		}
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

	private async void SaveProfile_Click(object sender, RoutedEventArgs e)
	{
		_ = 4;
		try
		{
			string username = ProfileUsernameBox.Text.Trim();
			string avatarUrl = ProfileAvatarBox.Text.Trim();
			if (string.IsNullOrWhiteSpace(username))
			{
				System.Windows.MessageBox.Show("Please enter a username.", "Profile Editor", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			if (string.IsNullOrWhiteSpace(_settings.ProfileAuthToken))
			{
				System.Windows.MessageBox.Show("No auth token found. Please restart the executor to get a new token.", "Profile Editor", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			System.Windows.Controls.Button button = (System.Windows.Controls.Button)sender;
			button.IsEnabled = false;
			button.Content = "Saving...";
			using (HttpClient client = new HttpClient())
			{
				StringContent content = new StringContent(JsonSerializer.Serialize(new
				{
					username = username,
					avatarUrl = (string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl),
					authToken = _settings.ProfileAuthToken
				}), Encoding.UTF8, "application/json");
				HttpResponseMessage response = await client.PostAsync("https://velocity-helper-bot.renern.workers.dev/api/profile/" + _settings.AnalyticsUserId, content);
				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					button.Content = "Refreshing Auth...";
					await Task.Delay(500);
					string newToken = await AnalyticsService.StartSessionAsync();
					if (!string.IsNullOrEmpty(newToken))
					{
						button.Content = "Retrying...";
						StringContent retryContent = new StringContent(JsonSerializer.Serialize(new
						{
							username = username,
							avatarUrl = (string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl),
							authToken = newToken
						}), Encoding.UTF8, "application/json");
						response = await client.PostAsync("https://velocity-helper-bot.renern.workers.dev/api/profile/" + _settings.AnalyticsUserId, retryContent);
					}
				}
				if (response.IsSuccessStatusCode)
				{
					System.Windows.MessageBox.Show("✅ Profile updated successfully!", "Profile Editor", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					System.Windows.MessageBox.Show("❌ Failed to update profile:\n" + await response.Content.ReadAsStringAsync(), "Profile Editor", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
			}
			button.IsEnabled = true;
			button.Content = "\ud83d\udcbe Save Profile";
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Error: " + ex.Message, "Profile Editor", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
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

	private void ViewStats_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string url = "https://velocity-helper-bot.renern.workers.dev/dashboard?userId=" + _settings.AnalyticsUserId;
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
		}
		catch
		{
		}
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


}
