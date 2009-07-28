﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;

namespace Chorus.Utilities
{
	public class UsbUtilities
	{
		public static List<DriveInfo> GetLogicalUsbDisks()
		{
			List<DriveInfo> driveInfos = new List<DriveInfo>();
			using (ManagementObjectSearcher driveSearcher = new ManagementObjectSearcher(
					"SELECT Caption, DeviceID FROM Win32_DiskDrive WHERE InterfaceType='USB'"))
			{
				// walk all USB WMI physical disks
				foreach (ManagementObject drive in driveSearcher.Get())
				{
					// browse all USB WMI physical disks

					using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
						"ASSOCIATORS OF {Win32_DiskDrive.DeviceID='"
						+ drive["DeviceID"]
						+ "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition"))
					{
						// walk all USB WMI physical disks
						foreach (ManagementObject partition in searcher.Get())
						{
							using (ManagementObjectSearcher partitionSearcher = new ManagementObjectSearcher(
								"ASSOCIATORS OF {Win32_DiskPartition.DeviceID='"
								+ partition["DeviceID"]
								+ "'} WHERE AssocClass = Win32_LogicalDiskToPartition"))
							{
								foreach (ManagementObject disk in partitionSearcher.Get())
								{
									foreach (DriveInfo driveInfo in System.IO.DriveInfo.GetDrives())
									{
										string s = driveInfo.Name.Replace("\\", "");
										if (s == disk["NAME"].ToString())
										{
											driveInfos.Add(driveInfo);
										}
									}
								}
							}
						}
					}
				}
			}
			return driveInfos;
		}
	}
}
