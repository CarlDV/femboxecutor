using System.Windows;
using System.Windows.Markup;

namespace VelocityExecutor;

public partial class RenameDialog : Window, IComponentConnector
{
	public string NewName { get; private set; }

	public RenameDialog(string currentName)
	{
		InitializeComponent();
		NameBox.Text = currentName;
		NameBox.SelectAll();
		NameBox.Focus();
	}

	private void Rename_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(NameBox.Text))
		{
			NewName = NameBox.Text;
			base.DialogResult = true;
			Close();
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}
}
