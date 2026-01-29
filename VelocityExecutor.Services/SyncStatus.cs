using System;

namespace VelocityExecutor.Services;

public class SyncStatus
{
	public SyncState State { get; set; }

	public bool IsSynced { get; set; }

	public DateTime? LastSyncTime { get; set; }

	public string ErrorMessage { get; set; }

	public bool IsUploading { get; set; }

	public int FilesUploaded { get; set; }

	public int FilesDownloaded { get; set; }
}
