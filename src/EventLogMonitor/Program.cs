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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;

namespace EventLogMonitor;

public class EventLogMonitor
{
  [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
  static extern ushort SetThreadLocale(ushort langId);

  [DllImport("kernel32.dll")]
  static extern ushort GetThreadLocale();

  [DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
  static extern int LCIDToLocaleName(uint locale, StringBuilder lpName, int cchName, int dwFlags);

  private static readonly bool outputLCID = Environment.GetEnvironmentVariable("EVENTLOGMONITOR_OUTPUT_LCID") != null;
  private static volatile int s_entriesDisplayed = 0;

  private const int LOCALE_ALLOW_NEUTRAL_NAMES = 0x08000000;
  public EventLogMonitor()
  {

  }

  private bool ParseArguments(SimpleArgumentProcessor myArgs)
  {
    // For Usage see help
    myArgs.SetOptionalFlaggedArgument("-p");
    myArgs.SetOptionalBooleanArgument("-1");
    myArgs.SetOptionalBooleanArgument("-2");
    myArgs.SetOptionalBooleanArgument("-3");
    myArgs.SetOptionalBooleanArgument("-v");
    myArgs.SetOptionalBooleanArgument("-b1");
    myArgs.SetOptionalBooleanArgument("-b2");
    myArgs.SetOptionalBooleanArgument("-nt");
    myArgs.SetOptionalBooleanArgument("-tf");
    myArgs.SetOptionalBooleanArgument("-utc");
    myArgs.SetOptionalBooleanArgument("-d");
    myArgs.SetOptionalFlaggedArgument("-i");
    myArgs.SetOptionalFlaggedArgument("-s");
    myArgs.SetOptionalFlaggedArgument("-c");
    myArgs.SetOptionalFlaggedArgument("-l");
    myArgs.SetOptionalFlaggedArgument("-fi");
    myArgs.SetOptionalFlaggedArgument("-fx");
    myArgs.SetOptionalFlaggedArgument("-fn");
    myArgs.SetOptionalBooleanArgument("-fw");
    myArgs.SetOptionalBooleanArgument("-fe");
    myArgs.SetOptionalBooleanArgument("-fc");
    myArgs.SetOptionalBooleanArgument("-nopatch");

    myArgs.SetOptionalBooleanArgument("-?"); // help
    myArgs.SetOptionalBooleanArgument("-help"); // help
    myArgs.SetOptionalBooleanArgument("-version"); // version

    bool validArgs = myArgs.ParseAndValidateArguments();
    if (!validArgs)
    {
      return InvalidArguments(null);
    }

    if (myArgs.GetBooleanArgument("-?") || myArgs.GetBooleanArgument("-help"))
    {
      DisplayHelp();
      return false;
    }

    if (myArgs.GetBooleanArgument("-version"))
    {
      DisplayVersion();
      return false;
    }

    string record = myArgs.GetFlaggedArgument("-p");
    if (record == "*")
    {
      iPreviousRecordCount = uint.MaxValue;
    }
    else
    {
      // ignore parse failure as default will be 0 which is fine
      _ = uint.TryParse(record, out iPreviousRecordCount);
    }

    string index = myArgs.GetFlaggedArgument("-i");
    bool indexSet = false;

    if (!string.IsNullOrEmpty(index))
    {
      indexSet = true;
      if (index.Contains('-'))
      {
        char[] match = ['-'];
        string[] range = index.Split(match, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (range.Length != 2)
        {
          return InvalidArguments("invalid range, use 'x-y' to specify a range");
        }
        else
        {
          // ignore parse failure as default will be 0
          _ = uint.TryParse(range[0], out iRecordIndexMin);
          _ = uint.TryParse(range[1], out iRecordIndexMax);

          if (iRecordIndexMax < iRecordIndexMin)
          {
            return InvalidArguments("index max > index min");
          }
          iOriginalIndex = iRecordIndexMin;

          // set up the indexes to output a range of events. 
          // they may not all exist but we try to include them
          iRecordIndexRange = iRecordIndexMax - iRecordIndexMin + 1;

          if (iPreviousRecordCount < iRecordIndexRange)
          {
            iPreviousRecordCount = iRecordIndexRange;
          }
        }
      }
      else
      {
        // ignore parse failure as default will be 0
        _ = uint.TryParse(index, out iRecordIndexMin);
        iOriginalIndex = iRecordIndexMin;
        if (iPreviousRecordCount > 0)
        {
          if (iPreviousRecordCount == uint.MaxValue)
          {
            // set up the indexes to output all events after and including the index event
            iRecordIndexMax = iPreviousRecordCount;
            iRecordIndexRange = iPreviousRecordCount;
          }
          else
          {
            // set up the indexes to output -p events before and after the index
            // note those events may not always exist but we try to include them
            iRecordIndexMax = iRecordIndexMin + iPreviousRecordCount;
            if (iPreviousRecordCount >= iRecordIndexMin)
            {
              //make sure we don't wrap
              iRecordIndexMin = 1;
            }
            else
            {
              iRecordIndexMin -= iPreviousRecordCount;
            }
            iRecordIndexRange = (iPreviousRecordCount * 2) + 1;
          }
        }
        else
        {
          // set up the indexes to output a single event
          iRecordIndexMax = iRecordIndexMin;
          iRecordIndexRange = 1;
        }

      }
    }

    int count = 0;
    iVerboseOutput = myArgs.GetBooleanArgument("-v");
    iMinimalOutput = myArgs.GetBooleanArgument("-1");
    iMediumOutput = myArgs.GetBooleanArgument("-2");
    iFullOutput = myArgs.GetBooleanArgument("-3");
    bool doNotTail = myArgs.GetBooleanArgument("-nt");
    if (doNotTail)
    {
      iTailEventLog = false;
    }

    if (myArgs.GetBooleanArgument("-tf"))
    {
      iTimestampFirst = true;
    }

    if (myArgs.GetBooleanArgument("-utc"))
    {
      iTimestampInUTC = true;
    }

    iDisplayLogs = myArgs.GetBooleanArgument("-d");

    string filter = myArgs.GetFlaggedArgument("-fi"); // filter include
    if (!string.IsNullOrEmpty(filter))
    {
      iEntryInclusiveFilter = filter;
    }

    filter = myArgs.GetFlaggedArgument("-fx"); // filter exclude
    if (!string.IsNullOrEmpty(filter))
    {
      iEntryExclusiveFilter = filter;
    }

    bool level = myArgs.GetBooleanArgument("-fw"); // filter to only show warnings and above
    if (level)
    {
      iLogLevel = 3; // This level equates to warning events
    }

    level = myArgs.GetBooleanArgument("-fe"); // filter to only show errors - overrides an fw
    if (level)
    {
      iLogLevel = 2; // This level equates to error events (will also catch critical events)
    }

    level = myArgs.GetBooleanArgument("-fc"); // filter to only show critical errors - overrides an fe amd fw
    if (level)
    {
      iLogLevel = 1; // This level equates to critical error events only
    }

    // filter to show specific event IDs - must be after -fw, -fe and -fc to capture iLogLevel first
    filter = myArgs.GetFlaggedArgument("-fn");
    if (!String.IsNullOrEmpty(filter) || (iLogLevel != -1))
    {
      try
      {
        EventLogSimpleQueryGenerator queryGenerator = new(filter, iLogLevel);
        iEntryEventIdAndLogLevelQuery = queryGenerator.QueryString;
      }
      catch (ArgumentException e)
      {
        // The error message should say what is wrong here
        return InvalidArguments("Invalid -fn filter: " + e.Message);
      }
    }


    string logName = myArgs.GetFlaggedArgument("-l");
    if (!string.IsNullOrEmpty(logName))
    {
      iLogName = logName;
    }
    else
    {
      if (iDisplayLogs)
      {
        iLogName = ""; // force empty to allow -l and -i to be specified
      }
    }

    string source = myArgs.GetFlaggedArgument("-s");
    if (!string.IsNullOrEmpty(source))
    {
      if (indexSet)
      {
        return InvalidArguments("-s not allowed with -i");
      }

      iSource = source;
      if (iSource.Contains(','))
      {
        char[] match = { ',' };
        iMultiMatch = iSource.Split(match, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      }
      else
      {
        iMultiMatch = Array.Empty<string>(); // clear the default
      }

    }
    else
    {
      // as the user has not specified a source, if the log name is not "Application" we should
      // not default to looking for IIB entries only
      if (!iLogName.Equals("Application"))
      {
        iSource = "*"; //look for all entries
        iMultiMatch = [];
      }
    }

    bool dontPatchProviders = myArgs.GetBooleanArgument("-nopatch");
    if (dontPatchProviders)
    {
      CultureSpecificMessage.SpecialCaseMissingProviders = false;
    }

    // make the default one first
    iUSDefaultCulture = new CultureInfo("En-US");
    string culture = myArgs.GetFlaggedArgument("-c");
    if (!string.IsNullOrEmpty(culture))
    {
      iDefaultCulture = culture;
      iCultureSet = true;
    }

    iMinBinaryOutput = myArgs.GetBooleanArgument("-b1");
    iFullBinaryOutput = myArgs.GetBooleanArgument("-b2");

    if (iMinimalOutput)
    {
      ++count;
    }

    if (iMediumOutput)
    {
      ++count;
    }

    if (iFullOutput)
    {
      ++count;
    }

    if (count > 1)
    {
      return InvalidArguments("only one of options '-1', '-2' and '-3' may be specified");
    }

    if (iCultureSet)
    {
      bool isValidCulture;
      bool isNumber = false;
      try
      {
        // allow LCIDs to be specified directly as numbers
        int defaultCultureAsLCID = -1;
        try
        {
          int numbase = 10;
          if (iDefaultCulture.ToLower().StartsWith("0x"))
          {
            numbase = 16;
          }
          defaultCultureAsLCID = Convert.ToInt32(iDefaultCulture, numbase);
          if (defaultCultureAsLCID >= 0)
          {
            isNumber = true;
            StringBuilder cultureName = new(100);
            int result = LCIDToLocaleName((uint)defaultCultureAsLCID, cultureName, cultureName.Capacity, LOCALE_ALLOW_NEUTRAL_NAMES);
            if (result == 0)
            {
              // Console.WriteLine("ERROR: GLE = {0}", Marshal.GetLastWin32Error());
              cultureName.Append("Unknown");
            }

            iChosenCultureName = cultureName.ToString();
            iChosenCultureLCID = defaultCultureAsLCID;
          }
        }
        catch (Exception)
        {
          isNumber = false;
        }

        if (!isNumber)
        {
          CultureInfo tmp = CultureInfo.CreateSpecificCulture(iDefaultCulture);
          iChosenCultureLCID = tmp.LCID;
          iChosenCultureName = tmp.Name;
        }

        if (iChosenCultureLCID == 127)
        {
          isValidCulture = false; // 127 is invarient culture which is no use to us
        }
        else
        {
          isValidCulture = true;
        }
      }
      catch (ArgumentException)
      {
        // in .NET 4.0, CreateSpecificCulture can throw a CultureNotFoundException, a subclass of ArgumentException!
        isValidCulture = false;
      }

      if (!isValidCulture)
      {
        Console.WriteLine("Culture is not supported. '" + iDefaultCulture + "' is an invalid culture identifier. Defaulting to 'En-US'.");
        iChosenCultureLCID = iUSDefaultCulture.LCID;
        iChosenCultureName = iUSDefaultCulture.Name;
      }
    }
    return true;
  }

  private static bool InvalidArguments(string extraInfo)
  {
    if (!string.IsNullOrEmpty(extraInfo))
    {
      Console.WriteLine("Invalid arguments or invalid argument combination: {0}.", extraInfo);
    }
    else
    {
      Console.WriteLine("Invalid arguments or invalid argument combination.");
    }
    DisplayHelp();
    return false;
  }

  private void DisplayAvailableLogs()
  {
    int providerCount = 0;
    EventLog[] logsa = EventLog.GetEventLogs();
    Dictionary<string, string> oldDisplayNames = new();
    foreach (EventLog log in logsa)
    {
      try
      {
        oldDisplayNames.Add(log.Log, log.LogDisplayName);
      }
      catch (SecurityException)
      {
        // we will always fail to access the Security log if we are not admin
      }
    }

    EventLogSession session = EventLogSession.GlobalSession;
    string[] logsToMatch;
    bool localFile = false;
    bool matchAll = false;
    PathType pathType = PathType.LogName;
    var allLogs = session.GetLogNames().ToArray();
    if (LogIsAFile(iLogName))
    {
      pathType = PathType.FilePath;
      logsToMatch = new string[] { iLogName };
      localFile = true;
      allLogs = logsToMatch; // replace with the file name
    }
    else if (!string.IsNullOrEmpty(iLogName) && (!iLogName.Equals("*")))
    {
      if (iLogName.Contains(','))
      {
        char[] match = { ',' };
        logsToMatch = iLogName.Split(match, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      }
      else
      {
        // assume a single real log name or a partial log name, not a file.
        logsToMatch = new string[] { iLogName };
      }
    }
    else
    {
      matchAll = true;
      logsToMatch = Array.Empty<string>();
    }

    if (!iVerboseOutput)
    {
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine("LogName : Entries : LastWriteTime : [DisplayName]");
      Console.WriteLine("-------------------------------------------------");
      Console.ResetColor();
    }

    int ignoreCount = 0;
    foreach (string current in allLogs)
    {
      try
      {
        EventLogInformation logInfo = session.GetLogInformation(current, pathType);
        if (!LogNameMatch(current, logsToMatch, matchAll))
        {
          continue;
        }

        EventLogConfiguration logConfig = null;
        if (!localFile)
        {
          logConfig = new EventLogConfiguration(current, session);
        }

        string displayName = "";
        if (!localFile)
        {
          if (logConfig.IsClassicLog)
          {
            if (oldDisplayNames.ContainsKey(current))
            {
              displayName = oldDisplayNames[current];
            }
          }
          else
          {
            displayName = logConfig.OwningProviderName;
          }
        }
        else
        {
          displayName = System.IO.Path.GetFileNameWithoutExtension(current);
        }

        long recordCountNull = logInfo.RecordCount ?? -1;
        if (!iVerboseOutput)
        {
          // only output logs that have records that can be read.
          if (recordCountNull > 0)
          {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(current);
            Console.ResetColor();
            Console.WriteLine(" : " + logInfo.RecordCount + " : " + logInfo.LastWriteTime + " : [" + displayName + "]");
            ++providerCount;
          }
        }
        else
        {
          // include more records than above as we count ones with 0 records
          if (recordCountNull >= 0)
          {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(current);
            Console.ResetColor();
            Console.WriteLine("    DisplayName:  " + displayName);
            Console.WriteLine("    Records:      " + logInfo.RecordCount);
            Console.WriteLine("    FileSize:     " + logInfo.FileSize);
            Console.WriteLine("    IsFull:       " + logInfo.IsLogFull);
            Console.WriteLine("    CreationTime: " + logInfo.CreationTime);
            Console.WriteLine("    LastWrite:    " + logInfo.LastWriteTime);
            Console.WriteLine("    OldestIndex:  " + logInfo.OldestRecordNumber);
            if (!localFile)
            {
              Console.WriteLine("    IsClassic:    " + logConfig.IsClassicLog);
              Console.WriteLine("    IsEnabled:    " + logConfig.IsEnabled);
              Console.WriteLine("    LogFile:      " + logConfig.LogFilePath);
              Console.WriteLine("    LogType:      " + logConfig.LogType);
              Console.WriteLine("    MaxSizeBytes: " + logConfig.MaximumSizeInBytes);
              Console.WriteLine("    MaxBuffers:   " + logConfig.ProviderMaximumNumberOfBuffers);
            }
            ++providerCount;
          }

        }

      }
      catch (UnauthorizedAccessException)
      {
        // we will always fail to access the Security log (and others) if we are not admin
        // Console.WriteLine(" Ignoring log: " + current + " (not Admin)"); //debug
        if (LogNameMatch(current, logsToMatch, matchAll))
        {
          ++ignoreCount;
        }
      }
      catch (EventLogException)
      {
        // Currently only "Microsoft-Windows-UAC/Operational : 1 : 07/07/2014 09:44:36 : [Microsoft-Windows-UAC]" on Windows 8.1 and 
        // "Microsoft-Windows-USBVideo/Analytic" on Windows 10 have this exception so ignore for now...
        // Console.WriteLine("    Exception finding log: " + current + " : " + e.ToString());
      }
    }

    Console.WriteLine();
    if (ignoreCount > 0)
    {
      // Although we know how many were skipped, we don't know how many of these have 0 entries and not be shown anyway,
      // so we choose not to put out the count here as it gets confusing if you rerun with Admin and the totals don't match.
      Console.WriteLine("Some providers maybe ignored (not Admin).");
    }

    if (localFile)
    {
      Console.WriteLine(providerCount + " Provider file listed.");
    }
    else
    {
      Console.WriteLine(providerCount + " Providers listed.");
    }

  }

  private void DisplayMatchingEvents()
  {
    //see if the user has given us a path to a file
    PathType pathType = PathType.LogName;
    if (LogIsAFile(iLogName))
    {
      iTailEventLog = false; //cannot tail a file
      pathType = PathType.FilePath;
      if (iPreviousRecordCount == 0)
      {
        // showing 0 events from a file is not much use so default to showing all events
        iPreviousRecordCount = uint.MaxValue;
      }
    }

    iEventLogQuery = new EventLogQuery(iLogName, pathType, iEntryEventIdAndLogLevelQuery)
    {
      ReverseDirection = true //we want newest to oldest
    };

    using var reader = new EventLogReader(iEventLogQuery);

    //display existing entries before we move onto waiting for new entries to appear
    List<EventRecord> entries = [];
    int matched = 0;
    if (iRecordIndexMin > 0)
    {
      // we are looking to output either a single event, a complete range (-i xxx-yyy) or a number before and after the index chosen (-i zzz -p x) 
      // where we have x events before and after the event (assuming events with those indexes exist).
      for (EventRecord current = reader.ReadEvent(); (matched <= iRecordIndexRange) && (current != null); current = reader.ReadEvent())
      {
        if (current.RecordId >= iRecordIndexMin && current.RecordId <= iRecordIndexMax)
        {
          ++matched;
          entries.Add(current);
        }
      }

      // output matched entries in event log order
      OutputEntriesInEventLogOrder(entries);

      if (s_entriesDisplayed == 0)
      {
        Console.WriteLine("No entries found with matching index " + iOriginalIndex + " in the " + this.iLogName + " log");
      }
      else if (s_entriesDisplayed == 1)
      {
        Console.WriteLine("Matching entry found in the " + this.iLogName + " log");
      }
      else
      {
        Console.WriteLine("\nFound " + s_entriesDisplayed + " Matching entries found in the " + this.iLogName + " log");
      }

    }
    else
    {
      if (iPreviousRecordCount > 0)
      {
        for (EventRecord current = reader.ReadEvent(); (matched < iPreviousRecordCount) && (current != null); current = reader.ReadEvent())
        {
          if (iMultiMatch.Length > 0)
          {
            foreach (string x in iMultiMatch)
            {
              if (current.ProviderName.Contains(x))
              {
                ++matched;
                entries.Add(current);
                break; // escape at the first match
              }
            }
          }
          else
          {
            if (iSource == "*" || current.ProviderName.Contains(iSource))
            {
              ++matched;
              entries.Add(current);
            }
          }
        }

        // output matched entries in event log order
        OutputEntriesInEventLogOrder(entries);

      }

      if (iTailEventLog)
      {
        // This is a horrible hack. "dotnet test" does something odd to the console stdin/out/err streams
        // in that they always return 'false', 'false', 'true' for Console.IsXXXRedirected no matter what the 
        // test tries to do. So for now we allow an environment to force console input redirection for testing.
        bool testInputRedirected = Environment.GetEnvironmentVariable("EVENTLOGMONITOR_INPUT_REDIRECTED") != null;
        bool inputRedirected = false;
        if (Console.IsInputRedirected || testInputRedirected)
        {
          inputRedirected = true;
        }

        // if we have displayed no previous entries, we should display a simple message
        if (s_entriesDisplayed == 0)
        {
          string toLog = EventSourceAsString();
          Console.WriteLine("Waiting for events from the " + this.iLogName + " log matching the event source '" + toLog + "'.");
          if (Console.IsOutputRedirected || testInputRedirected)
          {
            Console.WriteLine("");
          }
          else
          {
            Console.WriteLine("Press <Enter>, 'Q' or <Esc> to exit or press 'S' for current stats...");
          }
        }

        iEventLogQuery.ReverseDirection = false; // cannot tail a log with direction reversed!
        EventLogWatcher watcher = new(iEventLogQuery);
        watcher.EventRecordWritten += new EventHandler<EventRecordWrittenEventArgs>(EventLogEventRead);
        watcher.Enabled = true;

        // TODO check if VS still has this old problem:
        // if we are redirected, we could be running inside Eclipse or Visual Studio as an external tool.
        // Or we could be redirected to a file instead. Either way, we can't wait on ReadLine to exit
        // as it may return immediately with end of stream (NULL) as it does (did?) in VS.
        while (true)
        {
          ConsoleKey keyPressed;
          if (inputRedirected)
          {
            // We cannot use Console.ReadKey if input is redirected so use the TextReader directly
            TextReader textReader = Console.In;

            int inputValue = textReader.Read();
            if (inputValue == -1)
            {
              break; // EOS, we are done
            }

            // we must be upper case as all ConsoleKeys are
            char inputChar = Char.ToUpper((char)inputValue);

            // we only accept a limited number of input chars so it is safer to check specifically for now
            keyPressed = (int)inputChar switch
            {
              (int)ConsoleKey.Enter => ConsoleKey.Enter,
              (int)ConsoleKey.Escape => ConsoleKey.Escape,
              (int)ConsoleKey.Q => keyPressed = ConsoleKey.Q,
              (int)ConsoleKey.S => keyPressed = ConsoleKey.S,
              _ => ConsoleKey.I, // I for ignored
            };
          }
          else
          {
            try
            {
              // wait for user to exit - pass true to stop key echoing
              ConsoleKeyInfo keyPressedInfo = Console.ReadKey(true); // TODO check this works in eclipse...
              keyPressed = keyPressedInfo.Key;
            }
            catch (InvalidOperationException)
            {
              // this can happen if stdin is redirected to read from a file
              inputRedirected = true;
              continue;
            }
          }

          // Console.WriteLine("KeyPressed: '" + keyPressed + "'"); // debug
          if (keyPressed == ConsoleKey.Enter || keyPressed == ConsoleKey.Escape || keyPressed == ConsoleKey.Q)
          {
            Console.WriteLine();
            break; // we are done
          }

          // allow a single 's' to mean 'show stats and continue'
          if (keyPressed == ConsoleKey.S)
          {
            // TODO more stats (count of warning, errors etc)
            Console.ResetColor();
            Console.WriteLine(s_entriesDisplayed + " Entries shown so far from the " + this.iLogName + " log. Waiting for more events...");
          }

          // ignore other key presses
        }

        // stop receiving events as we are shutting down
        watcher.Enabled = false;
      }

      // Always finish by showing what we displayed
      Console.ResetColor();
      string toLog2 = EventSourceAsString();
      Console.WriteLine(s_entriesDisplayed + " Entries shown from the " + this.iLogName + " log matching the event source '" + toLog2 + "'.");
    }
  }

  // mostly displays any entry passed in...
  private bool DisplayEventLogEntry(EventRecord entry)
  {
    bool brokerEventLogEntry = IsBrokerEntry(entry.ProviderName);

    StandardEventLevel level = StandardEventLevel.LogAlways;
    if (entry.Level.HasValue)
    {
      level = (StandardEventLevel)entry.Level;
    }

    string type;
    ConsoleColor textColour;
    if (entry.LogName == "Security")
    {
      if (((ulong)entry.Keywords ^ 0x8010000000000000) == 0x0)
      {
        type = "F"; textColour = ConsoleColor.Red; // Audit Failure
      }
      else
      {
        type = "S"; textColour = ConsoleColor.Green; // Audit Success
      }
    }
    else
    {
      switch (level)
      {
        case StandardEventLevel.Informational: type = "I"; textColour = ConsoleColor.Green; break;
        case StandardEventLevel.Warning: type = "W"; textColour = ConsoleColor.Yellow; break;
        case StandardEventLevel.Error: type = "E"; textColour = ConsoleColor.Red; break;
        case StandardEventLevel.Critical: type = "C"; textColour = ConsoleColor.DarkRed; break;
        case StandardEventLevel.LogAlways: type = "I"; textColour = ConsoleColor.Green; break;
        case StandardEventLevel.Verbose: type = "V"; textColour = ConsoleColor.Green; break;
        default: type = "I"; textColour = ConsoleColor.Green; break;
      }
    }

    string message = string.Empty;
    string win32Message = string.Empty;
    bool messagePatched = false;
    int lcidUsed = -1;
    try
    {

      CultureSpecificMessage.EventLogRecordWrapper wrapper = null;
      if (iCultureSet)
      {
        // try to get the specific language version of the message.
        // however, this may not be available so the message may not be found
        // This is works best when the user has provided a DLL along side an EVTX
        wrapper = new(entry);
        message = CultureSpecificMessage.GetCultureSpecificMessage(wrapper, iChosenCultureLCID, iChosenCultureName);
        if (string.IsNullOrEmpty(message))
        {
          // try again with the thread set to the requested culture - but must reset
          // this will pick up any MTA files
          ushort origLCID = (ushort)GetThreadLocale();
          try
          {
            SetThreadLocale((ushort)iChosenCultureLCID);
            message = entry.FormatDescription();
            lcidUsed = iChosenCultureLCID;
          }
          finally
          {
            SetThreadLocale(origLCID);
          }
        }
        else
        {
          lcidUsed = iChosenCultureLCID;
        }

      }
      else
      {
        // Get the message if the catalogue is correct and present in the registry or there is an MTA file along with the EVTX
        message = entry.FormatDescription();
        // retry with alternate culture lookup as this tries different places to find a catalogue and will override the console culture
        if (string.IsNullOrEmpty(message))
        {
          // try with a Language Neutral (0) culture but with En-US name to mimic what C# does by default which seems to
          // be to call FormatMessage with a Lang Neutral (0) and so will prefer 0 above other LCIDs!
          // this will fall back to trying En-US if Language Neutral (0) fails to find a match and
          // will work when the user has provided a DLL alongside an EVTX which has neutral or US messages in it
          wrapper = new(entry);
          message = CultureSpecificMessage.GetCultureSpecificMessage(wrapper, 0, iUSDefaultCulture.Name);
          lcidUsed = iUSDefaultCulture.LCID;
        }
        else
        {
          lcidUsed = GetThreadLocale();
        }
      }

      // Fallback forcing thread to US (but must reset)
      if (string.IsNullOrEmpty(message))
      {
        // try again with the console default culture instead - but must reset
        ushort origLCID = (ushort)GetThreadLocale();
        try
        {
          SetThreadLocale((ushort)iUSDefaultCulture.LCID); // force US
          message = entry.FormatDescription();
          if (string.IsNullOrEmpty(message))
          {
            lcidUsed = -1;
          }
          else
          {
            lcidUsed = iUSDefaultCulture.LCID;
          }
        }
        finally
        {
          SetThreadLocale(origLCID);
        }
      }

      // Try one last time to get a patched version of the message
      // where we just get the inserts in a string, assuming patching is allowed.
      // This should work for all events except those that have no inserts or just a single binary insert
      // but we are not getting a "real" message just whatever inserts are being written to the log
      // "Universal Print" is an example of this case as it is registered but the .mui file is missing 
      // on the machines tested!
      if (string.IsNullOrEmpty(message))
      {
        wrapper ??= new(entry);
        message = CultureSpecificMessage.GetPatchedMessageFromFormatString(wrapper);
        messagePatched = true;
      }
    }
    catch (EventLogException e)
    {
      // get the internal exception code as 1168 means we probably have an invalid MTA file
      // TODO check and use code
      int exceptionCode = e.GetFieldValue<int>("_errorCode");
      lcidUsed = -1;
      //Console.WriteLine($"EXCEPTION CODE: {exceptionCode}"); // debug
      //Console.WriteLine(e.ToString());  // debug
      win32Message = e.Message; // used if there are no qualifiers on the message ID
    }

    // see if we still have nothing!
    if (string.IsNullOrEmpty(message))
    {
      messagePatched = false; // just in case - can't be patched if we don't have a msg!

      // build our own response message like the event log API does!
      message = "The description for Event ID " + entry.Id + " from source " + entry.ProviderName + " cannot be found. " +
      "Either the component that raises this event is not installed on your local computer or the installation is corrupted. " +
      "You can install or repair the component on the local computer.\r\n\r\n" +
      "If the event originated on another computer, the display information had to be saved with the event.\r\n\r\n";

      bool first = true;
      foreach (EventProperty prop in entry.Properties)
      {
        if (first)
        {
          message += "The following information was included with the event:\r\n";
          first = false;
        }

        // byte[] will normally be binary data, not an insert!
        if (prop.Value.GetType() != typeof(byte[]))
        {
          message += prop.Value.ToString();
        }
        message += "\r\n";
      }

      if (win32Message.Length > 0)
      {
        message += win32Message;
      }
      else
      {
        message += "The message resource is present but the message was not found in the message table";
      }

      if (entry.Qualifiers == 0)
      {
        // So this is a quick and simple way to get the text for a win32 error code.
        // We could call our own FormatMessage but that would be more effort and I'm
        // not sure there would be any extra benefit.
        Win32Exception win32Error = new(entry.Id);
        win32Message = win32Error.Message;
      }
    }

    // full is everything (description, explanation and user action) and we output it "as is"
    if (!iFullOutput)
    {
      // for medium and minimal we trim the beginning to make sure we are starting at real text
      // as some events start with a leading space or a \r\n\r\n which throws off the splitting.
      message = message.TrimStart();
      if (iMediumOutput)
      {
        // description and explanation
        int index = message.IndexOf(iEventLongSeparater);
        if (index > 0)
        {
          index = message.IndexOf(iEventLongSeparater, index + iEventLongSeparater.Length);
          if (index > 0)
          {
            message = message[0..index];
          }
        }
        else
        {
          // some events only use \r\n
          index = message.IndexOf(iEventShortSeparater);
          if (index > 0)
          {
            index = message.IndexOf(iEventShortSeparater, index + iEventShortSeparater.Length);
            if (index > 0)
            {
              message = message[0..index];
            }
          }
        }
      }
      else
      {
        //minimal is just description (this is the default)
        int index = message.IndexOf(iEventLongSeparater);
        if (index > 0)
        {
          message = message[0..index];
        }
        else
        {
          // some events only use \r\n
          index = message.IndexOf(iEventShortSeparater);
          if (index > 0)
          {
            message = message[0..index];
          }
        }

        // for minimal we only want one line if possible, so remove any line breaks left after trimming
        // and replace with a ' ' space or a ". " if appropriate.
        message = message.TrimEnd();

        ReadOnlySpan<char> messageSpan = message;
        // first look for "\r\n"
        int count = messageSpan.Count(iEventShortSeparater);
        if (count > 0)
        {
          message = EventLogUtils.RemoveChars(message, messageSpan, iEventShortSeparater, count);
        }

        // next look for any remaining '\n'
        messageSpan = message;
        count = messageSpan.Count("\n");
        if (count > 0)
        {
          message = EventLogUtils.RemoveChars(message, messageSpan, "\n", count);
        }

        // finally look for any remaining '\r'
        messageSpan = message;
        count = messageSpan.Count("\r");
        if (count > 0)
        {
          message = EventLogUtils.RemoveChars(message, messageSpan, "\r", count);
        }
      }
    }

    // remove any trailing junk
    message = message.TrimEnd(iTrimChars);

    // check to see if we need to filter out the entry, now we have formatted it.
    if (!string.IsNullOrEmpty(iEntryInclusiveFilter))
    {
      if (!message.Contains(iEntryInclusiveFilter))
      {
        // Console.Write("Ignoring inc entry: " + entry.Id + "\n");
        return false;
      }
    }

    if (!string.IsNullOrEmpty(iEntryExclusiveFilter))
    {
      if (message.Contains(iEntryExclusiveFilter))
      {
        // Console.Write("Ignoring exc entry: " + entry.Id + "\n");
        return false;
      }
    }

    if (iTimestampFirst)
    {
      Console.ForegroundColor = ConsoleColor.White;
      if (iTimestampInUTC)
      {
        outputTimestampInUTC(entry, true);
      }
      else
      {
        Console.Write(entry.TimeCreated + "." + entry.TimeCreated.Value.Millisecond + ": ");
      }

      Console.ResetColor();
    }

    Console.ForegroundColor = textColour;
    if (brokerEventLogEntry)
    {
      Console.Write("BIP");
    }

    Console.Write(entry.Id + type);

    if (messagePatched)
    {
      Console.ForegroundColor = ConsoleColor.DarkYellow;
      Console.Write(" [P]");
      Console.ForegroundColor = textColour;
    }

    Console.Write(": ");

    bool forceMinBinaryOutput = false;
    if (level == StandardEventLevel.Error || level == StandardEventLevel.Critical)
    {
      // force binary tracing for errors
      forceMinBinaryOutput = true;
    }

    Console.ResetColor();
    Console.Write(message);
    if (!iTimestampFirst)
    {
      Console.ForegroundColor = ConsoleColor.White;
      if (iTimestampInUTC)
      {
        outputTimestampInUTC(entry, false);
      }
      else
      {
        Console.Write(" [" + entry.TimeCreated + "." + entry.TimeCreated.Value.Millisecond + "]\n");
      }

      Console.ResetColor();
    }
    else
    {
      Console.Write("\n");
    }

    if (iVerboseOutput)
    {
      EventLogRecord logRecord = (EventLogRecord)entry;
      Console.ForegroundColor = ConsoleColor.DarkGray;
      Console.Write("Machine: {0}. Log: {1}. Source: {2}.", logRecord.MachineName, logRecord.ContainerLog, logRecord.ProviderName);

      if (logRecord.UserId != null)
      {
        string name = logRecord.UserId.ToString();
        Console.Write(" User: {0}.", name);
      }

      if (logRecord.ProcessId != 0)
      {
        string procId = logRecord.ProcessId.ToString();
        Console.Write(" ProcessId: {0}.", procId);
      }

      if (logRecord.ThreadId != 0)
      {
        string tId = logRecord.ThreadId.ToString();
        Console.Write(" ThreadId: {0}.", tId);
      }

      if (logRecord.Version != 0)
      {
        string ver = logRecord.Version.ToString();
        Console.Write(" Version: {0}.", ver);
      }

      if (!string.IsNullOrEmpty(win32Message))
      {
        Console.Write(" Win32Msg: {0} ({1}).", win32Message, entry.Id);
      }

      if (outputLCID && lcidUsed >= 0)
      {
        Console.Write(" LCID: {0}.", lcidUsed);
      }

      Console.WriteLine(); // finish with a blank line
      Console.ResetColor();
    }

    if (iMinBinaryOutput || iFullBinaryOutput || forceMinBinaryOutput)
    {
      byte[] data = null;
      int count = entry.Properties.Count;
      if (count > 0)
      {
        data = entry.Properties[count - 1].Value as byte[];
      }
      if (iFullBinaryOutput)
      {
        BinaryDataFormatter.OutputFormattedBinaryData(data, (long)entry.RecordId);
      }
      else
      {
        BinaryDataFormatter.OutputFormattedBinaryDataAsString(data, (long)entry.RecordId);
      }
    }

    return true;
  }

  static void Main(string[] args)
  {
    // Force output encoding to UTF-8
    Console.OutputEncoding = System.Text.Encoding.UTF8;

    //var currentLocale = GetThreadLocale();
    //Console.WriteLine("CURRENT LOCALE: " + currentLocale);

    // create and execute the monitoring object
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    if (initialized)
    {
      monitor.MonitorEventLog();
    }
  }

  public bool Initialize(string[] args)
  {
    SimpleArgumentProcessor myArgs = new(args);
    bool ok = ParseArguments(myArgs);
    if (!ok)
    {
      return false;
    }

    // test platform (XP needs an older API)
    OperatingSystem os = Environment.OSVersion;
    Version version = os.Version;
    if ((os.Platform == PlatformID.Win32NT) && (version.Major < 6))
    {
      Console.WriteLine("EventLogMonitor is not supported on this platform; use Windows 7 or above.");
      return false;
    }

    iInitialized = true;
    s_entriesDisplayed = 0;
    return true;
  }

  private static bool IsBrokerEntry(string toMatch)
  {
    foreach (string x in iBrokerMatch)
    {
      if (toMatch.Contains(x))
      {
        return true; // escape at the first match
      }
    }
    return false;
  }

  public void MonitorEventLog()
  {
    if (!iInitialized)
    {
      // it would be an error by the caller to get there
      DisplayHelp();
      return;
    }

    try
    {
      if (iDisplayLogs)
      {
        DisplayAvailableLogs();
      }
      else
      {
        DisplayMatchingEvents();
      }
    }
    catch (EventLogNotFoundException e)
    {
      Console.WriteLine(e.Message);
      if (e.Message.Contains("could not be found"))
      {
        Console.WriteLine("Make sure the event log '" + iLogName + "' exists.");
      }
    }
    catch (EventLogException e)
    {
      // general error
      Console.WriteLine(e.Message);
      if (iVerboseOutput)
      {
        Console.WriteLine(e.StackTrace);
      }
    }
    catch (InvalidOperationException e)
    {
      Console.WriteLine(e.Message);
      if (e.Message.Contains("does not exist"))
      {
        Console.WriteLine("Make sure the event log '" + iLogName + "' exists.");
      }
    }
    catch (SecurityException e)
    {
      Console.WriteLine(e.Message);
      if (iLogName.ToLower().Equals("security"))
      {
        // we know its Security
        Console.WriteLine("Run from an elevated command prompt to access the 'Security' event log.");
      }
      else
      {
        // probably a similar problem
        Console.WriteLine("Try running from an elevated command prompt to access the '" + iLogName + "' log.");
      }
    }
    catch (UnauthorizedAccessException e)
    {
      Console.WriteLine(e.Message);
      if (iLogName.ToLower().Equals("security"))
      {
        // we know its Security
        Console.WriteLine("Run from an elevated command prompt to access the 'Security' event log.");
      }
      else
      {
        // probably a similar problem
        Console.WriteLine("Try running from an elevated command prompt to access the '" + iLogName + "' log.");
      }
    }
    catch (Exception e)
    {
      // unknown general error
      Console.WriteLine("\n" + e.Message);
      if (iVerboseOutput)
      {
        Console.WriteLine(e.StackTrace);
      }
      Console.WriteLine("Problem encountered.\nPlease raise an issue at https://github.com/m-g-k/EventLogMonitor/issues for a fix");
    }
    finally
    {
      // we may well have an exception that means we leave the colour set, so make sure we always reset it.
      Console.ResetColor();
    }
  }

  readonly private Char[] iTrimChars = new Char[] { ' ', '\n', '\t', '\r' };
  readonly private string iEventLongSeparater = "\r\n\r\n";
  readonly private string iEventShortSeparater = "\r\n";
  private bool iMinimalOutput = true; //the default
  private bool iMediumOutput = false;
  private bool iFullOutput = false;
  private bool iMinBinaryOutput = false;
  private bool iFullBinaryOutput = false;
  private bool iTailEventLog = true;
  private bool iVerboseOutput = false;
  private string iSource = ""; // default to WMB
  private static readonly string[] iBrokerMatch = { "IBM Integration", "WebSphere Broker", "IBM App Connect Enterprise" };
  private string[] iMultiMatch = iBrokerMatch; // initially we assume broker
  private string iLogName = "Application";
  private string iDefaultCulture = "en-US";
  private string iEntryInclusiveFilter = "";
  private string iEntryExclusiveFilter = "";
  private string iEntryEventIdAndLogLevelQuery = null; // query must be null to represent no query by default
  private bool iCultureSet = false;
  private bool iTimestampFirst = false;
  private bool iTimestampInUTC = false;
  private uint iOriginalIndex = 0;
  private uint iRecordIndexMin = 0;
  private uint iRecordIndexMax = 0;
  private uint iRecordIndexRange = 0;
  private uint iPreviousRecordCount = 0;
  private int iChosenCultureLCID = 0;
  private string iChosenCultureName = "";
  private CultureInfo iUSDefaultCulture = null;
  private EventLogQuery iEventLogQuery = null;
  private bool iDisplayLogs;
  private int iLogLevel = -1; // include every type of event
  private bool iInitialized = false;

  private static void DisplayHelp()
  {
    Console.WriteLine("EventLogMonitor : Version {0} : https://github.com/m-g-k/EventLogMonitor", GetProductVersion());
    Console.WriteLine("Usage:");
    Console.WriteLine("  Usage 1 : EventLogMonitor [-p <count>] [-1|-2|-3] [-s <src>] [-nt] [-v]");
    Console.WriteLine("                            [-b1] [-b2] [-l <log>] [-c <culture>] [-tf] [-utc]");
    Console.WriteLine("                            [-fi <filt>] [-fx <filt>] [-fn <IDs>] [-fw|-fe|-fc]");
    Console.WriteLine("  Usage 2 : EventLogMonitor -i index [-v] [p <count>] [-c <culture>]");
    Console.WriteLine("                            [-b1] [-b2] [-fi <filt>] [-fx <filt>] [-fn <IDs>]");
    Console.WriteLine("                            [-fw | -fe | -fc]");
    Console.WriteLine("                            [-1|-2|-3] [-l <log>] [-tf] [-utc]");
    Console.WriteLine("  Usage 3 : EventLogMonitor -d [-v] [-l <log>]");
    Console.WriteLine("Examples:");
    Console.WriteLine("  EventLogMonitor -p * -s *");
    Console.WriteLine("  EventLogMonitor -p 10 -2");
    Console.WriteLine("  EventLogMonitor -i 115324 -b1");
    Console.WriteLine("  EventLogMonitor -p 5 -s * -l System");
    Console.WriteLine("  EventLogMonitor -p 10 -s \"Integration,MQ\" -b1 -2");
    Console.WriteLine("  EventLogMonitor -p * -s \"Browser Agent\" -l c:\\temp\\mylog.evtx");
    Console.WriteLine("  EventLogMonitor -i \"1127347-1127350\" -b1");
    Console.WriteLine("  EventLogMonitor -s * -fn \"1, 2, 50-90, -55-60, -88\" -p 10");
    Console.WriteLine("Options:");
    Console.WriteLine("  -p  show the last <count> entries from the event log for the given <source>.");
    Console.WriteLine("      Use a '*' for all event log entries.");
    Console.WriteLine("  -1  Minimal output (description only). This is the default output format.");
    Console.WriteLine("  -2  Medium output (description and explanation only).");
    Console.WriteLine("  -3  Full output (description, explanation and user action).");
    Console.WriteLine("  -b1 Show any binary info as text followed by the index of the entry.");
    Console.WriteLine("  -b2 Show any binary info as a hex dump followed by the index of the entry.");
    Console.WriteLine("  -nt No tailing - tailing the event log is the default for usage 1.");
    Console.WriteLine("  -tf Display the timestamp first rather than last for each entry shown.");
    Console.WriteLine("  -i  Display an entry with a specific index. Use -b1 to display indexes.");
    Console.WriteLine("      Display a range of events by specifying the start and end of the range.");
    Console.WriteLine("      For example, -i \"1127347-1127350\". Alternatively specify the index and a");
    Console.WriteLine("      count with -p, E.G. -p 5 -i 127347 displays the 5 events before and after");
    Console.WriteLine("      the index, with the index event in the middle assuming the other events");
    Console.WriteLine("      exist.");
    Console.WriteLine("  -l  Event log name to view or tail. Defaults to the Application log.");
    Console.WriteLine("      Use a relative or an absolute path to use a log file (*.evtx) instead.");
    Console.WriteLine("  -v  Verbose output. Outputs extra details (if present) for each record.");
    Console.WriteLine("  -c  Culture, defaults to \"En-US\". Options include \"De-DE\",\"Es-ES\",\"Fr-FR\"");
    Console.WriteLine("      \"It-IT\",\"Ja-JP\",\"Ko-KR\",\"Pl-PL\",\"Pt-BR\",\"Ru-RU\",\"Tr-TR\",\"Zh-CN\",\"Zh-TW\".");
    Console.WriteLine("  -s  Specify the log source name. Defaults to entries from ACE, IIB and WMB.");
    Console.WriteLine("      A partial name can also be used, so 'IBM' matches any entry containing");
    Console.WriteLine("      IBM. Use a '*' for all event log sources. Use a ',' to specify multiple");
    Console.WriteLine("      sources.");
    Console.WriteLine("      For example, \"Integration, MQ\" will match all IIB, and WMQ entries.");
    Console.WriteLine("  -d  Displays details of available event logs. Add -v for extra information.");
    Console.WriteLine("      Specify -d [-v] -l <log.evtx> for details on a log file.");
    Console.WriteLine("      Specify -d [-v] -l \"app, Hyper\" to filter on a subset of logs.");
    Console.WriteLine("  -fi Specify -fi <filt> to only show entries that contain <filt>.");
    Console.WriteLine("  -fx Specify -fx <filt> to only show entries that do not contain <filt>.");
    Console.WriteLine("  -fw Specify -fw to only show entries that are 'Warnings', 'Errors' or");
    Console.WriteLine("      'Critical Errors'.");
    Console.WriteLine("  -fe Specify -fe to only show entries that are 'Errors' or 'Critical Errors'.");
    Console.WriteLine("  -fc Specify -fc to only show entries that are 'Critical Errors'.");
    Console.WriteLine("  -fn Specify -fn <id_filter> to only show entries with the specified IDs.");
    Console.WriteLine("      The ID filter supports included, excluded and ranges of event IDs. For");
    Console.WriteLine("      details see: https://github.com/m-g-k/EventLogMonitor#filter-on-event-id");
    Console.WriteLine("  -nopatch Don't patch missing providers. See README.MD for details.");
    Console.WriteLine("  -utc Display the timestamp as UTC.");
    Console.WriteLine("  -version - displays the version of this tool.");
    Console.WriteLine("  -? or -help - displays this help.");
    Console.WriteLine("Notes:");
    Console.WriteLine("  When tailing the event log at the console:");
    Console.WriteLine("    Press <Enter>, 'Q' or <Esc> to exit.");
    Console.WriteLine("    Press 'S' for current statistics.");
  }

  private static void DisplayVersion()
  {
    Console.WriteLine("EventLogMonitor version {0}", GetProductVersion());
  }

  private static string GetProductVersion()
  {
    int majorVersion = typeof(EventLogMonitor).Assembly.GetName().Version.Major;
    int minorVersion = typeof(EventLogMonitor).Assembly.GetName().Version.Minor;
    return string.Format("{0}.{1}", majorVersion, minorVersion);
  }
  public void EventLogEventRead(object sender, EventArgs x1)
  {
    // Make sure there was no error reading the event and get the record from it
    EventRecord entry;
    if (x1 is EventRecordWrittenEventArgs x2)
    {
      if (x2.EventRecord is null)
      {
        Console.WriteLine("The event instance was null 1.");
        return;
      }
      if (x2.EventException != null)
      {
        Console.WriteLine("Exception getting event 1" + x2.EventException.ToString());
        return;
      }
      entry = x2.EventRecord;
    }
    else if (x1 is MyEventRecordWrittenEventArgs x3)
    {
      if (x3.EventRecord is null)
      {
        Console.WriteLine("The event instance was null 2.");
        return;
      }
      if (x3.EventException != null)
      {
        Console.WriteLine("Exception getting event 2" + x3.EventException.ToString());
        return;
      }
      entry = x3.EventRecord;
    }
    else
    {
      Console.WriteLine("Unknown EventType");
      return;
    }

    bool match = false;
    if (iMultiMatch.Length > 0 && iRecordIndexMin == 0)
    {
      foreach (string x in iMultiMatch)
      {
        if (entry.ProviderName.Contains(x))
        {
          match = true;
          break; // escape at the first match
        }
      }
    }
    else if (iSource == "*" || entry.ProviderName.Contains(iSource) || iRecordIndexMin > 0)
    {
      match = true;
    }

    if (match)
    {
      // display it!
      bool displayed = DisplayEventLogEntry(entry);
      if (displayed)
      {
        ++s_entriesDisplayed; // increment counts
      }
    }

  }

  private void OutputEntriesInEventLogOrder(List<EventRecord> entries)
  {
    // output matched entries in event log order
    if (entries.Count > 0)
    {
      entries.Reverse(); // reorder correctly
      foreach (EventRecord entry in entries)
      {
        bool displayed = DisplayEventLogEntry(entry);
        if (displayed)
        {
          ++s_entriesDisplayed; // increment counts
        }
      }
    }
  }

  public string EventSourceAsString()
  {
    string toLog = "";
    bool first = true;
    if (iMultiMatch.Length > 0)
    {
      foreach (string x in iMultiMatch)
      {
        if (first)
        {
          toLog = x;
          first = false;
        }
        else
        {
          toLog = toLog + "' or '" + x;
        }
      }
    }
    else
    {
      toLog = iSource;
    }
    return toLog;
  }

  public static bool LogIsAFile(string logName)
  {
    // if it looks like a file...
    return !string.IsNullOrEmpty(logName) && (logName.Contains(':') || logName.Contains('\\') || logName.Contains('.'));
  }

  private static void outputTimestampInUTC(EventRecord entry, bool first)
  {
    var entryAsXml = entry.ToXml();

    // quick hack to grab the timestamp out of the XML
    var timeSearch = "<TimeCreated SystemTime='";
    int startPos = entryAsXml.IndexOf(timeSearch);
    if (startPos > 0)
    {
      int endPos = entryAsXml.IndexOf("'/>", startPos);
      if (endPos > 0)
      {
        var begin = startPos + timeSearch.Length;
        var length = endPos - begin;
        var rawTimeStamp = entryAsXml.Substring(begin, length);
        var dt = XmlConvert.ToDateTime(rawTimeStamp, XmlDateTimeSerializationMode.Utc);
        if (first)
        {
          Console.Write(dt + "." + dt.Millisecond + ": ");
        }
        else
        {
          Console.Write(" [" + dt + "." + dt.Millisecond + "]\n");
        }
        return;
      }
    }

    // fall back if the xml does not find a match
    if (first)
    {
      Console.Write(entry.TimeCreated + "." + entry.TimeCreated.Value.Millisecond + ": ");
    }
    else
    {
      Console.Write(" [" + entry.TimeCreated + "." + entry.TimeCreated.Value.Millisecond + "]\n");
    }
  }

  private static bool LogNameMatch(string logName, string[] logsToMatch, bool matchAll)
  {
    if (matchAll)
    {
      return true;
    }

    foreach (string current in logsToMatch)
    {
      if (logName.Contains(current, StringComparison.CurrentCultureIgnoreCase))
      {
        return true;
      }
    }
    return false;
  }

  // class used to aid testing
  public abstract class MyEventRecordWrittenEventArgs : EventArgs
  {
    public EventRecord EventRecord;

    public Exception EventException;
  }

}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
public static class ReflectionExtensions
{
  public static T GetFieldValue<T>(this object o, string fieldName)
  {
    // Make sure both private and public fields will be found
    BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    FieldInfo field = o.GetType().GetField(fieldName, flags);
    return field != null ? (T)field?.GetValue(o) : default;
  }
}

