using System.Collections.Generic;

namespace VelocityExecutor.Services;

public class CloudSettingsData
{
	public string DeviceId { get; set; }

	public string DeviceName { get; set; }

	public AppSettings Settings { get; set; }

	public List<SyncedFile> FemboxecutorFiles { get; set; }

	public List<SyncedFile> WorkspaceFiles { get; set; }

	public long Timestamp { get; set; }

	public string Version { get; set; }
}
