using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace VelocityExecutor.Services;

public static class HardwareID
{
	private const string REGISTRY_KEY = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Femboxecutor";

	private const string REGISTRY_VALUE = "DeviceId";

	private const string BACKUP_KEY = "HKEY_CURRENT_USER\\Software\\Femboxecutor";

	public static string GetDeviceID()
	{
		try
		{
			string registryId = GetFromRegistry("HKEY_LOCAL_MACHINE\\SOFTWARE\\Femboxecutor", "DeviceId");
			if (!string.IsNullOrEmpty(registryId))
			{
				SetInRegistry("HKEY_CURRENT_USER\\Software\\Femboxecutor", "DeviceId", registryId);
				return registryId;
			}
			string userRegistryId = GetFromRegistry("HKEY_CURRENT_USER\\Software\\Femboxecutor", "DeviceId");
			if (!string.IsNullOrEmpty(userRegistryId))
			{
				SetInRegistry("HKEY_LOCAL_MACHINE\\SOFTWARE\\Femboxecutor", "DeviceId", userRegistryId);
				return userRegistryId;
			}
			string hardwareId = GenerateHardwareFingerprint();
			if (!string.IsNullOrEmpty(hardwareId))
			{
				SetInRegistry("HKEY_LOCAL_MACHINE\\SOFTWARE\\Femboxecutor", "DeviceId", hardwareId);
				SetInRegistry("HKEY_CURRENT_USER\\Software\\Femboxecutor", "DeviceId", hardwareId);
				return hardwareId;
			}
			string machineGuid = GetMachineGuid();
			if (!string.IsNullOrEmpty(machineGuid))
			{
				SetInRegistry("HKEY_LOCAL_MACHINE\\SOFTWARE\\Femboxecutor", "DeviceId", machineGuid);
				SetInRegistry("HKEY_CURRENT_USER\\Software\\Femboxecutor", "DeviceId", machineGuid);
				return machineGuid;
			}
			string newId = Guid.NewGuid().ToString();
			SetInRegistry("HKEY_LOCAL_MACHINE\\SOFTWARE\\Femboxecutor", "DeviceId", newId);
			SetInRegistry("HKEY_CURRENT_USER\\Software\\Femboxecutor", "DeviceId", newId);
			return newId;
		}
		catch (Exception)
		{
			return Guid.NewGuid().ToString();
		}
	}

	private static string GenerateHardwareFingerprint()
	{
		try
		{
			string cpuId = GetCpuId();
			string motherboardSerial = GetMotherboardSerial();
			string diskSerial = GetPrimaryDiskSerial();
			string macAddress = GetMacAddress();
			return HashString($"{cpuId}|{motherboardSerial}|{diskSerial}|{macAddress}");
		}
		catch
		{
			return null;
		}
	}

	private static string GetCpuId()
	{
		try
		{
			using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
			using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = searcher.Get().GetEnumerator();
			if (managementObjectEnumerator.MoveNext())
			{
				return ((ManagementObject)managementObjectEnumerator.Current)["ProcessorId"]?.ToString() ?? "";
			}
		}
		catch
		{
		}
		return "";
	}

	private static string GetMotherboardSerial()
	{
		try
		{
			using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
			using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = searcher.Get().GetEnumerator();
			if (managementObjectEnumerator.MoveNext())
			{
				return ((ManagementObject)managementObjectEnumerator.Current)["SerialNumber"]?.ToString() ?? "";
			}
		}
		catch
		{
		}
		return "";
	}

	private static string GetPrimaryDiskSerial()
	{
		try
		{
			using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
			using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = searcher.Get().GetEnumerator();
			if (managementObjectEnumerator.MoveNext())
			{
				return ((ManagementObject)managementObjectEnumerator.Current)["SerialNumber"]?.ToString()?.Trim() ?? "";
			}
		}
		catch
		{
		}
		return "";
	}

	private static string GetMacAddress()
	{
		try
		{
			return (from nic in NetworkInterface.GetAllNetworkInterfaces()
				where nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
				select nic.GetPhysicalAddress().ToString()).FirstOrDefault((string address) => !string.IsNullOrEmpty(address)) ?? "";
		}
		catch
		{
		}
		return "";
	}

	private static string GetMachineGuid()
	{
		try
		{
			return Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Cryptography", "MachineGuid", null)?.ToString();
		}
		catch
		{
		}
		return null;
	}

	private static string GetFromRegistry(string keyPath, string valueName)
	{
		try
		{
			return Registry.GetValue(keyPath, valueName, null)?.ToString();
		}
		catch
		{
			return null;
		}
	}

	private static void SetInRegistry(string keyPath, string valueName, string value)
	{
		try
		{
			Registry.SetValue(keyPath, valueName, value);
		}
		catch
		{
		}
	}

	private static string HashString(string input)
	{
		using SHA256 sha256 = SHA256.Create();
		return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("+", "").Replace("/", "")
			.Replace("=", "")
			.Substring(0, 32);
	}
}
