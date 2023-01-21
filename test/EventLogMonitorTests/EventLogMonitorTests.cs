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

using Xunit;
using Xunit.Abstractions;
using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using Moq;
using System.Diagnostics.Eventing.Reader;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Runtime.InteropServices;

namespace EventLogMonitor;

[Collection("EventLogMonitor")]
public class EventLogMonitorTests
{
  [SuppressMessage("Microsoft.Usage", "IDE0052:RemoveUnreadPrivateMember", MessageId = "stdoutput")]
  private readonly ITestOutputHelper stdoutput;
  private readonly string ace11SampleEventLog;
  private readonly string powerShellSampleEventLog;
  private readonly string vSSSampleEventLog;
  private readonly string restartManagerSampleEventLog;
  private readonly string securitySampleEventLog;
  private readonly string kernelPowerEventLogName;
  private readonly string invalidEventLogName;

  [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
  static extern System.UInt16 SetThreadLocale(System.UInt16 langId);

  public EventLogMonitorTests(ITestOutputHelper testOutputHelper)
  {
    stdoutput = testOutputHelper;
    ace11SampleEventLog = "../../../../../test/EventLogMonitorTests/SampleEventLogs/ACE-11-Log.evtx";
    powerShellSampleEventLog = "../../../../../test/EventLogMonitorTests/SampleEventLogs/POSH-Log.evtx";
    vSSSampleEventLog = "../../../../../test/EventLogMonitorTests/SampleEventLogs/VSS-Log.evtx";
    restartManagerSampleEventLog = "../../../../../test/EventLogMonitorTests/SampleEventLogs/RestartManager-Log.evtx";
    securitySampleEventLog = "../../../../../test/EventLogMonitorTests/SampleEventLogs/Security-Log.evtx";
    kernelPowerEventLogName = "../../../../../test/EventLogMonitorTests/SampleEventLogs/KernelPower-Log.evtx";
    invalidEventLogName = "../../../../../test/EventLogMonitorTests/SampleEventLogs/Invalid-Log.evtx";

    // Several tests produce output that includes the expected date and time in UK (En-GB - LCID 2057) format,
    // so we must force UK style output even when the machine running the tests is not in this locale.
    Thread.CurrentThread.CurrentCulture = new CultureInfo("En-GB", false);
  }

  [Fact]
  public void NoArgumentsPasedInIsAllowed()
  {
    string[] args = Array.Empty<string>();
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized);
  }

  [Theory]
  [InlineData("-?")]
  [InlineData("-help")]
  public void HelpIsReturned(string helpOption)
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { helpOption };
    EventLogMonitor monitor = new();
    monitor.Initialize(args);
    string help = output.ToString();
    Assert.StartsWith("EventLogMonitor : Version", help);
    Assert.EndsWith("Press 'S' for current statistics.", help.TrimEnd());
  }

  [Fact]
  public void VersionIsReturned()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-version" };
    EventLogMonitor monitor = new();
    monitor.Initialize(args);
    string version = output.ToString();
    Assert.StartsWith("EventLogMonitor version 2.", version);
  }

  [Fact]
  public void InvalidArgumentGivesAnErrorMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-imvalidArg" };
    EventLogMonitor monitor = new();
    monitor.Initialize(args);
    string help = output.ToString();
    Assert.StartsWith("Invalid arguments or invalid argument combination.", help);
  }

  [Fact]
  public void InvalidDuplicateArgumentWithValueGivesAnErrorMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-imvalidArg", "-invalidArg" };
    EventLogMonitor monitor = new();
    monitor.Initialize(args);
    string help = output.ToString();
    Assert.StartsWith("Invalid arguments or invalid argument combination.", help);
  }

  [Theory]
  [InlineData("")]
  [InlineData("-1")] // -1 is the default output type if not present or overridden with -2 or -3
  public void OptionP2ReturnsTwoMostRecentPreviousEvents(string extraOptions)
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, extraOptions };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[0]);
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[1]);
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithMediumOutput()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-2" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
                                   // entry one
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully.", lines[0].TrimEnd());
    Assert.Equal("The integration node received an operational control message containing an instruction to start the deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') and successfully performed this action. [23/12/2021 11:58:12.195]", lines[1]);
    // entry 2
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message.", lines[2].TrimEnd());
    Assert.Equal("A command response will be sent to the integration node. [23/12/2021 11:58:12.195]", lines[3]);
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[4]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithFullOutput()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-3" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(7, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
                                   // entry one
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully.", lines[0].TrimEnd());
    Assert.Equal("The integration node received an operational control message containing an instruction to start the deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') and successfully performed this action.", lines[1].TrimEnd());
    Assert.Equal("No user action required. [23/12/2021 11:58:12.195]", lines[2]);
    // entry 2
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message.", lines[3].TrimEnd());
    Assert.Equal("A command response will be sent to the integration node.", lines[4].TrimEnd());
    Assert.Equal("No user action required. [23/12/2021 11:58:12.195]", lines[5]);
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[6]);
  }

  [Fact]
  public void UsingMoreThanOneOutputTypeReturnsAnError()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-3", "-2" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: only one of options '-1', '-2' and '-3' may be specified.", logOut);
  }


  [Fact]
  public void OptionP2WithTFReturnsTwoMostRecentPreviousEventsWithTimestampFirst()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-tf" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    Assert.Equal("23/12/2021 11:58:12.195: BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully.", lines[0]);
    Assert.Equal("23/12/2021 11:58:12.195: BIP2154I: ( MGK.main ) Integration server finished with Configuration message.", lines[1]);
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithBinaryDataAsUnicode()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-b1" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
                                   // entry 1
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[0]);
    Assert.Equal("8464 MGK.main C:\\ci\\product-build\\WMB\\src\\DataFlowEngine\\MessageServices\\ImbResource.cpp 3566 ImbResource::logDeployStatusComp. Index: 282277", lines[1]);
    // entry 2
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[2]);
    Assert.Equal("8464 MGK.main C:\\ci\\product-build\\WMB\\src\\DataFlowEngine\\MessageServices\\ImbDataFlowNotifications.cpp 525 ImbDataFlowNotifications::output. Index: 282278", lines[3]);
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[4]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithBinaryDataAsHex()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-b2" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(76, lines.Length); // one extra closing test line is returned
                                    // most recent 2 entries
                                    // event 1
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[0]);
    Assert.Equal("Binary Data size: 256", lines[1]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[2]);
    Assert.Equal("00000008: 38 00 34 00-36 00 34 00 8.4.6.4. 00340038 00340036", lines[3]);
    Assert.Equal("00000256: 6D 00 70 00-00 00 00 00 m.p..... 0070006D 00000000", lines[34]); //skip some lines before this one
    Assert.StartsWith("Index: 282277", lines[35]);
    // event 2
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[36]);
    Assert.Equal("Binary Data size: 280", lines[37]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[38]);
    Assert.Equal("00000008: 38 00 34 00-36 00 34 00 8.4.6.4. 00340038 00340036", lines[39]);
    Assert.Equal("00000280: 75 00 74 00-00 00 00 00 u.t..... 00740075 00000000", lines[73]); //skip some lines before this one
    Assert.StartsWith("Index: 282278", lines[74]);
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[75]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithExtraVerboseOutput()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-v" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
                                   // event 1
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[0]);
    Assert.StartsWith("Machine: mgk-PC3. Log:", lines[1]);
    Assert.EndsWith("Source: IBM App Connect Enterprise v110011.", lines[1].TrimEnd());
    // event 2
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[2]);
    Assert.StartsWith("Machine: mgk-PC3. Log:", lines[1]);
    Assert.EndsWith("Source: IBM App Connect Enterprise v110011.", lines[1].TrimEnd());
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[4]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsInGerman()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-c", "de" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    Assert.Equal("BIP2269I: ( MGK.main ) Die implementierte Ressource ''test'' (UUID=''test'', Typ=''MessageFlow'') wurde erfolgreich gestartet. [23/12/2021 11:58:12.195]", lines[0]);
    Assert.Equal("BIP2154I: ( MGK.main ) Integrationsserver hat die Verarbeitung der Konfigurationsnachricht abgeschlossen. [23/12/2021 11:58:12.195]", lines[1]);
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Fact]
  public void OptionInvalidCultureReturnsError()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-l", ace11SampleEventLog, "-c", "fake" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    Assert.Equal("Culture is not supported. 'fake' is an invalid culture identifier. Defaulting to 'En-US'.", lines[0].TrimEnd());
    Assert.StartsWith("0 Entries shown from the", lines[1]);
  }

  [Fact]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsInEnglishWithInvalidCulture()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLog, "-c", "ABC" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(4, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries  
    Assert.Equal("Culture is not supported. 'ABC' is an invalid culture identifier. Defaulting to 'En-US'.", lines[0].TrimEnd());
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[1]);
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[2]);
    Assert.StartsWith("2 Entries shown from the", lines[3]);
  }

  [Fact]
  public void OptionPStarReturnsAll64PreviousEvents()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    // stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(65, lines.Length); // one extra closing test line is returned
                                    // oldest 2 entries
    Assert.Equal("BIP2001I: ( MGK ) The IBM App Connect Enterprise service has started at version '110011'; process ID 7192. [18/11/2021 18:21:16.344]", lines[0]);
    Assert.Equal("BIP3132I: ( MGK ) The HTTP Listener has started listening on port ''4414'' for ''RestAdmin http'' connections. [18/11/2021 18:21:26.571]", lines[1]);
    // most recent 2 entries
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[62]);
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[63]);
    Assert.StartsWith("64 Entries shown from the", lines[64]);
  }

  [Fact]
  public void OptionIndexReturnsSingleEvent()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282229", "-b1", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length); // one event with one binary line and one extra closing test line is returned
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7108'. [23/12/2021 11:57:04.661]", lines[0].TrimEnd());
    Assert.Equal(@"5812 MGK.service C:\ci\product-build\WMB\src\AdminAgent\BipService\Win32\BipServiceMain.cpp 962 ImbControlService::serviceHandle. Index: 282229", lines[1]);
    // tail
    Assert.StartsWith("Matching entry found in the", lines[2]);
  }

  [Fact]
  public void OptionIndexRangeReturnsThreeEventsInclusive()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282229-282259", "-b1", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(7, lines.Length); // one event with one binary line and one extra closing test line is returned
    //event 1
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7108'. [23/12/2021 11:57:04.661]", lines[0].TrimEnd());
    Assert.Equal(@"5812 MGK.service C:\ci\product-build\WMB\src\AdminAgent\BipService\Win32\BipServiceMain.cpp 962 ImbControlService::serviceHandle. Index: 282229", lines[1]);
    //event 2
    Assert.Equal("BIP2001I: ( MGK ) The IBM App Connect Enterprise service has started at version '110011'; process ID 7100. [23/12/2021 11:57:51.90]", lines[2].TrimEnd());
    Assert.Equal(@"6784 MGK.service C:\ci\product-build\WMB\src\AdminAgent\BipService\Win32\BipServiceMain.cpp 641 ImbControlService::serviceMain. Index: 282237", lines[3]);
    //event 3
    Assert.Equal("BIP3132I: ( MGK ) The HTTP Listener has started listening on port ''4414'' for ''RestAdmin http'' connections. [23/12/2021 11:58:00.33]", lines[4].TrimEnd());
    Assert.Equal(@"8816 MGK.agent C:\ci\product-build\WMB\src\bipBroker\NodejsLoggingHooks.cpp 113 <JS> ace-admin-server server.js.. Index: 282259", lines[5]);

    // tail
    Assert.StartsWith("Found 3 Matching entries found in the", lines[6]);
  }

  [Fact]
  public void OptionIndexWithOptionP3WithSparseIndexReturnsThreeEventsAfterIndex()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "240568", "-p", "3", "-b1", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(7, lines.Length); // one event with one binary line and one extra closing test line is returned
    //event 1
    Assert.Equal("BIP3132I: ( MGK ) The HTTP Listener has started listening on port ''4414'' for ''RestAdmin http'' connections. [18/11/2021 18:21:26.571]", lines[0].TrimEnd());
    Assert.Equal(@"9336 MGK.agent C:\ci\product-build\WMB\src\bipBroker\NodejsLoggingHooks.cpp 113 <JS> ace-admin-server server.js.. Index: 240568", lines[1]);
    //event 2
    Assert.Equal("BIP2866I: ( MGK ) IBM App Connect Enterprise administration security is 'inactive'. [18/11/2021 18:21:26.571]", lines[2].TrimEnd());
    Assert.Equal(@"9068 MGK.agent C:\ci\product-build\WMB\src\AdminSec\AdminSecAuthManager.cpp 105 AdminSecAuthManager::init. Index: 240569", lines[3]);
    //event 3
    Assert.Equal(@"BIP2208I: ( MGK.main ) Integration server (64) started: process '9980'; thread '9976'; additional information: integrationNodeName ''MGK'' (operation mode ''advanced''); integrationServerUUID ''00000000-0000-0000-0000-000000000000''; integrationServerLabel ''main''; queueManagerName ''''; trusted 'false'; userId ''SYSTEM''; migrationNeeded 'false'; integrationNodeUUID ''63e1e703-5274-4930-afe2-bccc27b87214''; filePath ''D:\ACE\11.0.0.11\server''; workPath ''C:\ProgramData\IBM\MQSI''; ICU Converter Path ''''; ordinality '1'. [18/11/2021 18:21:27.569]", lines[4].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\Main\ImbMain.cpp 1713 ImbMain::start. Index: 240570", lines[5]);
    // tail
    Assert.StartsWith("Found 3 Matching entries found in the", lines[6]);
  }

  [Fact]
  public void OptionIndexWithSmallIndexAndLargerPValueReturnsCorrectIndexInError()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "5", "-p", "6", "-b1", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines); // one event with one binary line and one extra closing test line is returned
    // tail
    Assert.StartsWith("No entries found with matching index 5 in the", lines[0]);
  }

  [Fact]
  public void OptionIndexWithOptionP3ReturnsThreeEventsEitherSideOfTheIndex()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "240584", "-p", "3", "-b1", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(15, lines.Length); // one event with one binary line and one extra closing test line is returned
    //event 1
    Assert.Equal("BIP2153I: ( MGK.main ) About to ''Start'' an integration server. [18/11/2021 18:21:38.345]", lines[0].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbDataFlowNotifications.cpp 508 ImbDataFlowNotifications::output. Index: 240581", lines[1]);
    //event 2
    Assert.Equal("BIP2155I: ( MGK.main ) About to ''Initialize'' the deployed resource ''testApp'' of type ''Application''. [18/11/2021 18:21:38.370]", lines[2].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbResource.cpp 3459 ImbResource::logDeployStatus. Index: 240582", lines[3]);
    //event 3
    Assert.Equal(@"BIP2155I: ( MGK.main ) About to ''Start'' the deployed resource ''testApp'' of type ''Application''. [18/11/2021 18:21:38.689]", lines[4].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbResource.cpp 3459 ImbResource::logDeployStatus. Index: 240583", lines[5]);
    //event 4
    Assert.Equal(@"BIP3132I: ( MGK.main ) The HTTP Listener has started listening on port '7800' for ''http'' connections. [18/11/2021 18:21:38.801]", lines[6].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\WebServices\EventHTTP\RequestQueue.cpp 437 Imb::EvHtp::RequestQueue::regist. Index: 240584", lines[7]);
    //event 5
    Assert.Equal(@"BIP1996I: ( MGK.main ) Listening on HTTP URL ''/test''. [18/11/2021 18:21:38.802]", lines[8].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\WebServices\WSLibrary\ImbWSInputNode.cpp 599 Imb::WSInputNode::onStart. Index: 240585", lines[9]);
    //event 6
    Assert.Equal(@"BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [18/11/2021 18:21:38.805]", lines[10].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbResource.cpp 3566 ImbResource::logDeployStatusComp. Index: 240586", lines[11]);
    //event 7
    Assert.Equal(@"BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [18/11/2021 18:21:38.806]", lines[12].TrimEnd());
    Assert.Equal(@"9976 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbDataFlowNotifications.cpp 525 ImbDataFlowNotifications::output. Index: 240587", lines[13]);
    // tail
    Assert.StartsWith("Found 7 Matching entries found in the", lines[14]);
  }

  [Fact]
  public void OptionIndexWithOptionPStarReturnsLast8Events()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282271", "-p", "*", "-b1", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(17, lines.Length);
    //event 1
    Assert.Equal("BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]", lines[0].TrimEnd());
    Assert.Equal(@"8464 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbDataFlowNotifications.cpp 504 ImbDataFlowNotifications::output. Index: 282271", lines[1]);
    //event 2
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[14].TrimEnd());
    Assert.Equal(@"8464 MGK.main C:\ci\product-build\WMB\src\DataFlowEngine\MessageServices\ImbDataFlowNotifications.cpp 525 ImbDataFlowNotifications::output. Index: 282278", lines[15]);
    // tail
    Assert.StartsWith("Found 8 Matching entries found in the", lines[16]);
  }

  [Fact]
  public void InvalidIndexRangeReturnsError()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282-229-345", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: invalid range, use 'x-y' to specify a range.", logOut);
  }

  [Fact]
  public void UsingAnIndexWithOptionSIsAnError()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "229-282", "-s", "newSource", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: -s not allowed with -i.", logOut);
  }

  [Fact]
  public void InvalidIndexRangeEndLessThanStartReturnsError()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282271-282270", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: index max > index min.", logOut);
  }

  [Fact]
  public void IndexHigherThanHigestEntryInLogIsOKReturnsNoEvents()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282280", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines);
    Assert.StartsWith("No entries found with matching index 282280 in the", lines[0]);
  }

  [Fact]
  public void OptionFilterIncludeReturns5EventsInEnglish()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "Listening", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(6, lines.Length);
    //event 1
    Assert.Equal("BIP1996I: ( MGK.main ) Listening on HTTP URL ''/test''. [18/11/2021 18:21:38.802]", lines[0].TrimEnd());
    //event 5
    Assert.Equal("BIP1996I: ( MGK.main ) Listening on HTTP URL ''/test''. [23/12/2021 11:58:12.194]", lines[4].TrimEnd());
    // tail
    Assert.StartsWith("5 Entries shown from the", lines[5]);
  }

  [Fact]
  public void OptionFilterIncludeReturns5EventsInGerman()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "empfangsbereit", "-c", "De-DE", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(6, lines.Length);
    //event 1
    Assert.Equal("BIP1996I: ( MGK.main ) Unter HTTP-URL ''/test'' empfangsbereit. [18/11/2021 18:21:38.802]", lines[0].TrimEnd());
    //event 5
    Assert.Equal("BIP1996I: ( MGK.main ) Unter HTTP-URL ''/test'' empfangsbereit. [23/12/2021 11:58:12.194]", lines[4].TrimEnd());
    // tail
    Assert.StartsWith("5 Entries shown from the", lines[5]);
  }

  [Fact]
  public void OptionFilterIncludeReturns4Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "shutdown", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length);
    //event 1
    Assert.Equal("BIP3988I: ( MGK ) The integration node ''MGK'' is performing an immediate shutdown. [24/11/2021 18:52:43.396]", lines[0].TrimEnd());
    //event 4
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7108'. [23/12/2021 11:57:04.661]", lines[3].TrimEnd());
    // tail
    Assert.StartsWith("4 Entries shown from the", lines[4]);
  }

  [Fact]
  public void OptionFilterIncludeAndExcludeReturns2Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "shutdown", "-fx", "immediate", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length);
    //event 1
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7028'. [30/11/2021 22:49:47.66]", lines[0].TrimEnd());
    //event 2
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7108'. [23/12/2021 11:57:04.661]", lines[1].TrimEnd());
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Fact]
  public void OptionFilterIncludeAndWarnReturns2Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "shutdown", "-fw", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length);
    //event 1
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7028'. [30/11/2021 22:49:47.66]", lines[0].TrimEnd());
    //event 2
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7108'. [23/12/2021 11:57:04.661]", lines[1].TrimEnd());
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Fact]
  public void OptionFilterErrorOnlyReturns0Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fe", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines);
    // tail
    Assert.StartsWith("0 Entries shown from the", lines[0]);
  }

  [Fact]
  public void OptionFilterCriticalReturns5Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // force a culture specific search
    string[] args = new string[] { "-p", "*", "-fc", "-c", "En-US", "-l", kernelPowerEventLogName };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(11, lines.Length);
    //event 1
    Assert.Equal("41C: The system has rebooted without cleanly shutting down first. This error could be caused if the system stopped responding, crashed, or lost power unexpectedly. [24/03/2022 12:35:05.0]", lines[0].TrimEnd());
    //event 2
    Assert.Equal("<Entry has no binary data>. Index: 69554", lines[1].TrimEnd());
    // tail
    Assert.StartsWith("5 Entries shown from the", lines[10]);
  }

  [Fact]
  public void OptionFilterSingleIncludedIDsReturns2Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLog}", "-fn=2011" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length);
    //event 1
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7028'. [30/11/2021 22:49:47.66]", lines[0].TrimEnd());
    //event 2
    Assert.Equal("BIP2011W: ( MGK ) The IBM App Connect Enterprise service has been shutdown, process ID '7108'. [23/12/2021 11:57:04.661]", lines[1].TrimEnd());
    // tail
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Fact]
  public void OptionFilterSingleExcludedIDsReturns54Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLog}", "-fn=-2155" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(55, lines.Length);
    // output should not contain the excluded IDs
    Assert.DoesNotContain("BIP2155", logOut);
    //event 1
    Assert.Equal("BIP2001I: ( MGK ) The IBM App Connect Enterprise service has started at version '110011'; process ID 7192. [18/11/2021 18:21:16.344]", lines[0].TrimEnd());
    //event 54
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[53].TrimEnd());
    // tail
    Assert.StartsWith("54 Entries shown from the", lines[54]);
  }

  [Fact]
  public void OptionFilterIncludedRangeReturns25Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLog}", "-fn=2150-2160" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(26, lines.Length);
    //event 1
    Assert.Equal("BIP2152I: ( MGK.main ) Configuration message received. [18/11/2021 18:21:38.345]", lines[0].TrimEnd());
    //event 25
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[24].TrimEnd());
    // tail
    Assert.StartsWith("25 Entries shown from the", lines[25]);
  }

  [Fact]
  public void OptionFilterExcludedRangeReturns39Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLog}", "-fn=-2150-2160" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(40, lines.Length);
    // output should not contain the excluded IDs
    Assert.DoesNotContain("BIP2150", logOut);
    Assert.DoesNotContain("BIP2151", logOut);
    Assert.DoesNotContain("BIP2152", logOut);
    Assert.DoesNotContain("BIP2153", logOut);
    Assert.DoesNotContain("BIP2154", logOut);
    Assert.DoesNotContain("BIP2155", logOut);
    Assert.DoesNotContain("BIP2156", logOut);
    Assert.DoesNotContain("BIP2157", logOut);
    Assert.DoesNotContain("BIP2158", logOut);
    Assert.DoesNotContain("BIP2159", logOut);
    Assert.DoesNotContain("BIP2160", logOut);
    //event 1
    Assert.Equal("BIP2001I: ( MGK ) The IBM App Connect Enterprise service has started at version '110011'; process ID 7192. [18/11/2021 18:21:16.344]", lines[0].TrimEnd());
    //event 25
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[38].TrimEnd());
    // tail
    Assert.StartsWith("39 Entries shown from the", lines[39]);
  }

  [Fact]
  public void OptionFilterMixedEventIdsReturns22Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLog}", "-fn=2150-2160,-2154-2155,2011,2001,-2152,3130-3140" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(23, lines.Length);
    // output should not contain the excluded IDs
    Assert.DoesNotContain("BIP2152", logOut);
    Assert.DoesNotContain("BIP2154", logOut);
    Assert.DoesNotContain("BIP2155", logOut);
    //event 1
    Assert.Equal("BIP2001I: ( MGK ) The IBM App Connect Enterprise service has started at version '110011'; process ID 7192. [18/11/2021 18:21:16.344]", lines[0].TrimEnd());
    //event 25
    Assert.Equal("BIP3132I: ( MGK.main ) The HTTP Listener has started listening on port '7800' for ''http'' connections. [23/12/2021 11:58:12.194]", lines[21].TrimEnd());
    // tail
    Assert.StartsWith("22 Entries shown from the", lines[22]);
  }

  [Fact]
  public void OptionFilterIncludedRangeIDsReturns4Entries()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the '=' form for the fn arguments as well as a trailing bool argument to ensure '=' form does not have to be last
    string[] args = new string[] { "-p=*", $"-l={securitySampleEventLog}", "-fn=0-10000", "-v" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(9, lines.Length);
    //event 1
    Assert.Equal("4957F: Windows Firewall did not apply the following rule: [05/02/2022 13:54:04.551]", lines[0].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 17632.", lines[1].TrimEnd());
    //event 2
    Assert.Equal("4662S: An operation was performed on an object. [05/02/2022 14:40:21.712]", lines[2].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 12488.", lines[3].TrimEnd());
    //event 3
    Assert.Equal("4662S: An operation was performed on an object. [05/02/2022 14:40:21.720]", lines[4].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 12488.", lines[5].TrimEnd());
    //event 4
    Assert.Equal("4957F: Windows Firewall did not apply the following rule: [06/02/2022 15:57:02.961]", lines[6].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 17632.", lines[7].TrimEnd());
    // tail
    Assert.StartsWith("4 Entries shown from the", lines[8]);
  }

  [Fact]
  public void OptionFilterIncludedRangeIDsReturns4EntriesInAltCulture()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the '=' form for the fn arguments as well as a trailing bool argument to ensure '=' form does not have to be last
    string[] args = new string[] { "-p=*", "-c=En-US", $"-l={securitySampleEventLog}", "-fn=0-10000", "-v" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(9, lines.Length);
    //event 1
    Assert.Equal("4957F: Windows Firewall did not apply the following rule: [05/02/2022 13:54:04.551]", lines[0].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 17632.", lines[1].TrimEnd());
    //event 2
    Assert.Equal("4662S: An operation was performed on an object. [05/02/2022 14:40:21.712]", lines[2].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 12488.", lines[3].TrimEnd());
    //event 3
    Assert.Equal("4662S: An operation was performed on an object. [05/02/2022 14:40:21.720]", lines[4].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 12488.", lines[5].TrimEnd());
    //event 4
    Assert.Equal("4957F: Windows Firewall did not apply the following rule: [06/02/2022 15:57:02.961]", lines[6].TrimEnd());
    Assert.EndsWith("Source: Microsoft-Windows-Security-Auditing. ProcessId: 1052. ThreadId: 17632.", lines[7].TrimEnd());
    // tail
    Assert.StartsWith("4 Entries shown from the", lines[8]);
  }

  [Fact]
  public void InvalidIDFilterGivesAnErrorMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-fn", "imvalidFilter" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    string help = output.ToString();
    Assert.StartsWith("Invalid arguments or invalid argument combination: Invalid -fn filter: Invalid Event ID filter: 'imvalidFilter'.", help);
  }

  [Fact]
  public void InvalidIDInclusiveRangeFilterGivesAnErrorMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-fn", "42-bad" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    string help = output.ToString();
    Assert.StartsWith("Invalid arguments or invalid argument combination: Invalid -fn filter: Invalid inclusive range filter: '42-bad'.", help);
  }

  [Fact]
  public void InvalidIDExclusiveRangeFilterGivesAnErrorMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-fn=-43-56bad" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    string help = output.ToString();
    Assert.StartsWith("Invalid arguments or invalid argument combination: Invalid -fn filter: Invalid exclusive range filter: '-43-56bad'.", help);
  }

  [Fact]
  public void OptionDShowsDetailsOfEventLogFile()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(4, lines.Length);
    Assert.Equal("LogName : Entries : LastWriteTime : [DisplayName]", lines[0]);
    Assert.Equal("-------------------------------------------------", lines[1]);
    Assert.Contains("ACE-11-Log.evtx : 64 : ", lines[2]);
    Assert.EndsWith(" : [ACE-11-Log]", lines[2]);
    Assert.Equal("1 Provider file listed.", lines[3]);
  }

  [Fact]
  public void OptionDShowsDetailsOfEventLogFileVerbose()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-v", "-l", ace11SampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(9, lines.Length);
    Assert.EndsWith("ACE-11-Log.evtx", lines[0]);
    Assert.Equal("    DisplayName:  ACE-11-Log", lines[1]);
    Assert.Equal("    Records:      64", lines[2]);
    Assert.Equal("    FileSize:     69632", lines[3]);
    Assert.Equal("    IsFull:       False", lines[4]);
    Assert.StartsWith("    CreationTime:", lines[5]); // this varies if the file is moved
    Assert.StartsWith("    LastWrite:", lines[6]);
    Assert.Equal("    OldestIndex:  1", lines[7]);
    Assert.Equal("1 Provider file listed.", lines[8]);
  }

  [Fact]
  public void OptionDAloneShowsDetailsOfEventLogs()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.InRange(lines.Length, 10, 1000000); // should be at least 10 returned
    Assert.Equal("LogName : Entries : LastWriteTime : [DisplayName]", lines[0]);
    Assert.Equal("-------------------------------------------------", lines[1]);
    Assert.EndsWith("Providers listed.", lines[^1]);
  }

  [Fact]
  public void OptionDWithMultiOptionLReturnsMultipleEventLogs()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-l", "Application, Windows PowerShell" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.InRange(lines.Length, 5, 1000000); // should be at least 5 lines (3 boiler plate, 2 logs) returned
    Assert.Equal("LogName : Entries : LastWriteTime : [DisplayName]", lines[0]);
    Assert.Equal("-------------------------------------------------", lines[1]);
    Assert.EndsWith("Providers listed.", lines[^1]);
    // we cannot guarantee the line order returned so just do a scan check to make sure we got something
    Assert.Contains("Application", logOut);
    Assert.Contains("Windows PowerShell", logOut);
  }

  [Fact]
  public void OptionDWithSingleOptionLReturnsSingleEventLog()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-l", "Windows PowerShell" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.InRange(lines.Length, 4, 1000000); // should be at least 5 lines (3 boiler plate, 2 logs) returned
    Assert.Equal("LogName : Entries : LastWriteTime : [DisplayName]", lines[0]);
    Assert.Equal("-------------------------------------------------", lines[1]);
    Assert.EndsWith("Providers listed.", lines[^1]);
    // we cannot guarantee a single line returned so just do a scan check to make sure we got something
    Assert.Contains("Windows PowerShell", logOut);
  }

  [Fact]
  public void OptionDShowsDetailsOfRealEventLogVerbose()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-v", "-l", "Windows Powershell" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    // we can only really check the shape without making this test too brittle
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(15, lines.Length);
    Assert.EndsWith("Windows PowerShell", lines[0]);
    Assert.Equal("    DisplayName:  Windows PowerShell", lines[1]);
    Assert.StartsWith("    Records:", lines[2]);
    Assert.StartsWith("    FileSize:", lines[3]);
    Assert.StartsWith("    IsFull:", lines[4]);
    Assert.StartsWith("    CreationTime:", lines[5]);
    Assert.StartsWith("    LastWrite:", lines[6]);
    Assert.StartsWith("    OldestIndex:", lines[7]);
    Assert.StartsWith("    IsClassic:", lines[8]);
    Assert.StartsWith("    IsEnabled:", lines[9]);
    Assert.StartsWith("    LogFile:", lines[10]);
    Assert.StartsWith("    LogType:", lines[11]);
    Assert.StartsWith("    MaxSizeBytes:", lines[12]);
    Assert.StartsWith("    MaxBuffers:", lines[13]);
    Assert.Equal("1 Providers listed.", lines[14]);
  }

  [Fact]
  public void EventLogReaderReturnsEvent()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    var mockEventRecord = new Mock<EventRecord>();
    mockEventRecord.Setup(x => x.Id).Returns(42);
    mockEventRecord.Setup(x => x.LogName).Returns("Applicationz");
    var mockQuery = new Mock<EventLogQuery>("Application", PathType.LogName, null);
    var mockReader = new Mock<TestEventLogReader>(mockQuery.Object);

    mockReader.Setup(x => x.ReadEvent()).Returns(mockEventRecord.Object);

    var testEvent = mockReader.Object.ReadEvent();
    Assert.Equal(42, testEvent.Id);
    Assert.Equal("Applicationz", testEvent.LogName);

  }
  [Fact]
  public void NoneExistentMessageIDReturnsErrorMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // mock an event to raise
    var mockEventRecord = new Mock<EventRecord>();
    mockEventRecord.Setup(x => x.Id).Returns(42);
    mockEventRecord.Setup(x => x.ProviderName).Returns("WebSphere Broker");
    mockEventRecord.Setup(x => x.Properties).Returns(new List<EventProperty>());
    mockEventRecord.Setup(x => x.TimeCreated).Returns(new DateTime(2000, 1, 1, 12, 0, 0));
    var mockQuery = new Mock<EventLogQuery>("Application", PathType.LogName, null);
    var mockWatch = new Mock<TestEventLogWatcher>(mockQuery.Object);

    string[] monitorArgs = new string[] { "-nt" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(monitorArgs);
    Assert.True(initialized, $"{initialized} should be true");
    var args = new Mock<EventLogMonitor.MyEventRecordWrittenEventArgs>();
    args.Object.EventRecord = mockEventRecord.Object;
    mockWatch.Object.EventRecordWritten += new EventHandler<EventLogMonitor.MyEventRecordWrittenEventArgs>(monitor.EventLogEventRead);
    mockWatch.Object.Enabled = true;
    mockWatch.Raise(e => e.EventRecordWritten += null, this, args.Object);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines);
    Assert.Equal("BIP42I: The description for Event ID 42 from source WebSphere Broker cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [01/01/2000 12:00:00.0]", lines[0]);
  }

  [Fact]
  public void OptionMultiSourceReturnsNoEvents()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-s", "A,B,C", "-l", "System", "-nt" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines);
    Assert.Equal("0 Entries shown from the System log matching the event source 'A' or 'B' or 'C'.", lines[0]);
  }

  [Fact]
  public void OptionSingleSourceReturnsNoEvents()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-s", "Z", "-l", "System", "-nt" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines);
    Assert.Equal("0 Entries shown from the System log matching the event source 'Z'.", lines[0]);
  }

  [Fact]
  public void OptionMultiSourceReturnsEvents()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);
    string[] args = new string[] { "-s", "Restart, Test, Power", "-l", powerShellSampleEventLog, "-p", "*" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);

    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(6, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("600I: Provider \"Variable\" is Started. [11/01/2022 18:37:18.339]", lines[0].TrimEnd());
    Assert.Equal("400I: Engine state is changed from None to Available. [11/01/2022 18:37:18.977]", lines[1]);
    Assert.Equal("800I: Pipeline execution details for command line: Add-Type -TypeDefinition $Source -Language CSharp -IgnoreWarnings. [11/01/2022 18:37:22.771]", lines[2].TrimEnd());
    Assert.Equal("600I: Provider \"Certificate\" is Started. [11/01/2022 18:37:25.811]", lines[3]);
    Assert.Equal("403I: Engine state is changed from Available to Stopped. [11/01/2022 18:37:26.805]", lines[4]);
    // tail
    Assert.StartsWith("5 Entries shown from the", lines[5]);
    Assert.EndsWith("POSH-Log.evtx log matching the event source 'Restart' or 'Test' or 'Power'.", lines[5]);
  }

  [Fact]
  public void CliOptionsReturnsZeroEntriesTailing()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // replace stdin to fake it
    string esc = ((char)0x1b).ToString(); // Dec 27
    string inputData = "sssx" + esc; // 3 's' for stats, one 'x' to make sure it is ignored and an 'ESC' to quit
    var input = new StringReader(inputData);
    Console.SetIn(input);
    Environment.SetEnvironmentVariable("EVENTLOGMONITOR_INPUT_REDIRECTED", "true");

    string[] args = new string[] { "-s", "FakeSource", "-l", "System" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length);
    Assert.Equal("Waiting for events from the System log matching the event source 'FakeSource'.", lines[0]);
    Assert.Equal("0 Entries shown so far from the System log. Waiting for more events...", lines[1]); // from the "s" option passed in
    Assert.Equal("0 Entries shown so far from the System log. Waiting for more events...", lines[2]); // from the "s" option passed in
    Assert.Equal("0 Entries shown so far from the System log. Waiting for more events...", lines[3]); // from the "s" option passed in
    Assert.Equal("0 Entries shown from the System log matching the event source 'FakeSource'.", lines[4]);
    Console.In.Close();
  }

  [Fact]
  public void CliOptionEnterWillQuitTailing()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // replace stdin to fake it
    string inputData = ((char)0x0D).ToString(); //Dec 13
    var input = new StringReader(inputData);
    Console.SetIn(input);
    Environment.SetEnvironmentVariable("EVENTLOGMONITOR_INPUT_REDIRECTED", "true");

    string[] args = new string[] { "-s", "FakeSource", "-l", "System" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length);
    Assert.Equal("Waiting for events from the System log matching the event source 'FakeSource'.", lines[0]);
    Assert.Equal("0 Entries shown from the System log matching the event source 'FakeSource'.", lines[1]);
    Console.In.Close();
  }

  [Fact]
  public void CliOptionQWillQuitTailing()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // replace stdin to fake it
    string inputData = "q";
    var input = new StringReader(inputData);
    Console.SetIn(input);
    Environment.SetEnvironmentVariable("EVENTLOGMONITOR_INPUT_REDIRECTED", "true");

    string[] args = new string[] { "-s", "FakeSource", "-l", "System" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length);
    Assert.Equal("Waiting for events from the System log matching the event source 'FakeSource'.", lines[0]);
    Assert.Equal("0 Entries shown from the System log matching the event source 'FakeSource'.", lines[1]);
    Console.In.Close();
  }

  [Fact]
  public void CliOptionEOSWillQuitTailing()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // replace stdin to fake it
    string inputData = ""; // this will give an EOS when it is read
    var input = new StringReader(inputData);
    Console.SetIn(input);
    Environment.SetEnvironmentVariable("EVENTLOGMONITOR_INPUT_REDIRECTED", "true");

    string[] args = new string[] { "-s", "FakeSource", "-l", "System" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length);
    Assert.Equal("Waiting for events from the System log matching the event source 'FakeSource'.", lines[0]);
    Assert.Equal("0 Entries shown from the System log matching the event source 'FakeSource'.", lines[1]);
    Console.In.Close();
  }

  [Fact]
  public void OptionInvalidIndexReturnsNoEvents()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "4294967295" }; // choose an index unlikely to be ever used (uint max)
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");

    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Single(lines);
    Assert.Equal("No entries found with matching index 4294967295 in the Application log", lines[0]);
    Console.In.Close();
  }

  [Fact]
  public void PowerShellLogReturnsSingleLinesOutput()
  {
    // The "Windows PowerShell" log has an embedded '\r\n' in the event 800 which should be stripped out

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", powerShellSampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(6, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("600I: Provider \"Variable\" is Started. [11/01/2022 18:37:18.339]", lines[0].TrimEnd());
    Assert.Equal("400I: Engine state is changed from None to Available. [11/01/2022 18:37:18.977]", lines[1]);
    Assert.Equal("800I: Pipeline execution details for command line: Add-Type -TypeDefinition $Source -Language CSharp -IgnoreWarnings. [11/01/2022 18:37:22.771]", lines[2].TrimEnd());
    Assert.Equal("600I: Provider \"Certificate\" is Started. [11/01/2022 18:37:25.811]", lines[3]);
    Assert.Equal("403I: Engine state is changed from Available to Stopped. [11/01/2022 18:37:26.805]", lines[4]);
    // tail
    Assert.StartsWith("5 Entries shown from the", lines[5]);
  }

  [Fact]
  public void VSSLogReturnsASCIIOutputOutput()
  {
    // The "VSS" log has an 2 error messages with ASCII binary data in it which should be automatically be shown

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", vSSSampleEventLog, "-s", "*" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(7, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("8224I: The VSS service is shutting down due to idle timeout. [21/12/2021 21:49:36.851]", lines[0].TrimEnd());
    Assert.Equal("8193E: Volume Shadow Copy Service error: Unexpected error calling routine QueryFullProcessImageNameW.  hr = 0x8007001f, A device attached to the system is not functioning. [30/12/2021 10:53:12.512]", lines[1]);
    Assert.Equal("- Code: SECSECRC00000581- Call: SECSECRC00000565- PID:  00032356- TID:  00000788- CMD:  C:\\WINDOWS\\system32\\vssvc.exe   - User: Name: NT AUTHORITY\\SYSTEM, SID:S-1-5-18. Index: 290107", lines[2].TrimEnd());
    Assert.Equal("8193E: Volume Shadow Copy Service error: Unexpected error calling routine QueryFullProcessImageNameW.  hr = 0x8007001f, A device attached to the system is not functioning. [30/12/2021 10:55:38.380]", lines[3]);
    Assert.Equal("- Code: SECSECRC00000581- Call: SECSECRC00000565- PID:  00032356- TID:  00028688- CMD:  C:\\WINDOWS\\system32\\vssvc.exe   - User: Name: NT AUTHORITY\\SYSTEM, SID:S-1-5-18. Index: 290277", lines[4]);
    Assert.Equal("8224I: The VSS service is shutting down due to idle timeout. [30/12/2021 11:03:26.902]", lines[5].TrimEnd());
    // tail
    Assert.StartsWith("4 Entries shown from the", lines[6]);
  }

  [Fact]
  public void VSSLogReturnsCorrectOutputWithOption2()
  {
    // The "VSS" log has error messages that use '\r\n' rather than '\r\n\rn' and this checks we handle that case

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "290107", "-l", vSSSampleEventLog, "-2" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(8, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("8193E: Volume Shadow Copy Service error: Unexpected error calling routine QueryFullProcessImageNameW.  hr = 0x8007001f, A device attached to the system is not functioning.", lines[0].TrimEnd());
    Assert.Equal(".", lines[1].TrimEnd());
    Assert.Equal("Operation:", lines[2].TrimEnd());
    Assert.Equal("   Executing Asynchronous Operation", lines[3]);
    Assert.Equal("Context:", lines[4]);
    Assert.Equal("   Current State: DoSnapshotSet [30/12/2021 10:53:12.512]", lines[5].TrimEnd());
    Assert.Equal(@"- Code: SECSECRC00000581- Call: SECSECRC00000565- PID:  00032356- TID:  00000788- CMD:  C:\WINDOWS\system32\vssvc.exe   - User: Name: NT AUTHORITY\SYSTEM, SID:S-1-5-18. Index: 290107", lines[6].TrimEnd());
    // tail
    Assert.StartsWith("Matching entry found in the", lines[7]);
  }

  [Fact]
  public void InvalidLogReturnsErrorWhenUsed()
  {
    // The "Invalid-Log" log has a valid message from the VSS service in it but the FormatMessage API can't parse it
    // on an En-GB locale machine as the LocalMetadata MTA file is present for En-GB but does not have the message present in it.
    // Therefore, we are using En-GB as the LocalMetadata MTA file for En-GB has no data. This causes an exception and we produce
    // an error which we are looking for in this test. 
    // However, we need to force the thread locale to En-GB for this to happen or on a different locale machine, e,g En-US this
    // will return the actual message which we don't want here and this test will fail.
    SetThreadLocale((ushort)CultureInfo.CurrentCulture.LCID); // it will be 2057 which is En-GB as set in the constructor
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", invalidEventLogName, "-3" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("8224I: The description for Event ID 8224 from source VSS cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer.", lines[0].TrimEnd());
    Assert.Equal("If the event originated on another computer, the display information had to be saved with the event.", lines[1]);
    Assert.Equal("The following information was included with the event:", lines[2].TrimEnd());
    Assert.Equal("Element not found. [21/12/2021 21:49:36.851]", lines[3].TrimEnd());
    // tail
    Assert.StartsWith("1 Entries shown from the", lines[4]);
  }

  [Fact]
  public void RestartManagerLogReturnsUserIdWhenVerboseOptionSet()
  {
    // The RestartManager log contains an example of a user id, process id and thread id

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", restartManagerSampleEventLog, "-v" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("10005I: Machine restart is required. [16/01/2022 14:59:02.873]", lines[0].TrimEnd());
    Assert.StartsWith("Machine: mgk-PC3. Log: ", lines[1]);
    // Avoid putting the repo name in the comparison in case it's a github ZIP download that ends with "-main"
    Assert.EndsWith("test\\EventLogMonitorTests\\SampleEventLogs\\RestartManager-Log.evtx. Source: Microsoft-Windows-RestartManager. User: S-1-5-18. ProcessId: 44120. ThreadId: 40204.", lines[1]);    // tail
    Assert.StartsWith("1 Entries shown from the", lines[2]);
  }

  [Fact]
  public void SecurityLogReturns_F_And_S_SuffixedEventIds()
  {
    // The Security log should be formattable on any machine with En-US installed
    // TODO we should make our own catalogue for these security events just incase of a none En-US machine...

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", securitySampleEventLog };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(5, lines.Length); // one extra closing test line is returned                               
    Assert.Equal("4957F: Windows Firewall did not apply the following rule: [05/02/2022 13:54:04.551]", lines[0].TrimEnd());
    Assert.Equal("4662S: An operation was performed on an object. [05/02/2022 14:40:21.712]", lines[1].TrimEnd());
    Assert.Equal("4662S: An operation was performed on an object. [05/02/2022 14:40:21.720]", lines[2].TrimEnd());
    Assert.Equal("4957F: Windows Firewall did not apply the following rule: [06/02/2022 15:57:02.961]", lines[3].TrimEnd());
    // tail
    Assert.StartsWith("4 Entries shown from the", lines[4]);
  }
}

public abstract class TestEventLogReader : EventLogReader
{
  public TestEventLogReader() : base("") { }
  public TestEventLogReader(EventLogQuery a) : base(a) { }

  public abstract new EventRecord ReadEvent();
}

public abstract class TestEventLogWatcher : EventLogWatcher
{
  public TestEventLogWatcher() : base("") { }
  public TestEventLogWatcher(EventLogQuery a) : base(a) { }
  public abstract new event EventHandler<EventLogMonitor.MyEventRecordWrittenEventArgs> EventRecordWritten;
}

