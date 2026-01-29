using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using VelocityExecutor.Services;

namespace VelocityExecutor;

public partial class HistoryWindow : Window, IComponentConnector, IStyleConnector
{
	private MainWindow _mainWindow;

	private ScriptBloxService _scriptBloxService;

	private bool _isCloudMode;

	private int _currentPage = 1;

	private int _totalPages = 1;

	private object? _selectedEntry;

	private string _lastSearch = "";

	public HistoryWindow(MainWindow mainWindow)
	{
		InitializeComponent();
		_mainWindow = mainWindow;
		_scriptBloxService = new ScriptBloxService();
		base.Loaded += Window_Loaded;
		UpdateActionButtons(enabled: false);
		ModeHistory.IsChecked = true;
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		await InitializeMonaco();
		if (ModeHistory.IsChecked == true)
		{
			LoadHistory();
		}
	}

	private async Task InitializeMonaco()
	{
		try
		{
			await PreviewMonaco.EnsureCoreWebView2Async(null);
			string monacoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Monaco", "index.html");
			if (File.Exists(monacoPath))
			{
				PreviewMonaco.Source = new Uri(monacoPath);
			}
			else
			{
				MessageBox.Show("Local Monaco files missing. Script preview will be disabled.", "Warning");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to initialize preview: " + ex.Message);
		}
	}

	private void Mode_Checked(object sender, RoutedEventArgs e)
	{
		UpdateListMode();
	}

	private void UpdateListMode()
	{
		_isCloudMode = ModeCloud.IsChecked == true;
		if (_isCloudMode)
		{
			PaginationPanel.Visibility = Visibility.Visible;
			CountText.Visibility = Visibility.Collapsed;
			_currentPage = 1;
			LoadCloudScripts();
		}
		else
		{
			PaginationPanel.Visibility = Visibility.Collapsed;
			CountText.Visibility = Visibility.Visible;
			LoadHistory();
		}
	}

	private void LoadHistory()
	{
		try
		{
			string searchText = SearchBox?.Text ?? "";
			List<LoadstringHistoryEntry> entries = LoadstringCache.SearchHistory(string.IsNullOrWhiteSpace(searchText) ? null : searchText);
			List<HistoryItemViewModel> viewModels = entries.Select((LoadstringHistoryEntry x) => new HistoryItemViewModel
			{
				Title = x.Url,
				Subtitle = x.Type,
				Footer = x.Timestamp.ToString("MMM dd"),
				CopyText = x.Url,
				OriginalEntry = x,
				IsVerified = false
			}).ToList();
			HistoryList.ItemsSource = viewModels;
			if (CountText != null)
			{
				CountText.Text = $"{entries.Count} items";
			}
		}
		catch
		{
		}
	}

	private async void LoadCloudScripts()
	{
		if (!_isCloudMode)
		{
			return;
		}
		HistoryList.ItemsSource = null;
		string query = SearchBox?.Text ?? "";
		ScriptBloxResponse response = ((!string.IsNullOrWhiteSpace(query)) ? (await _scriptBloxService.SearchScriptsAsync(query, _currentPage)) : (await _scriptBloxService.FetchRecentScriptsAsync(_currentPage)));
		if (response != null && response.Result != null && response.Result.Scripts != null)
		{
			_totalPages = response.Result.TotalPages;
			PageText.Text = $"{_currentPage}/{_totalPages}";
			List<HistoryItemViewModel> viewModels = response.Result.Scripts.Select((ScriptBloxScript x) => new HistoryItemViewModel
			{
				Title = x.Title,
				Subtitle = (x.Game?.Name ?? "Unknown Game"),
				Footer = $"{x.Views} views",
				CopyText = "https://scriptblox.com/script/" + x.Slug,
				IsVerified = x.Verified,
				OriginalEntry = x
			}).ToList();
			HistoryList.ItemsSource = viewModels;
		}
		else
		{
			HistoryList.ItemsSource = new List<HistoryItemViewModel>();
		}
	}

	private void SearchBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return && _isCloudMode)
		{
			_currentPage = 1;
			LoadCloudScripts();
		}
	}

	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (!_isCloudMode)
		{
			LoadHistory();
		}
	}

	private void PagePrev_Click(object sender, RoutedEventArgs e)
	{
		if (_currentPage > 1)
		{
			_currentPage--;
			LoadCloudScripts();
		}
	}

	private void PageNext_Click(object sender, RoutedEventArgs e)
	{
		if (_currentPage < _totalPages)
		{
			_currentPage++;
			LoadCloudScripts();
		}
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
		Close();
	}

	private async void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (HistoryList.SelectedItem is HistoryItemViewModel vm)
		{
			_selectedEntry = vm.OriginalEntry;
			UpdateActionButtons(enabled: true);
			BtnDelete.IsEnabled = !_isCloudMode;
			try
			{
				if (vm.OriginalEntry is LoadstringHistoryEntry localEntry)
				{
					using HttpClient client = new HttpClient();
					client.DefaultRequestHeaders.UserAgent.ParseAdd("VelocityExecutor");
					SetPreview(await client.GetStringAsync(localEntry.Url));
				}
				else if (vm.OriginalEntry is ScriptBloxScript cloudEntry)
				{
					if (!string.IsNullOrEmpty(cloudEntry.ScriptContent))
					{
						SetPreview(cloudEntry.ScriptContent);
					}
					else
					{
						SetPreview(cloudEntry.ScriptContent ?? "-- Script content not available in preview --");
					}
				}
				return;
			}
			catch
			{
				SetPreview("-- Could not fetch script content preview --");
				return;
			}
		}
		_selectedEntry = null;
		UpdateActionButtons(enabled: false);
		SetPreview("");
	}

	private async void SetPreview(string content)
	{
		if (PreviewMonaco != null && PreviewMonaco.CoreWebView2 != null)
		{
			try
			{
				string escaped = JsonSerializer.Serialize(content);
				await PreviewMonaco.ExecuteScriptAsync("window.SetContent(" + escaped + ")");
			}
			catch
			{
			}
		}
	}

	private void UpdateActionButtons(bool enabled)
	{
		if (BtnLoad != null)
		{
			BtnLoad.IsEnabled = enabled;
		}
		if (BtnDelete != null)
		{
			BtnDelete.IsEnabled = enabled;
		}
		if (BtnLoad != null)
		{
			BtnLoad.Opacity = (enabled ? 1.0 : 0.5);
		}
		if (BtnDelete != null)
		{
			BtnDelete.Opacity = (enabled ? 1.0 : 0.5);
		}
	}

	private void CopyUrl_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: string text })
		{
			Clipboard.SetText(text);
		}
	}

	private async void LoadScript_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedEntry == null)
		{
			return;
		}
		try
		{
			string contentToLoad = "";
			string title = "Script";
			if (_selectedEntry is LoadstringHistoryEntry localEntry)
			{
				title = "Loadstring";
				using HttpClient client = new HttpClient();
				contentToLoad = await client.GetStringAsync(localEntry.Url);
			}
			else if (_selectedEntry is ScriptBloxScript cloudEntry)
			{
				title = cloudEntry.Title;
				contentToLoad = cloudEntry.ScriptContent;
			}
			if (!string.IsNullOrEmpty(contentToLoad))
			{
				_mainWindow.AddNewTab(title, contentToLoad);
				Close();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to load script: " + ex.Message);
		}
	}

	private void DeleteEntry_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedEntry != null && !_isCloudMode && _selectedEntry is LoadstringHistoryEntry localEntry && MessageBox.Show("Delete this entry?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
		{
			LoadstringCache.DeleteEntry(localEntry.Url);
			LoadHistory();
		}
	}
}
