/* 
   Copyright 2012-2022, MGK

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics.Eventing.Reader;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Security;
using System.Text;

namespace EventLogMonitor
{
  public static class CultureSpecificMessage
  {
    private const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private const int LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryExW", SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr LoadLibraryEx(string libFilename, IntPtr reserved, int flags);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);
    private const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
    private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
    private const int FORMAT_MESSAGE_FROM_STRING = 0x00000400;
    private const int FORMAT_MESSAGE_FROM_HMODULE = 0x00000800;
    // private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
    private const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;

    private const int FORMAT_MESSAGE_MAX_WIDTH_MASK = 0x000000FF;
    private const int USEnglishLCID = 1033;
    private static readonly Dictionary<string, IntPtr> iCatalogueCache = [];

    // There are several EventLog providers that have been verified to not register catalogs in the registry, but
    // instead output messages (usually with a low number like 0 or 1) that have the information directly
    // in the inserts so we can provide a default insert string for them instead of giving an error message
    // for a message not found. Most of these seem to be "updater" services that are derived from a version
    // of "Omaha", the open-source version of Google Update: https://github.com/google/omaha
    // Here are some of the logs that show this and similar issues that we can patch when encountered.
    // "AdobeARMservice", // Adobe Acrobat Update Service
    //  "dbupdate", // Dropbox Update Service (dbupdate)
    //  "dbupdatem", // Dropbox Update Service (dbupdatem)
    //  "Dolby DAX2 API Service", // Dolby DAX2 API Service 
    //  "gupdate",  // Google Update Service (gupdate)
    //  "gupdatem",  // Google Update Service (gupdatem)
    //  "iBtSiva",  // Intel(R) Wireless Bluetooth(R) iBtSiva Service
    //  "igfxCUIService1.0.0.0",  // Intel(R) HD Graphics Control Panel Service
    //  "igfxCUIService2.0.0.0",  // Intel(R) HD Graphics Control Panel Service
    //  "WebExService", // Cisco WebEx Update Service
    //  "Universal Print" // Universal Print Management Service (MS) - missing MUI file
    // Note that our check and patch is done very late in the reading flow on purpose so that if
    // a provider were to be registered in a new update of an application it would automatically
    // be picked up and take precedence of our patch. 
    public static bool SpecialCaseMissingProviders { get; set; } = true; // can be unset with '-nopatch'

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "FormatMessageW", SetLastError = true, BestFitMapping = true, ExactSpelling = true)]
    private static extern int FormatMessageW(
      int dwFlags,
      IntPtr lpSource,
      uint dwMessageId,
      int dwLanguageId,
      ref IntPtr lpBuffer,
      int nSize,
      string[] pArguments
    );

    public static string GetCultureSpecificMessage(IEventLogRecordWrapper entry, int cultureLCID, string cultureName)
    {
      bool entryIsAFile = EventLogMonitor.LogIsAFile(entry.ContainerLog);
      string provider = entry.ProviderName; // e.g "IBM App Connect Enterprise v110011" or "Microsoft-Windows-Security-Auditing"
      string logName = entry.LogName; // e.g. "Application" or "Security"
      string providerRegPath = @$"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\{logName}\{provider}";
      string catalogueLocation = null;
      bool securityLogFailure = false;
      try
      {
        catalogueLocation = (string)Registry.GetValue(providerRegPath, "EventMessageFile", null);
      }
      catch (SecurityException)
      {
        if (logName == "Security")
        {
          // checked lower down if the user does not provide their own catalog
          securityLogFailure = true;
        }
      }

      // get a list of all inserts (apart from the last if it is binary)
      List<string> insertList = entry.Properties;

      string formatString = string.Empty;
      if (string.IsNullOrEmpty(catalogueLocation))
      {
        if (entryIsAFile)
        {
          // See if we have a message dll to use next to the current file.
          // Here we look for a file in the same location as the .evtx with
          // the same name but a .dll extension instead.
          var (baseName, baseLocation) = GetPathBaseLocation(entry.ContainerLog);
          int index = baseName.LastIndexOf('.');
          string fileName = baseName[..index];
          string fileFullName = baseLocation + fileName + ".dll";
          if (File.Exists(fileFullName))
          {
            catalogueLocation = fileFullName;
          }

          // special case a security log file that does not have a user provided catalg
          if (securityLogFailure && string.IsNullOrEmpty(catalogueLocation))
          {
            // fake the result as this is an attempt to get the reg info for a security log file and it will always fail here if not elevated
            // however, we can make a best guess at what it should be and for a file we don't want to require elevation for this case...
            catalogueLocation = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\adtschema.dll"; // adtschema.dll contains the entries for the security log
          }
        }
      }
      else
      {
        if (catalogueLocation.Contains(';'))
        {
          // some registry entries, especially device drivers, have multiple options which are semicolon separated.
          // for now just pick the first, but we will probably have to try each one in a loop at some point
          // however, picking the first seems to work ok for now
          string[] paths = catalogueLocation.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
          if (paths.Length >= 1)
          {
            catalogueLocation = paths[0];
          }
        }

        // see if we have an MUI file to use instead of the dll
        string muiFile = LookforMuiLocation(cultureName, catalogueLocation);
        if (!string.IsNullOrEmpty(muiFile))
        {
          catalogueLocation = muiFile;
        }
      }

      // see if we can get a dll handle first...
      IntPtr dllHandle = IntPtr.Zero;
      dllHandle = GetDllHandle(catalogueLocation);

      // If no dll is found we are done
      if (dllHandle == IntPtr.Zero)
      {
        return string.Empty;
      }

      int finalCode = CalculateMessageNumber(entry);
      string responseMsg = GetMessage(finalCode, insertList.ToArray(), cultureLCID, dllHandle, formatString);
      return responseMsg;
    }

    public static string GetPatchedMessageFromFormatString(IEventLogRecordWrapper entry)
    {
      // get a list of all inserts apart from the last if it is binary
      List<string> insertList = entry.Properties;
      string formatString = GenerateFormatString(entry, insertList);
      if(string.IsNullOrEmpty(formatString)) 
      {
        // we can have an empty format string if there are no inserts or just a single binary one
        return string.Empty;
      }
      int finalCode = CalculateMessageNumber(entry);
      string responseMsg = GetMessage(finalCode, insertList.ToArray(), 0, IntPtr.Zero, formatString);
      return responseMsg;
    }

    private static string LookforMuiLocation(string cultureName, string catalogueLocation)
    {
      // Test for an MUI file. If the event is from MS then it is likely there will be a MUI file which we should use instead of the 
      // base DLL. Some other apps also follow this pattern of placing a file with the same name but including a .mui extension
      // in a culture specific subfolder. E.g if the catalogueLocation is "C:\\WINDOWS\\system32\\microsoft-windows-kernel-power-events.dll" and the
      // culture is german then we are looking for the file "C:\\WINDOWS\\system32\\de-DE\\microsoft-windows-kernel-power-events.dll.mui"
      // This is per: https://docs.microsoft.com/en-us/windows/win32/intl/application-deployment. Note that we do not currently handle the
      // documented pre-vista file layout for MUI files as this is so old, but raise an issue if this causes you problems...
      var (baseName, baseLocation) = GetPathBaseLocation(catalogueLocation);
      string muiFullName = baseLocation + cultureName + "\\" + baseName + ".mui";
      return File.Exists(muiFullName) ? muiFullName : String.Empty;
    }

    private static IntPtr GetDllHandle(string catalogueLocation)
    {
      IntPtr dllHandle = IntPtr.Zero;
      if (String.IsNullOrEmpty(catalogueLocation))
      {
        return dllHandle;
      }

      // get a handle to the DLL/MUI file passed in
      if (iCatalogueCache.TryGetValue(catalogueLocation, out IntPtr value))
      {
        dllHandle = value;
      }
      else
      {
        int flags = LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE;
        dllHandle = LoadLibraryEx(catalogueLocation, IntPtr.Zero, flags);
        if (dllHandle != IntPtr.Zero)
        {
          iCatalogueCache[catalogueLocation] = dllHandle;
        }
      }

      return dllHandle;
    }

    private static string GetMessage(int msgCode, string[] arguments, int cultureLCID, IntPtr moduleHandle, string formatString)
    {
      int flags;
      bool useFormatString = false;
      if (!string.IsNullOrEmpty(formatString))
      {
        useFormatString = true;
        flags = FORMAT_MESSAGE_FROM_STRING | FORMAT_MESSAGE_ALLOCATE_BUFFER;
      }
      else
      {
        //flags = FORMAT_MESSAGE_FROM_HMODULE | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_ALLOCATE_BUFFER;
        flags = FORMAT_MESSAGE_FROM_HMODULE | FORMAT_MESSAGE_ALLOCATE_BUFFER;
      }

      if (arguments.Length > 0)
      {
        flags |= FORMAT_MESSAGE_ARGUMENT_ARRAY;
      }
      else
      {
        flags |= FORMAT_MESSAGE_IGNORE_INSERTS;
      }

      // we need to set the low order byte to stop FormatMessage duplicating line breaks in the output message. If not set 
      // all '\r\n' (%n) sequences come out as '\r\n\r\n' which is a real pain and messes up the '-2' (medium output) option.
      // see https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-formatmessage for more details.
      flags += FORMAT_MESSAGE_MAX_WIDTH_MASK;

      // for faster unsafe method see: https://github.com/dotnet/runtime/blob/01b7e73cd378145264a7cb7a09365b41ed42b240/src/libraries/Common/src/Interop/Windows/Kernel32/Interop.FormatMessage.cs
      IntPtr nativeBuffer = IntPtr.Zero;
      try
      {
        int currentCulture = cultureLCID;
        while (true)
        {
          IntPtr formatPtr = moduleHandle;
          if (useFormatString)
          {
            formatPtr = Marshal.StringToHGlobalUni(formatString);
          }

          int length = FormatMessageW(flags, formatPtr, unchecked((uint)msgCode), currentCulture, ref nativeBuffer, 65535, arguments);
          // Console.WriteLine("Len: " + length + ", lastErr: " + Marshal.GetLastWin32Error()); // debugging

          if (length > 0)
          {
            string formattedString = Marshal.PtrToStringUni(nativeBuffer, length);
            return formattedString;
          }
          else
          {
            int lastError = Marshal.GetLastWin32Error();
            if (lastError is 1815 or >= 15100 and <= 15108 or 317)
            {
              // Code definitions below are from here: https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes
              // ALSO see https://learn.microsoft.com/en-us/windows/win32/wes/windows-event-log-error-constants for 15000 - 15038

              // Error code '1815' is "ERROR_RESOURCE_LANG_NOT_FOUND" which means:
              // "The specified resource language ID cannot be found in the image file."

              // The range 15100 - 15108 are all MUI errors which generally mean the language
              // resource dll is not found or is corrupt or the lang is not found in the dll
              // for example errors seen in testing include:

              // Error code '15105' is "ERROR_MUI_FILE_NOT_LOADED" which means:
              // "The resource loader cache doesn't have loaded MUI entry." 
              // AKA - Language not found in DLL

              // Error code '15100' is "ERROR_MUI_FILE_NOT_FOUND" which means:
              // "The resource loader failed to find MUI file."
              // AKA language resource dll not found

              // Error code 317 is "ERROR_MR_MID_NOT_FOUND" which means:
              // The system cannot find message text for message number 0x%1 in the message file for %2.
              // AKA - yet another message not found in dll error code!

              // try again with US English if we have not already tried.
              if ((currentCulture is 0 && cultureLCID is 0) ||
                  (currentCulture is not USEnglishLCID and not 0))
              {
                // if we failed zero, next try with En-US instead
                currentCulture = USEnglishLCID;
                continue;
              }

              if (currentCulture is not 0 && cultureLCID is not 0)
              {
                // final attempt to find a message
                currentCulture = 0;
                continue;
              }

              // So we return an empty string below to force the use of the default culture
              // instead in these cases
            }
            else
            {
              Console.WriteLine("Error: " + lastError + " using specified culture.");
            }
          }
          break;
        }
      }
      finally
      {
        // Free the buffer.
        Marshal.FreeHGlobal(nativeBuffer);
      }

      // default return to force default culture use
      return string.Empty;
    }

    private static string GenerateFormatString(IEventLogRecordWrapper entry, List<string> insertList)
    {
      var retVal = string.Empty;
      if (SpecialCaseMissingProviders)
      {
        int insertCount = insertList.Count;
        if (entry.LastPropertyIsByteArray())
        {
          --insertCount; // don't include a byte[] in the format string if present
        }

        StringBuilder result = new(insertCount);
        for (int i = 1; i <= insertCount; ++i)
        {
          result.Append($"%{i}.%n");
        }
        retVal = result.ToString();
      }

      return retVal;
    }

    static private int CalculateMessageNumber(IEventLogRecordWrapper entry)
    {
      // The native FormatMessage expects the qualifier as the high word of the msg number
      int finalCode;
      if (entry.Qualifiers > 0)
      {
        finalCode = (int)entry.Qualifiers << 16;
        finalCode += entry.Id;
      }
      else
      {
        if (entry.Level == StandardEventLevel.Critical)
        {
          // Investigation shows that critical entries do not set the severity bits,
          // instead they set bit 26 in the Facility section so we need to set it here
          // too so that the correct message number is used.
          finalCode = 0x200 << 16;
          finalCode += entry.Id;
        }
        else
        {
          finalCode = entry.Id;
        }
      }
      return finalCode;
    }

    // Method to split a full path location into a base name and location
    // for a location "C:\\WINDOWS\\system32\\microsoft-windows-kernel-power-events.evtx"
    // baseName would be microsoft-windows-kernel-power-events.evtx and
    // baseLocation would be C:\\WINDOWS\\system32\\
    private static (string baseName, string baseLocation) GetPathBaseLocation(string fullLocation)
    {
      int index = fullLocation.LastIndexOf('\\');
      string baseName = fullLocation[(index + 1)..];
      string baseLocation = fullLocation[..(index + 1)];
      return (baseName, baseLocation);
    }

    // Interface to allow mocking of an EventLogRecord
    public interface IEventLogRecordWrapper
    {
      public string ContainerLog { get; }
      public int? Qualifiers { get; }
      public int Id { get; }
      public string ProviderName { get; }
      public List<string> Properties { get; }
      public string LogName { get; }
      public StandardEventLevel Level { get; }
      public bool LastPropertyIsByteArray();
    }

    // Class to allow mocking of an EventLogRecord
    public class EventLogRecordWrapper : IEventLogRecordWrapper
    {
      private readonly EventRecord iLogRecord;
      private List<string> iEventProperties;
      public EventLogRecordWrapper(EventRecord logRecord)
      {
        iLogRecord = logRecord;
      }
      public string ContainerLog => iLogRecord is EventLogRecord record ? record.ContainerLog : null;
      public int? Qualifiers => iLogRecord.Qualifiers;

      public int Id => iLogRecord.Id;

      public string ProviderName => iLogRecord.ProviderName;

      public List<string> Properties
      {
        get
        {
          if (iEventProperties == null)
          {
            // make a list of inserts, but ignore the last entry if this is binary
            int insertCount = iLogRecord.Properties.Count;
            List<string> insertList = new(insertCount);
            for (int i = 0; i < insertCount; ++i)
            {
              object insert = iLogRecord.Properties[i].Value;
              if (insert is byte[])
              {
                // use an empty string or we get the string "System.Byte[]" not the value.
                // the user can see the value if they provide -b1 or -b2 as byte[]'s are always the last insert
                insertList.Add("");
              }
              else
              {
                insertList.Add(insert.ToString());
              }
            }
            iEventProperties = insertList;
          }
          return iEventProperties;
        }
      }

      public bool LastPropertyIsByteArray()
      {
        int insertCount = iLogRecord.Properties.Count;
        return insertCount > 0 && iLogRecord.Properties[insertCount - 1].Value.GetType() == typeof(byte[]);
      }

      public string LogName => iLogRecord.LogName;

      public StandardEventLevel Level => (StandardEventLevel)iLogRecord.Level;
    }

  }
}