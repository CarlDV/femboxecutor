using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VelocityExecutor;

public class ScriptItem : INotifyPropertyChanged
{
	private string _fileName;

	private string _fullPath;

	private bool _isSelected;

	public required string FileName
	{
		get
		{
			return _fileName;
		}
		set
		{
			if (_fileName != value)
			{
				_fileName = value;
				OnPropertyChanged("FileName");
			}
		}
	}

	public required string FullPath
	{
		get
		{
			return _fullPath;
		}
		set
		{
			if (_fullPath != value)
			{
				_fullPath = value;
				OnPropertyChanged("FullPath");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (_isSelected != value)
			{
				_isSelected = value;
				OnPropertyChanged("IsSelected");
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? name = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
