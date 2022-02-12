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


namespace EventLogMonitor;
public static class CultureSpecificMessage
{
  private const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
  private const int LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryExW", SetLastError = true, ExactSpelling = true)]
  private static extern IntPtr LoadLibraryEx(string libFilename, IntPtr reserved, int flags);

  [DllImport("kernel32.dll")]
  private static extern bool FreeLibrary(IntPtr hModule);

  private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
  private const int FORMAT_MESSAGE_FROM_HMODULE = 0x00000800;
  private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
  private const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;
  private const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
  private const int FORMAT_MESSAGE_MAX_WIDTH_MASK = 0x000000FF;
  private const int USEnglishLCID = 1033;
  readonly private static Dictionary<string, IntPtr> iCatalogueCache = new();

  [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "FormatMessageW", SetLastError = true, BestFitMapping = true, ExactSpelling = true)]
  private static extern int FormatMessageW(
    int dwFlags,
    IntPtr lpSource,
    uint dwMessageId,
    int dwLanguageId,
    ref IntPtr lpBuffer,
    int nSize,
    string[] pArguments);

  public static string GetCultureSpecificMessage(IEventLogRecordWrapper entry, int cultureLCID)
  {
    // make a list of inserts, but ignore the last entry if this is binary
    int insertCount = entry.Properties.Count;
    List<string> insertList = new(insertCount);
    for (int i = 0; i < (insertCount); ++i)
    {
      object insert = entry.Properties[i].Value;
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

    string provider = entry.ProviderName; // e.g "IBM App Connect Enterprise v110011"
    string logName = entry.LogName; // e.g. "Application"
    string providerRegPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\" + logName + @"\" + provider;
    string catalogueLocation = (string)Registry.GetValue(providerRegPath, "EventMessageFile", null);

    if (string.IsNullOrEmpty(catalogueLocation))
    {
      if (EventLogMonitor.LogIsAFile(entry.ContainerLog))
      {
        // see if we have a message dll to use next to the current file
        int index = entry.ContainerLog.LastIndexOf('\\');
        string logBaseName = entry.ContainerLog[(index + 1)..];
        string logBaseLocation = entry.ContainerLog[..(index + 1)];
        index = logBaseName.LastIndexOf('.');
        string fileName = logBaseName[..index];
        fileName += ".dll";
        string fileFullName = logBaseLocation + fileName;
        if (File.Exists(fileFullName))
        {
          catalogueLocation = fileFullName;
        }
      }

      // check again
      if (string.IsNullOrEmpty(catalogueLocation))
      {
        // we don't have a message catalogue DLL so can't get a culture specific message
        return string.Empty;
      }
    }
    else
    {
      if (catalogueLocation.Contains(';'))
      {
        // some registry entries, esp' for device drivers, have multiple options, which are semicolon separated.
        // for now just pick the first, but we will probably have to try each one in a loop at some point
        // however, picking the first seems to work ok for now
        string[] paths = catalogueLocation.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Length >= 1)
        {
          catalogueLocation = paths[0];
        }
      }
    }

    IntPtr dllHandle;
    if (iCatalogueCache.ContainsKey(catalogueLocation))
    {
      dllHandle = iCatalogueCache[catalogueLocation];
    }
    else
    {
      int flags = LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE;
      dllHandle = LoadLibraryEx(catalogueLocation, IntPtr.Zero, flags);
      if (dllHandle != IntPtr.Zero)
      {
        iCatalogueCache[catalogueLocation] = dllHandle;
      }
      else
      {
        // we can't load the message catalogue DLL so can't get a culture specific message
        return string.Empty;
      }
    }

    // The native FormatMessage expects the qualifier as the high word of the msg number
    int finalCode;
    if (entry.Qualifiers > 0)
    {
      finalCode = (int)entry.Qualifiers << 16;
      finalCode += entry.Id;
    }
    else
    {
      finalCode = entry.Id;
    }

    string responseMsg = GetMessage(finalCode, insertList.ToArray(), cultureLCID, dllHandle);
    return responseMsg;
  }
  static string GetMessage(int msgCode, string[] arguments, int cultureLCID, IntPtr moduleHandle)
  {
    //int flags = FORMAT_MESSAGE_FROM_HMODULE | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_ALLOCATE_BUFFER;
    int flags = FORMAT_MESSAGE_FROM_HMODULE | FORMAT_MESSAGE_ALLOCATE_BUFFER;

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
        int length = FormatMessageW(flags, moduleHandle, unchecked((uint)msgCode), currentCulture, ref nativeBuffer, 65535, arguments);
        // Console.WriteLine("Len: " + length + ", lastErr: " + Marshal.GetLastWin32Error()); // debugging

        if (length > 0)
        {
          string formattedString = Marshal.PtrToStringUni(nativeBuffer, length);
          return formattedString;
        }
        else
        {
          int lastError = Marshal.GetLastWin32Error();
          if (lastError == 1815 || (lastError >= 15100 && lastError <= 15108) || lastError == 317)
          {
            // Code definitions nelow are from here: https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes

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

            // try again with US English if we have not already.
            if (currentCulture != USEnglishLCID && currentCulture != 0)
            {
              currentCulture = USEnglishLCID;
              continue;
            }

            if (currentCulture != 0)
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

  // Interface to allow mocking of an EventLogRecord
  public interface IEventLogRecordWrapper
  {
    public string ContainerLog { get; }
    public int? Qualifiers { get; }
    public int Id { get; }
    public string ProviderName { get; }
    public IList<EventProperty> Properties { get; }
    public string LogName { get; }
  }

  // Class to allow mocking of an EventLogRecord
  public class EventLogRecordWrapper : IEventLogRecordWrapper
  {
    private readonly EventRecord iLogRecord;
    public EventLogRecordWrapper(EventRecord logRecord)
    {
      iLogRecord = logRecord;
    }
    public string ContainerLog
    {
      get
      {
        return iLogRecord is EventLogRecord record ? record.ContainerLog : null;
      }
    }

    public int? Qualifiers => iLogRecord.Qualifiers;

    public int Id => iLogRecord.Id;

    public string ProviderName => iLogRecord.ProviderName;

    public IList<EventProperty> Properties => iLogRecord.Properties;

    public string LogName => iLogRecord.LogName;
  }

}
