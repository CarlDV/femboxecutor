namespace VelocityExecutor;

public class HistoryItemViewModel
{
	public string Title { get; set; }

	public string Subtitle { get; set; }

	public string Footer { get; set; }

	public string CopyText { get; set; }

	public bool IsVerified { get; set; }

	public string ImageUrl { get; set; }

	public int Views { get; set; }

	public bool IsPatched { get; set; }

	public bool IsUniversal { get; set; }

	public object OriginalEntry { get; set; }
}
