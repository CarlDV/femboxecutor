using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DiscordRPC;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using VelocityAPI;
using VelocityExecutor.Services;
using XamlAnimatedGif;

namespace VelocityExecutor;

public partial class MainWindow : Window, IComponentConnector, IStyleConnector
{
	public const string VERSION = "v3.0.0";

	private VelAPI _api = new VelAPI();

	private string _scriptsDir;

	private ObservableCollection<ScriptItem> _scripts;

	private string? _currentScriptPath;

	private bool _isInternalUpdate;

	private bool _isMonacoReady;

	private string _rpcActivityState = "Idle";

	private DiscordRpcClient _rpcClient;

	private AppSettings _settings;

	private GameDetector _gameDetector;

	private List<string> _consoleBuffer = new List<string>();

	private DispatcherTimer _statusTimer;

	private readonly string[] _sassTexts = new string[7] { ":3", "uwu", "Nya~", ">///<", "OwO", "rawr :3", "^///^" };

	private Timestamps _startTime;

	private ConsoleWindow _consoleWindow;

	private SettingsWindow _settingsWindow;

	private bool _isSidebarOpen = true;

	private string _originalScriptBeforeLoadstring = null;

	private static readonly DependencyProperty ScrollOffsetProperty = DependencyProperty.RegisterAttached("ScrollOffset", typeof(double), typeof(MainWindow), new PropertyMetadata(0.0, OnScrollOffsetChanged));

	private void PlaySplash()
	{
		try
		{
			Random rand = new Random();
			if (SplashSub != null)
			{
				SplashSub.Text = _sassTexts[rand.Next(_sassTexts.Length)];
			}
		}
		catch
		{
		}
	}

	public void ApplyTheme()
	{
		try
		{
			if (!string.IsNullOrEmpty(_settings.ButtonColor))
			{
				SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFrom(_settings.ButtonColor);
				Application.Current.Resources["ActionButtonBackground"] = brush;
			}
		}
		catch
		{
		}
	}

	public MainWindow()
	{
		try
		{
			InitializeComponent();
			VersionLabel.Text = "v2.0.2";
			_scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
			_scripts = new ObservableCollection<ScriptItem>();
			ScriptList.ItemsSource = _scripts;
			_settings = AppSettings.Load();
			AnalyticsService.Initialize(_settings);
			AnalyticsService.StartSession();
			AnalyticsService.Track("Access", "App Launched");
			AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
			{
				string text = e.ExceptionObject.ToString();
				AnalyticsService.Track("Crash", text);
				File.AppendAllText("crash.log", $"{DateTime.Now}: {text}\n");
			};
			File.AppendAllText("debug_startup.log", "InitializeComponent Success\n");
		}
		catch (Exception value)
		{
			try
			{
				File.AppendAllText("debug_startup.log", $"InitializeComponent FAILED: {value}\n");
			}
			catch
			{
			}
			throw;
		}
		base.Loaded += delegate
		{
			PlaySplash();
			ApplyTheme();
		};
		_settings = AppSettings.Load();
		try
		{
			File.AppendAllText("debug_startup.log", "Settings Loaded\n");
		}
		catch
		{
		}
		_gameDetector = new GameDetector();
		_gameDetector.OnGameChanged += async delegate
		{
			base.Dispatcher.Invoke(UpdateRPC);
			if (_settings.AutoAttach)
			{
				await Task.Delay(4000);
				int pid = GetRobloxPid();
				if (pid != 0 && _api.IsAttached(pid))
				{
					await RunAutoExec();
				}
			}
		};
		InitializeScripts();
		base.Dispatcher.BeginInvoke(new Action(InitializeMonacoAsync), DispatcherPriority.Background);
		ScriptList.Loaded += delegate
		{
			ScrollViewer scrollViewer = FindScrollViewer(ScriptList);
			if (scrollViewer != null)
			{
				scrollViewer.ScrollChanged += delegate
				{
					UpdateScrollIndicator(scrollViewer);
				};
			}
			UpdateScrollIndicator(scrollViewer);
		};
		ScriptList.SizeChanged += delegate
		{
			UpdateScrollIndicator();
		};
		if (!string.IsNullOrEmpty(_settings.CustomTitle))
		{
			AppTitleText.Text = _settings.CustomTitle;
		}
		base.Topmost = _settings.TopMost;
		UpdateAccentColor();
		ApplyOpacity();
		InitializeRPC();
		try
		{
			_api.StartCommunication();
			LogConsole("=== Fembox Executor ===");
			LogConsole("Version: v2.0.2");
			LogConsole("API Communication: Started");
			LogConsole("Auto-Attach: " + (_settings.AutoAttach ? "Enabled" : "Disabled"));
			LogConsole("Discord RPC: " + (_settings.RpcEnabled ? "Enabled" : "Disabled"));
			LogConsole("Ready! Waiting for Roblox...");
		}
		catch (PlatformNotSupportedException)
		{
		}
		catch (Win32Exception ex2) when (ex2.NativeErrorCode == 1392)
		{
			AnalyticsService.TrackError("Antivirus Intervention", "Startup Corruption (1392)");
			MessageBox.Show("Critical Error: The internal helper component is corrupted.\n\nThis is nearly always caused by Antivirus software.\nPlease exclude the application folder from your Antivirus and reinstall.", "Anti-Virus Intervention Detected", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		catch (AggregateException ex3)
		{
			AnalyticsService.TrackError("API Init Network Error", ex3.GetBaseException().Message);
			LogConsole("=== Fembox Executor ===");
			LogConsole("Version: v2.0.2");
			LogConsole("API Communication: Started (Update server unreachable)");
			LogConsole("Auto-Attach: " + (_settings.AutoAttach ? "Enabled" : "Disabled"));
			LogConsole("Discord RPC: " + (_settings.RpcEnabled ? "Enabled" : "Disabled"));
			LogConsole("Ready! Waiting for Roblox...");
		}
		catch (Exception ex4)
		{
			AnalyticsService.TrackError("Backend Init Failed", ex4.Message);
			LogConsole("=== Fembox Executor ===");
			LogConsole("Version: v2.0.2");
			LogConsole("API Communication: Failed (" + ex4.Message + ")");
			LogConsole("Auto-Attach: " + (_settings.AutoAttach ? "Enabled" : "Disabled"));
			LogConsole("Discord RPC: " + (_settings.RpcEnabled ? "Enabled" : "Disabled"));
			LogConsole("Ready! Waiting for Roblox...");
		}
		StartStatusTimer();
		base.Dispatcher.BeginInvoke(new Action(LoadBackground), DispatcherPriority.Background);
		try
		{
			File.AppendAllText("debug_startup.log", "Startup Complete\n");
		}
		catch
		{
		}
		AutoAttachLoop();
		Updater.CheckPostUpdateNotification();
		Updater.CheckRemoteForceUpdate("v2.0.2", _settings.AutoUpdate);
	}

	private async Task AutoAttachLoop()
	{
		while (true)
		{
			try
			{
				if (_settings.AutoAttach)
				{
					int pid = GetRobloxPid();
					if (pid != 0 && !_api.IsAttached(pid))
					{
						await _api.Attach(pid);
						await Task.Delay(1000);
						if (_api.IsAttached(pid))
						{
							base.Dispatcher.Invoke(delegate
							{
								StatusCircle.Fill = Brushes.Green;
								_rpcActivityState = "Attached";
								UpdateRPC();
								LogConsole($"[Auto-Attach] Successfully attached to Roblox (PID: {pid})");
							});
							await RunAutoExec();
						}
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					File.AppendAllText("debug_autoattach.log", $"[{DateTime.Now}] Error: {ex.Message}\n");
				}
				catch
				{
				}
			}
			await Task.Delay(2000);
		}
	}

	private void InitializeRPC()
	{
		if (_settings.RpcEnabled)
		{
			string appId = "1459475824460824607";
			_rpcClient = new DiscordRpcClient(appId);
			if (_settings.RpcTimestamp)
			{
				_startTime = Timestamps.Now;
			}
			_rpcClient.Initialize();
			UpdateRPC();
		}
	}

	private void UpdateRPC()
	{
		if (_rpcClient == null || !_rpcClient.IsInitialized)
		{
			return;
		}
		string largeKey = "app_icon";
		string largeText = "Fembox";
		string smallKey = "app_icon";
		string smallText = "Velocity Reskinned";
		string details = _settings.RpcDetails;
		if (_settings.ShowGameInRpc && _gameDetector.CurrentPlaceId != 0L)
		{
			details = "Playing " + _gameDetector.CurrentGameName;
			if (!string.IsNullOrEmpty(_gameDetector.CurrentGameIconUrl))
			{
				largeKey = _gameDetector.CurrentGameIconUrl;
				largeText = _gameDetector.CurrentGameName;
				smallKey = "app_icon";
				smallText = "Fembox";
			}
		}
		string state = _settings.RpcState;
		if (state == "Femboxecutor")
		{
			state = "Fembox";
		}
		if (string.IsNullOrWhiteSpace(state))
		{
			state = ((_rpcActivityState != "Idle") ? _rpcActivityState : ((!_api.IsAttached(GetRobloxPid())) ? "Idle" : "Attached"));
		}
		RichPresence presence = new RichPresence
		{
			Details = details,
			State = state,
			Assets = new Assets
			{
				LargeImageKey = largeKey,
				LargeImageText = largeText,
				SmallImageKey = smallKey,
				SmallImageText = smallText
			}
		};
		try
		{
			File.AppendAllText("c:\\test\\debug_game_detection.txt", $"[{DateTime.Now}] UpdateRPC - Visuals: LargeKey='{largeKey}', SmallKey='{smallKey}'\n");
		}
		catch
		{
		}
		if (_settings.RpcTimestamp && _startTime != null)
		{
			presence.Timestamps = _startTime;
		}
		_rpcClient.SetPresence(presence);
	}

	protected override void OnClosed(EventArgs e)
	{
		AnalyticsService.EndSession();
		AnalyticsService.Track("Exit", "App Closed");
		_settings.AutoAttach = false;
		_api.StopCommunication();
		if (_consoleWindow != null)
		{
			_consoleWindow.AllowClose = true;
			_consoleWindow.Close();
		}
		if (_settingsWindow != null)
		{
			_settingsWindow.AllowClose = true;
			_settingsWindow.Close();
		}
		if (_rpcClient != null)
		{
			_rpcClient.Dispose();
		}
		Application.Current.Shutdown();
		base.OnClosed(e);
	}

	private async void AttachButton_Click(object sender, RoutedEventArgs e)
	{
		int pid = GetRobloxPid();
		if (pid != 0)
		{
			try
			{
				LogConsole($"[Inject] Attempting to attach to Roblox (PID: {pid})...");
				AnalyticsService.Track("Attach", "Attempting to attach...");
				await _api.Attach(pid);
				if (_api.IsAttached(pid))
				{
					StatusCircle.Fill = Brushes.Green;
					LogConsole($"[Inject] Successfully attached to Roblox! (PID: {pid})");
					AnalyticsService.Track("Attach", "Success");
					_rpcActivityState = "Attached";
					UpdateRPC();
					await RunAutoExec();
				}
				else
				{
					LogConsole($"Attachment failed for PID: {pid}. Anti-Cheat or Version Mismatch.");
					AnalyticsService.TrackError("Attach Failed", "Timeout/Mismatch");
					MessageBox.Show("Oopsie! \nInjection failed >w<! The API was unable to attach to the target process.\n\nPossible causes:\n- Anti-virus interference\n- Missing dependencies\n- Roblox update patched the exploit", "Injection Failed", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				return;
			}
			catch (DllNotFoundException ex)
			{
				AnalyticsService.TrackError("Antivirus Intervention", "Missing DLLs");
				MessageBox.Show("Critical Error >w<! Missing required DLL files.\n" + ex.Message + "\n\nPlease disable your antivirus and reinstall :3", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			catch (Exception ex2)
			{
				AnalyticsService.TrackError("Attach Error", ex2.Message);
				MessageBox.Show("An unexpected error occurred during injection >w<:\n" + ex2.Message, "Injection Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
		}
		AnalyticsService.TrackError("Attach Failed", "Roblox process not found");
		MessageBox.Show("Roblox process not found! Please open Roblox before attaching >w<", "Target Not Found", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private async Task RunAutoExec()
	{
		_ = 2;
		try
		{
			string autoExecPath = Path.Combine(_scriptsDir, "..", "autoexec");
			autoExecPath = Path.GetFullPath(autoExecPath);
			File.WriteAllText("debug_autoexec.log", $"[{DateTime.Now}] Starting AutoExec\n");
			File.AppendAllText("debug_autoexec.log", "Path: " + autoExecPath + "\n");
			File.AppendAllText("debug_autoexec.log", $"Exists: {Directory.Exists(autoExecPath)}\n");
			if (!Directory.Exists(autoExecPath))
			{
				File.AppendAllText("debug_autoexec.log", "Directory not found. Aborting.\n");
				return;
			}
			List<string> files = Directory.GetFiles(autoExecPath, "*.lua").Concat(Directory.GetFiles(autoExecPath, "*.txt")).ToList();
			File.AppendAllText("debug_autoexec.log", $"Found {files.Count} files.\n");
			if (!files.Any())
			{
				LogConsole("[AutoExec] No scripts found in autoexec folder.");
				return;
			}
			LogConsole($"[AutoExec] Running {files.Count} script(s)...");
			File.AppendAllText("debug_autoexec.log", "Waiting 1s for stable attach...\n");
			await Task.Delay(1000);
			foreach (string file in files)
			{
				try
				{
					File.AppendAllText("debug_autoexec.log", "Executing: " + Path.GetFileName(file) + "\n");
					LogConsole("[AutoExec] Executing: " + Path.GetFileName(file));
					string content = await File.ReadAllTextAsync(file);
					AnalyticsService.TrackScript(content, "AutoExec", _gameDetector.CurrentGameName);
					_api.Execute(content);
					await Task.Delay(200);
				}
				catch (Exception value)
				{
					File.AppendAllText("debug_autoexec.log", $"EXCEPTION: {value}\n");
				}
			}
			LogConsole("[AutoExec] Complete!");
			File.AppendAllText("debug_autoexec.log", "AutoExec Complete.\n");
		}
		catch (Exception ex)
		{
			try
			{
				File.WriteAllText("debug_autoexec_CRASH.log", ex.ToString());
			}
			catch
			{
			}
		}
	}

	private void LogConsole(string message)
	{
		if (_consoleWindow != null)
		{
			_consoleWindow.Log(message);
		}
		else
		{
			_consoleBuffer.Add(message);
		}
	}

	private void ToggleConsole_Click(object sender, RoutedEventArgs e)
	{
		if (_consoleWindow == null || !_consoleWindow.IsLoaded)
		{
			_consoleWindow = new ConsoleWindow();
			_consoleWindow.Owner = this;
			_consoleWindow.Loaded += delegate
			{
				UpdateSatellitePositions();
			};
			foreach (string msg in _consoleBuffer)
			{
				_consoleWindow.Log(msg);
			}
			_consoleBuffer.Clear();
			UpdateSatellitePositions();
			_consoleWindow.Show();
		}
		else if (_consoleWindow.Visibility == Visibility.Visible)
		{
			_consoleWindow.Hide();
		}
		else
		{
			_consoleWindow.Show();
			UpdateSatellitePositions();
		}
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (_settingsWindow == null || !_settingsWindow.IsLoaded)
			{
				InitializeSettingsWindow();
				_settingsWindow.Loaded += delegate
				{
					UpdateSatellitePositions();
				};
			}
			if (_settingsWindow.Visibility == Visibility.Visible)
			{
				_settingsWindow.Hide();
				return;
			}
			_settingsWindow.Show();
			_settingsWindow.Activate();
			UpdateSatellitePositions();
		}
		catch (Exception)
		{
			try
			{
				_settingsWindow?.Close();
			}
			catch
			{
			}
			InitializeSettingsWindow();
			_settingsWindow.Show();
		}
	}

	private void InitializeSettingsWindow()
	{
		_settingsWindow = new SettingsWindow(_settings);
		_settingsWindow.Owner = this;
		_settingsWindow.SettingsUpdated += delegate
		{
			base.Topmost = _settings.TopMost;
			LoadBackground();
			if (_consoleWindow != null && _consoleWindow.IsLoaded)
			{
				_consoleWindow.LoadBackground();
			}
			UpdateAccentColor();
			ApplyOpacity();
			if (_settings.RpcEnabled)
			{
				if (_rpcClient == null || !_rpcClient.IsInitialized)
				{
					InitializeRPC();
				}
				else
				{
					UpdateRPC();
				}
			}
			else if (_rpcClient != null)
			{
				_rpcClient.Deinitialize();
				_rpcClient.Dispose();
				_rpcClient = null;
			}
		};
		_settingsWindow.ConsoleBackgroundChanged += delegate
		{
			if (_consoleWindow != null && _consoleWindow.IsLoaded)
			{
				_consoleWindow.LoadBackground();
			}
		};
	}

	protected override void OnStateChanged(EventArgs e)
	{
		base.OnStateChanged(e);
		if (base.WindowState == WindowState.Minimized)
		{
			AnimationBehavior.SetSourceUri(BackgroundImage, null);
			if (_consoleWindow != null && _consoleWindow.IsLoaded)
			{
				_consoleWindow.UnloadBackground();
			}
			if (_settingsWindow != null && _settingsWindow.IsLoaded)
			{
				_settingsWindow.UnloadBackground();
			}
		}
		else
		{
			LoadBackground();
			if (_consoleWindow != null && _consoleWindow.IsLoaded)
			{
				_consoleWindow.LoadBackground();
			}
			if (_settingsWindow != null && _settingsWindow.IsLoaded)
			{
				_settingsWindow.LoadBackground();
			}
		}
	}

	private void UpdateAccentColor()
	{
		try
		{
			if (!string.IsNullOrEmpty(_settings.BorderColor))
			{
				Color color = (Color)ColorConverter.ConvertFromString(_settings.BorderColor);
				Application.Current.Resources["AccentColor"] = new SolidColorBrush(color);
				Application.Current.Resources["AccentColorValue"] = color;
				Application.Current.Resources["GlobalBorderBrush"] = new SolidColorBrush(color);
			}
		}
		catch
		{
		}
	}

	private void ApplyOpacity()
	{
		if (Application.Current.Resources.Contains("PanelBg") && Application.Current.Resources["PanelBg"] is SolidColorBrush brush)
		{
			if (brush.IsFrozen)
			{
				SolidColorBrush newBrush = brush.Clone();
				newBrush.Opacity = _settings.PanelOpacity;
				Application.Current.Resources["PanelBg"] = newBrush;
			}
			else
			{
				brush.Opacity = _settings.PanelOpacity;
			}
		}
		_settingsWindow?.SetOpacity(_settings.WindowOpacity);
		_consoleWindow?.SetOpacity(_settings.WindowOpacity);
	}

	protected override void OnLocationChanged(EventArgs e)
	{
		base.OnLocationChanged(e);
		UpdateSatellitePositions();
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		UpdateSatellitePositions();
	}

	private void UpdateSatellitePositions()
	{
		if (_consoleWindow != null && _consoleWindow.Visibility == Visibility.Visible)
		{
			_consoleWindow.Left = base.Left - _consoleWindow.Width - 10.0;
			_consoleWindow.Top = base.Top;
			_consoleWindow.Height = base.Height;
		}
		if (_settingsWindow != null && _settingsWindow.Visibility == Visibility.Visible)
		{
			_settingsWindow.Left = base.Left + base.Width + 10.0;
			_settingsWindow.Top = base.Top;
			_settingsWindow.Height = base.Height;
		}
	}

	private void RenameTitle_Click(object sender, RoutedEventArgs e)
	{
		RenameDialog dialog = new RenameDialog(AppTitleText.Text);
		dialog.Owner = this;
		if (dialog.ShowDialog() == true)
		{
			_settings.CustomTitle = dialog.NewName;
			AppTitleText.Text = dialog.NewName;
			_settings.Save();
		}
	}

	private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
	{
		await Updater.ManualCheck("v2.0.2");
	}

	private void LoadBackground()
	{
		try
		{
			string path = _settings.BackgroundImagePath;
			try
			{
				File.AppendAllText("debug_bg.log", $"[{DateTime.Now}] Loading BG: '{path}'\n");
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(path))
			{
				return;
			}
			if (!Path.IsPathRooted(path))
			{
				path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
				try
				{
					File.AppendAllText("debug_bg.log", $"[{DateTime.Now}] Resolved Absolute: '{path}'\n");
				}
				catch
				{
				}
			}
			if (File.Exists(path))
			{
				BackgroundImage.Visibility = Visibility.Visible;
				AnimationBehavior.SetSourceUri(BackgroundImage, new Uri(path, UriKind.Absolute));
				return;
			}
			try
			{
				File.AppendAllText("debug_bg.log", $"[{DateTime.Now}] File NOT FOUND: '{path}'\n");
			}
			catch
			{
			}
			if (!_settings.BackgroundImagePath.EndsWith("background.png"))
			{
				_settings.BackgroundImagePath = "Images/background.png";
				LoadBackground();
			}
		}
		catch (Exception value)
		{
			try
			{
				File.AppendAllText("debug_bg.log", $"[{DateTime.Now}] ERROR: {value}\n");
			}
			catch
			{
			}
		}
	}

	private void StartStatusTimer()
	{
		_statusTimer = new DispatcherTimer();
		_statusTimer.Interval = TimeSpan.FromSeconds(1.0);
		_statusTimer.Tick += delegate
		{
			UpdateStatus();
		};
		_statusTimer.Start();
	}

	private void UpdateStatus()
	{
		int pid = GetRobloxPid();
		bool attached = pid != 0 && _api.IsAttached(pid);
		StatusCircle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(attached ? "#28CD41" : "#FF3B30"));
		StatusGlow.Color = (Color)ColorConverter.ConvertFromString(attached ? "#28CD41" : "#FF3B30");
	}

	private async void InitializeMonacoAsync()
	{
		try
		{
			await MonacoEditor.EnsureCoreWebView2Async();
			string monacoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Monaco", "index.html");
			MonacoEditor.Source = new Uri(monacoPath);
			MonacoEditor.WebMessageReceived += MonacoEditor_WebMessageReceived;
			MonacoEditor.NavigationCompleted += MonacoEditor_NavigationCompleted;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to initialize Monaco Editor: " + ex.Message);
		}
	}

	private async void MonacoEditor_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		_isMonacoReady = true;
		if (_currentScriptPath != null && File.Exists(_currentScriptPath))
		{
			SetMonacoContentAsync(File.ReadAllText(_currentScriptPath));
		}
		_ = LoadRobloxApiAsync();
	}

	private async Task LoadRobloxApiAsync()
	{
		try
		{
			var handler = new System.Net.Http.HttpClientHandler();
			handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
			handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
			
			using var client = new System.Net.Http.HttpClient(handler);
			client.Timeout = TimeSpan.FromSeconds(30);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("VelocityExecutor/1.0");
			
			string version = await client.GetStringAsync("https://setup.roblox.com/versionQTStudio");
			version = version.Trim();
			
			string apiJson = await client.GetStringAsync($"https://setup.roblox.com/{version}-API-Dump.json");
			
			string escaped = JsonSerializer.Serialize(apiJson);
			await MonacoEditor.ExecuteScriptAsync($"window.LoadRobloxApiFromCSharp({escaped})");
			
			LogConsole($"[Monaco] Loaded Roblox API ({version})");
		}
		catch (Exception ex)
		{
			LogConsole($"[Monaco] API load failed: {ex.Message}");
			try { File.AppendAllText("debug_api.log", $"[{DateTime.Now}] API Load Error: {ex}\n"); } catch { }
		}
	}

	private void MonacoEditor_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		try
		{
			using JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
			JsonElement root = doc.RootElement;
			if (root.TryGetProperty("type", out var type) && type.GetString() == "textChanged" && !_isInternalUpdate && !string.IsNullOrEmpty(_currentScriptPath) && root.TryGetProperty("content", out var content))
			{
				File.WriteAllText(_currentScriptPath, content.GetString());
			}
		}
		catch (Exception ex)
		{
			try
			{
				File.AppendAllText("debug_monaco.log", $"[{DateTime.Now}] WebMessage Error: {ex.Message}\n");
			}
			catch
			{
			}
		}
	}

	private async Task SetMonacoContentAsync(string content)
	{
		if (_isMonacoReady)
		{
			string escaped = JsonSerializer.Serialize(content);
			await MonacoEditor.ExecuteScriptAsync("window.SetContent(" + escaped + ")");
		}
	}

	private async Task<string> GetMonacoContentAsync()
	{
		if (!_isMonacoReady)
		{
			return "";
		}
		string json = await MonacoEditor.ExecuteScriptAsync("window.GetContent()");
		try
		{
			return JsonSerializer.Deserialize<string>(json) ?? "";
		}
		catch
		{
			return "";
		}
	}

	private void ToolsButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button { ContextMenu: not null } btn)
		{
			btn.ContextMenu.PlacementTarget = btn;
			btn.ContextMenu.IsOpen = true;
		}
	}

	private void CheckApiStatus_Click(object sender, RoutedEventArgs e)
	{
		int pid = GetRobloxPid();
		if (pid == 0)
		{
			MessageBox.Show("Roblox is not running.", "Status");
			return;
		}
		bool isAttached = _api.IsAttached(pid);
		MessageBox.Show(isAttached ? "API is ATTACHED to Roblox." : "API is NOT attached.", "API Status", MessageBoxButton.OK, isAttached ? MessageBoxImage.Asterisk : MessageBoxImage.Exclamation);
	}

	private void Base64Encode_Click(object sender, RoutedEventArgs e)
	{
		EncodeEditorContentAsync();
	}

	private async Task EncodeEditorContentAsync()
	{
		string content = await GetMonacoContentAsync();
		if (!string.IsNullOrEmpty(content))
		{
			string encoded = VelAPI.Base64Encode(content);
			await SetMonacoContentAsync(encoded);
		}
	}

	private void Base64Decode_Click(object sender, RoutedEventArgs e)
	{
		DecodeEditorContentAsync();
	}

	private async Task DecodeEditorContentAsync()
	{
		string content = await GetMonacoContentAsync();
		if (string.IsNullOrEmpty(content))
		{
			return;
		}
		try
		{
			byte[] bytes = VelAPI.Base64Decode(content);
			string decoded = Encoding.UTF8.GetString(bytes);
			await SetMonacoContentAsync(decoded);
		}
		catch
		{
			MessageBox.Show("Invalid Base64 content.", "Error");
		}
	}

	private void InitializeScripts()
	{
		try
		{
			string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Femboxecutor");
			if (!Directory.Exists(appData))
			{
				Directory.CreateDirectory(appData);
			}
			_scriptsDir = Path.Combine(appData, "Scripts");
			if (!Directory.Exists(_scriptsDir))
			{
				Directory.CreateDirectory(_scriptsDir);
			}
			string workspaceDir = Path.Combine(appData, "workspace");
			if (!Directory.Exists(workspaceDir))
			{
				Directory.CreateDirectory(workspaceDir);
			}
			string autoExec = Path.Combine(appData, "autoexec");
			if (!Directory.Exists(autoExec))
			{
				Directory.CreateDirectory(autoExec);
			}
			_scripts = new ObservableCollection<ScriptItem>();
			ScriptList.ItemsSource = _scripts;
			LoadScripts();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to initialize scripts directory: " + ex.Message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			_scripts = new ObservableCollection<ScriptItem>();
			ScriptList.ItemsSource = _scripts;
		}
	}

	private void LoadScripts()
	{
		_scripts.Clear();
		try
		{
			if (!Directory.Exists(_scriptsDir))
			{
				return;
			}
			foreach (string file in Directory.GetFiles(_scriptsDir, "*.txt").Concat(Directory.GetFiles(_scriptsDir, "*.lua")))
			{
				_scripts.Add(new ScriptItem
				{
					FullPath = file,
					FileName = Path.GetFileName(file)
				});
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading scripts: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			try
			{
				DragMove();
			}
			catch
			{
			}
		}
	}

	private void MinimizeButton_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
	}

	private void SidebarToggle_Click(object sender, RoutedEventArgs e)
	{
		_statusTimer?.Stop();
		SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
		SidebarBorder.BeginAnimation(FrameworkElement.MarginProperty, null);
		SidebarBorder.BeginAnimation(UIElement.OpacityProperty, null);
		if (_isSidebarOpen)
		{
			_isSidebarOpen = false;
			DoubleAnimation widthAnim = new DoubleAnimation(SidebarBorder.ActualWidth, 0.0, TimeSpan.FromSeconds(0.45))
			{
				EasingFunction = new QuinticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.35));
			ThicknessAnimation marginAnim = new ThicknessAnimation(SidebarBorder.Margin, new Thickness(0.0), TimeSpan.FromSeconds(0.45))
			{
				EasingFunction = new QuinticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			widthAnim.Completed += delegate
			{
				if (!_isSidebarOpen)
				{
					SidebarBorder.Visibility = Visibility.Collapsed;
				}
				_statusTimer?.Start();
			};
			Timeline.SetDesiredFrameRate(widthAnim, 120);
			SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
			SidebarBorder.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
			SidebarBorder.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
			return;
		}
		_isSidebarOpen = true;
		SidebarBorder.Visibility = Visibility.Visible;
		if (SidebarBorder.ActualWidth < 1.0)
		{
			SidebarBorder.Width = 0.0;
		}
		DoubleAnimation widthAnim2 = new DoubleAnimation(SidebarBorder.ActualWidth, 180.0, TimeSpan.FromSeconds(0.45))
		{
			EasingFunction = new QuinticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		ThicknessAnimation marginAnim2 = new ThicknessAnimation(SidebarBorder.Margin, new Thickness(0.0, 0.0, 10.0, 0.0), TimeSpan.FromSeconds(0.45))
		{
			EasingFunction = new QuinticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		DoubleAnimation fadeAnim2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(0.45));
		Timeline.SetDesiredFrameRate(widthAnim2, 120);
		widthAnim2.Completed += delegate
		{
			_statusTimer?.Start();
		};
		SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnim2);
		SidebarBorder.BeginAnimation(FrameworkElement.MarginProperty, marginAnim2);
		SidebarBorder.BeginAnimation(UIElement.OpacityProperty, fadeAnim2);
	}

	private void AddScript_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string newFile = Path.Combine(_scriptsDir, $"Script{_scripts.Count + 1}.lua");
			int i = 1;
			while (File.Exists(newFile))
			{
				newFile = Path.Combine(_scriptsDir, $"Script{_scripts.Count + 1 + i}.lua");
				i++;
			}
			File.WriteAllText(newFile, "-- New Script");
			ScriptItem item = new ScriptItem
			{
				FullPath = newFile,
				FileName = Path.GetFileName(newFile)
			};
			_scripts.Add(item);
			ScriptList.SelectedItem = item;
			LogConsole("Created new script: " + Path.GetFileName(newFile));
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateScrollIndicator();
			}, DispatcherPriority.Loaded);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to create new script: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ScriptList.SelectedItem is ScriptItem script)
		{
			_currentScriptPath = script.FullPath;
			if (File.Exists(_currentScriptPath))
			{
				SetMonacoContentAsync(File.ReadAllText(_currentScriptPath));
			}
		}
	}

	private void RenameScript_Click(object sender, RoutedEventArgs e)
	{
		ScriptItem item = ((sender is MenuItem { Tag: ScriptItem s }) ? s : (ScriptList.SelectedItem as ScriptItem));
		if (item == null)
		{
			return;
		}
		ScriptItem currentSelection = ScriptList.SelectedItem as ScriptItem;
		ScrollViewer scrollViewer = FindScrollViewer(ScriptList);
		double scrollPosition = scrollViewer?.HorizontalOffset ?? 0.0;
		RenameDialog dialog = new RenameDialog(item.FileName);
		dialog.Owner = this;
		if (dialog.ShowDialog() != true)
		{
			return;
		}
		string newName = dialog.NewName;
		if (string.IsNullOrWhiteSpace(newName) || !(newName != item.FileName))
		{
			return;
		}
		try
		{
			if (!newName.EndsWith(".lua") && !newName.EndsWith(".txt"))
			{
				newName += Path.GetExtension(item.FileName);
			}
			string oldPath = item.FullPath;
			string newPath = Path.Combine(Path.GetDirectoryName(oldPath), newName);
			if (File.Exists(newPath))
			{
				MessageBox.Show("A file with that name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			File.Move(oldPath, newPath);
			item.FullPath = newPath;
			item.FileName = newName;
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (currentSelection != null)
				{
					ScriptList.SelectedItem = currentSelection;
				}
				if (scrollViewer != null)
				{
					scrollViewer.ScrollToHorizontalOffset(scrollPosition);
				}
			}, DispatcherPriority.Loaded);
			LogConsole("Renamed " + Path.GetFileName(oldPath) + " to " + newName);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to rename file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private ScrollViewer FindScrollViewer(DependencyObject obj)
	{
		if (obj == null)
		{
			return null;
		}
		if (obj is ScrollViewer sv)
		{
			return sv;
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(obj, i);
			ScrollViewer result = FindScrollViewer(child);
			if (result != null)
			{
				return result;
			}
		}
		return null;
	}

	private void ScriptList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = FindScrollViewer(ScriptList);
		if (scrollViewer != null)
		{
			e.Handled = true;
			double delta = (double)e.Delta * 0.8;
			double targetOffset = scrollViewer.HorizontalOffset - delta;
			targetOffset = Math.Max(0.0, Math.Min(scrollViewer.ScrollableWidth, targetOffset));
			DoubleAnimation animation = new DoubleAnimation
			{
				From = scrollViewer.HorizontalOffset,
				To = targetOffset,
				Duration = TimeSpan.FromMilliseconds(300.0),
				EasingFunction = new QuarticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			animation.Completed += delegate
			{
				UpdateScrollIndicator();
			};
			new Storyboard();
			Storyboard.SetTarget(animation, scrollViewer);
			Storyboard.SetTargetProperty(animation, new PropertyPath(ScrollOffsetProperty));
			scrollViewer.BeginAnimation(ScrollOffsetProperty, animation);
			UpdateScrollIndicator();
		}
	}

	private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ScrollViewer scrollViewer)
		{
			scrollViewer.ScrollToHorizontalOffset((double)e.NewValue);
		}
	}

	private void UpdateScrollIndicator(ScrollViewer sv = null)
	{
		try
		{
			ScrollViewer scrollViewer = sv ?? FindScrollViewer(ScriptList);
			if (scrollViewer == null)
			{
				return;
			}
			Border indicator = FindName("TopProgressBar") as Border;
			Grid indicatorContainer = FindName("BottomScrollIndicator") as Grid;
			if (indicator == null || indicatorContainer == null)
			{
				return;
			}
			double viewportWidth = scrollViewer.ViewportWidth;
			double extentWidth = scrollViewer.ExtentWidth;
			double actualWidth = scrollViewer.ActualWidth;
			double offset = scrollViewer.HorizontalOffset;
			if (extentWidth <= viewportWidth || actualWidth == 0.0)
			{
				indicatorContainer.Visibility = Visibility.Collapsed;
				return;
			}
			indicatorContainer.Visibility = Visibility.Visible;
			double ratio = viewportWidth / extentWidth;
			double indicatorWidth = Math.Max(20.0, actualWidth * ratio);
			double num = actualWidth - indicatorWidth;
			double scrollRatio = offset / (extentWidth - viewportWidth);
			double indicatorPosition = num * scrollRatio;
			double currentWidth = indicator.ActualWidth;
			if (double.IsNaN(currentWidth) || currentWidth <= 0.0)
			{
				currentWidth = (indicator.Width = indicatorWidth);
			}
			if (Math.Abs(currentWidth - indicatorWidth) > 1.0)
			{
				DoubleAnimation widthAnimation = new DoubleAnimation
				{
					From = currentWidth,
					To = indicatorWidth,
					Duration = TimeSpan.FromMilliseconds(200.0),
					EasingFunction = new QuarticEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				indicator.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
			}
			else
			{
				indicator.Width = indicatorWidth;
			}
			TranslateTransform transform = indicator.RenderTransform as TranslateTransform;
			if (transform == null)
			{
				transform = (TranslateTransform)(indicator.RenderTransform = new TranslateTransform());
			}
			transform.BeginAnimation(TranslateTransform.XProperty, null);
			transform.X = indicatorPosition;
		}
		catch
		{
		}
	}

	private void DeleteScript_Click(object sender, RoutedEventArgs e)
	{
		DeleteSelectedScript();
	}

	private void ScrollLeft_Click(object sender, RoutedEventArgs e)
	{
		if (ScriptList.SelectedIndex > 0)
		{
			ScriptList.SelectedIndex--;
			ScriptList.ScrollIntoView(ScriptList.SelectedItem);
		}
	}

	private void ScrollRight_Click(object sender, RoutedEventArgs e)
	{
		if (ScriptList.SelectedIndex < ScriptList.Items.Count - 1)
		{
			ScriptList.SelectedIndex++;
			ScriptList.ScrollIntoView(ScriptList.SelectedItem);
		}
	}

	private void CloseTab_Click(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button { DataContext: ScriptItem script })
		{
			_scripts.Remove(script);
			ScriptList.ItemsSource = _scripts;
			if (_scripts.Count > 0)
			{
				ScriptList.SelectedIndex = 0;
			}
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateScrollIndicator();
			}, DispatcherPriority.Loaded);
		}
	}

	private void CloseTabWithWarning_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is System.Windows.Controls.Button { DataContext: ScriptItem script }) || MessageBox.Show("Are you sure you want to delete '" + script.FileName + "'?\n\nThis will permanently delete the script file.", "Delete Script", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
		{
			return;
		}
		try
		{
			if (File.Exists(script.FullPath))
			{
				File.Delete(script.FullPath);
			}
			_scripts.Remove(script);
			if (_scripts.Count > 0)
			{
				ScriptList.SelectedIndex = Math.Max(0, _scripts.Count - 1);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to delete script: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void CtxDelete_Click(object sender, RoutedEventArgs e)
	{
		DeleteSelectedScript();
	}

	private void DeleteSelectedScript()
	{
		if (!(ScriptList.SelectedItem is ScriptItem item) || MessageBox.Show("Delete '" + item.FileName + "'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
		{
			return;
		}
		try
		{
			if (File.Exists(item.FullPath))
			{
				File.Delete(item.FullPath);
			}
			_scripts.Remove(item);
			SetMonacoContentAsync("");
			_currentScriptPath = null;
			LogConsole("Deleted script: " + item.FileName);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to delete script: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void LoadFile_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Filter = "Lua/Text (*.lua;*.txt)|*.lua;*.txt|All Files (*.*)|*.*"
		};
		if (dialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			await SetMonacoContentAsync(await File.ReadAllTextAsync(dialog.FileName));
			LogConsole("Loaded file: " + Path.GetFileName(dialog.FileName));
			string fileName = Path.GetFileName(dialog.FileName);
			string destPath = Path.Combine(_scriptsDir, fileName);
			if (!File.Exists(destPath))
			{
				try
				{
					File.Copy(dialog.FileName, destPath);
					LoadScripts();
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to load file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async void SaveFile_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog dialog = new SaveFileDialog
		{
			Filter = "Lua Script (*.lua)|*.lua|Text File (*.txt)|*.txt",
			FileName = "Script.lua"
		};
		if (dialog.ShowDialog() == true)
		{
			try
			{
				string content = await GetMonacoContentAsync();
				await File.WriteAllTextAsync(dialog.FileName, content);
				LogConsole("Saved file: " + Path.GetFileName(dialog.FileName));
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to save file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
	}

	public async void LoadScriptContent(string content)
	{
		await SetMonacoContentAsync(content);
		LogConsole($"[History] Loaded script ({content.Length} characters)");
	}

	public async void AddNewTab(string title, string content)
	{
		try
		{
			string safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
			if (string.IsNullOrWhiteSpace(safeTitle))
			{
				safeTitle = "Untitled";
			}
			if (!safeTitle.EndsWith(".lua") && !safeTitle.EndsWith(".txt"))
			{
				safeTitle += ".lua";
			}
			string fullPath = Path.Combine(_scriptsDir, safeTitle);
			int count = 1;
			while (File.Exists(fullPath))
			{
				string nameWithourExt = Path.GetFileNameWithoutExtension(safeTitle);
				string ext = Path.GetExtension(safeTitle);
				fullPath = Path.Combine(_scriptsDir, $"{nameWithourExt}_{count}{ext}");
				count++;
			}
			await File.WriteAllTextAsync(fullPath, content);
			ScriptItem newItem = new ScriptItem
			{
				FullPath = fullPath,
				FileName = Path.GetFileName(fullPath)
			};
			_scripts.Add(newItem);
			ScriptList.SelectedItem = newItem;
			ScriptList.ScrollIntoView(newItem);
			LogConsole("[History] Added new tab: " + newItem.FileName);
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateScrollIndicator();
			}, DispatcherPriority.Loaded);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to add new tab: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void OpenHistory_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			HistoryWindow historyWindow = new HistoryWindow(this);
			historyWindow.Owner = this;
			historyWindow.Show();
		}
		catch (Exception ex)
		{
			LogConsole("[History] Error: " + ex.Message);
			MessageBox.Show("Failed to open history window:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ClearButton_Click(object sender, RoutedEventArgs e)
	{
		SetMonacoContentAsync("");
		LogConsole("Editor cleared.");
	}

	private async void FetchButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (_originalScriptBeforeLoadstring != null)
			{
				await SetMonacoContentAsync(_originalScriptBeforeLoadstring);
				LogConsole($"[Show Loadstring] Reverted to original script ({_originalScriptBeforeLoadstring.Length} characters)");
				_originalScriptBeforeLoadstring = null;
				return;
			}
			string content = await GetMonacoContentAsync();
			if (string.IsNullOrWhiteSpace(content))
			{
				MessageBox.Show("Editor is empty. Please paste a loadstring first.", "No Content", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			string url = PastebinService.ExtractUrlFromLoadstring(content);
			if (string.IsNullOrEmpty(url))
			{
				MessageBox.Show("No valid URL found in the editor content.\n\nPlease paste a loadstring like:\nloadstring(game:HttpGet(\"URL\"))()\n\nor a direct URL.", "URL Not Found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			LogConsole("[Show Loadstring] Fetching script from: " + url);
			string script = await PastebinService.FetchScriptAsync(url);
			if (string.IsNullOrWhiteSpace(script))
			{
				MessageBox.Show("The fetched content is empty.", "Empty Script", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			_originalScriptBeforeLoadstring = content;
			await SetMonacoContentAsync(script);
			LogConsole($"[Show Loadstring] Showing script ({script.Length} characters) - Click again to revert");
		}
		catch (Exception ex)
		{
			LogConsole("[Show Loadstring] Error: " + ex.Message);
			MessageBox.Show("Failed to fetch script:\n" + ex.Message, "Fetch Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private int GetRobloxPid()
	{
		return (from p in Process.GetProcessesByName("RobloxPlayerBeta").Where(delegate(Process p)
			{
				try
				{
					return !p.HasExited && p.MainWindowHandle != IntPtr.Zero;
				}
				catch
				{
					return false;
				}
			}).OrderByDescending(delegate(Process p)
			{
				try
				{
					return p.StartTime;
				}
				catch
				{
					return DateTime.MinValue;
				}
			})
			select p.Id).FirstOrDefault();
	}

	private void ReconnectApi_Click(object sender, RoutedEventArgs e)
	{
		ReconnectAPI();
		MessageBox.Show("API Communication Re-established.", "Reconnect", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void ReconnectAPI()
	{
		try
		{
			_api.StopCommunication();
			_api.StartCommunication();
		}
		catch (Exception)
		{
		}
	}

	private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
	{
		_ = 4;
		try
		{
			int pid = GetRobloxPid();
			if (pid == 0)
			{
				MessageBox.Show("Roblox is not running!", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogConsole("Execution Failed: Roblox not running.");
				return;
			}
			if (!_api.IsAttached(pid))
			{
				LogConsole($"[Execute] Not attached. Attaching to PID {pid}...");
				_api.Attach(pid);
				await Task.Delay(100);
				if (_api.IsAttached(pid))
				{
					LogConsole("[Execute] Successfully attached!");
				}
			}
			string script = await GetMonacoContentAsync();
			try
			{
				_rpcActivityState = "Executing Script...";
				UpdateRPC();
				AnalyticsService.TrackScript(script, "Execute", _gameDetector.CurrentGameName);
				_api.Execute(script);
				LogConsole("Script Executed.");
				await Task.Delay(1000);
				_rpcActivityState = "Idle";
				UpdateRPC();
			}
			catch
			{
				LogConsole("Execution failed, attempting retry/reconnect...");
				AnalyticsService.Track("Execute", "Retry");
				ReconnectAPI();
				await Task.Delay(100);
				if (!_api.IsAttached(pid))
				{
					_api.Attach(pid);
				}
				_rpcActivityState = "Executing Script...";
				UpdateRPC();
				_api.Execute(script);
				LogConsole("Retry: Script Executed.");
				await Task.Delay(1000);
				_rpcActivityState = "Idle";
				UpdateRPC();
			}
		}
		catch (Exception ex)
		{
			try
			{
				_api = new VelAPI();
				_api.StartCommunication();
				MessageBox.Show("Execution Error: " + ex.Message + "\nAPI has been reset. Please try again.");
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Critical Error resetting API: " + ex2.Message);
			}
		}
	}
}
