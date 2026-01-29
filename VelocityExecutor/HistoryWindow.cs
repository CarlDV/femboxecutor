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
		_isCloudMode = true;
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		PaginationPanel.Visibility = Visibility.Visible;
		LoadCloudScripts();
	}




	private async void LoadCloudScripts()
	{
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
				Subtitle = (x.Game?.Name ?? "Universal"),
				Footer = $"{x.Views:N0} views",
				CopyText = "https://scriptblox.com/script/" + x.Slug,
				IsVerified = x.Verified,
				ImageUrl = x.Game?.ImageUrl ?? x.Image,
				Views = x.Views,
				IsPatched = x.IsPatched,
				IsUniversal = x.IsUniversal,
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
		if (e.Key == Key.Return)
		{
			_currentPage = 1;
			LoadCloudScripts();
		}
	}

	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
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

	private HistoryItemViewModel _selectedViewModel;

	private void Card_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is Border border && border.DataContext is HistoryItemViewModel vm)
		{
			_selectedViewModel = vm;
			_selectedEntry = vm.OriginalEntry;
			ShowDetailView(vm);
		}
	}

	private void ShowDetailView(HistoryItemViewModel vm)
	{
		DetailTitle.Text = vm.Title;
		DetailGame.Text = vm.Subtitle;
		DetailViews.Text = vm.Footer;
		
		if (!string.IsNullOrEmpty(vm.ImageUrl))
		{
			try
			{
				DetailImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(vm.ImageUrl));
			}
			catch { }
		}
		
		if (_selectedEntry is ScriptBloxScript script)
		{
			DetailScript.Text = string.IsNullOrEmpty(script.ScriptContent) 
				? "-- Script content not available --" 
				: script.ScriptContent;
		}
		else
		{
			DetailScript.Text = "-- Loading... --";
		}
		
		GridView.Visibility = Visibility.Collapsed;
		DetailView.Visibility = Visibility.Visible;
	}

	private void BackToGrid_Click(object sender, RoutedEventArgs e)
	{
		DetailView.Visibility = Visibility.Collapsed;
		GridView.Visibility = Visibility.Visible;
	}

	private void CopyScript_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedEntry is ScriptBloxScript script && !string.IsNullOrEmpty(script.ScriptContent))
		{
			Clipboard.SetText(script.ScriptContent);
		}
	}

	private void UpdateActionButtons(bool enabled)
	{
	}

	private void CopyUrl_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedViewModel != null && !string.IsNullOrEmpty(_selectedViewModel.CopyText))
		{
			Clipboard.SetText(_selectedViewModel.CopyText);
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
}
