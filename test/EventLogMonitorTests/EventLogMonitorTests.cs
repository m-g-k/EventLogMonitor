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
using System.Numerics;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.Serialization;

namespace EventLogMonitor;

[Collection("EventLogMonitor")]
public class EventLogMonitorTests
{
  // [SuppressMessage("Microsoft.Usage", "IDE0052:RemoveUnreadPrivateMember", MessageId = "stdoutput")]
  private readonly ITestOutputHelper stdoutput;
  private static readonly string ace11SampleEventLogDLLsLocation = "../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/ACE-11-Log.evtx";
  private static readonly string ace11SampleEventLogLocaleMetaDataLocation = "../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/ACE-11-Log.evtx";
  private static readonly CultureInfo enGBCulture = new("En-GB", true);

  [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
  static extern System.UInt16 SetThreadLocale(System.UInt16 langId);

  [DllImport("kernel32.dll")]
  static extern uint GetThreadLocale();

  class Ace11SampleEventLogLocationData : TheoryData<string>
  {
    public Ace11SampleEventLogLocationData()
    {
      Add(ace11SampleEventLogDLLsLocation);
      Add(ace11SampleEventLogLocaleMetaDataLocation);
    }
  }

  class PowerShellSampleEventLogLocationData : TheoryData<string>
  {
    public PowerShellSampleEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/POSH-Log.evtx");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/POSH-Log.evtx");
    }
  }

  class VSSSampleEventLogLocationData : TheoryData<string>
  {
    public VSSSampleEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/VSS-Log.evtx");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/VSS-Log.evtx");
    }
  }

  class RestartManagerSampleEventLogLocationData : TheoryData<string>
  {
    public RestartManagerSampleEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/RestartManager-Log.evtx");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/RestartManager-Log.evtx");
    }
  }

  class SecuritySampleEventLogLocationData : TheoryData<string>
  {
    public SecuritySampleEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/Security-Log.evtx");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/Security-Log.evtx");
    }
  }

  class KernelPowerEventLogLocationData : TheoryData<string>
  {
    public KernelPowerEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/KernelPower-Log.evtx");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/KernelPower-Log.evtx");
    }
  }

  class InvalidEventLogLocationData : TheoryData<string>
  {
    public InvalidEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/Invalid-Log.evtx");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/Invalid-Log.evtx");
    }
  }

  class Ace11SampleEventLogCultureSpecificData : TheoryData<int, string, string>
  {
    public Ace11SampleEventLogCultureSpecificData()
    {
      // first test named cultures
      Add(1, "en-US", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]");
      Add(2, "de-DE", "BIP2152I: ( MGK.main ) Konfigurationsnachricht empfangen. [23/12/2021 11:58:11.763]");
      Add(3, "es-ES", "BIP2152I: ( MGK.main ) Se ha recibido el mensaje de configuración. [23/12/2021 11:58:11.763]");
      Add(4, "fr-FR", "BIP2152I: ( MGK.main ) Message de configuration reçu. [23/12/2021 11:58:11.763]");
      Add(5, "it-IT", "BIP2152I: ( MGK.main ) È stato ricevuto un messaggio di configurazione. [23/12/2021 11:58:11.763]");
      Add(6, "ja-JP", "BIP2152I: ( MGK.main ) 構成メッセージが受信されました。 [23/12/2021 11:58:11.763]");
      Add(7, "ko-KR", "BIP2152I: ( MGK.main ) 구성 메시지를 수신했습니다. [23/12/2021 11:58:11.763]");
      Add(8, "pl-PL", "BIP2152I: ( MGK.main ) Odebrano komunikat dotyczący konfiguracji. [23/12/2021 11:58:11.763]");
      Add(9, "pt-BR", "BIP2152I: ( MGK.main ) Mensagem de configuração recebida. [23/12/2021 11:58:11.763]");
      Add(10, "ru-RU", "BIP2152I: ( MGK.main ) Получено сообщение конфигурации. [23/12/2021 11:58:11.763]");
      Add(11, "tr-TR", "BIP2152I: ( MGK.main ) Yapılandırma iletisi alındı. [23/12/2021 11:58:11.763]");
      Add(12, "zh-CN", "BIP2152I: ( MGK.main ) 接收到配置消息。 [23/12/2021 11:58:11.763]");
      Add(13, "zh-TW", "BIP2152I: ( MGK.main ) 接收到配置訊息。 [23/12/2021 11:58:11.763]");
      // test missing cases
      Add(14, "ka-GE", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // ka-GE is not present in dll or MTA so expect gracefull fall back to en-US
      Add(15, "en-GB", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // en-GB is not present in dll or MTA so expect gracefull fall back to en-US
      // test again with culture as hex LCID
      Add(16, "0x0409", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]");
      Add(17, "0x0407", "BIP2152I: ( MGK.main ) Konfigurationsnachricht empfangen. [23/12/2021 11:58:11.763]");
      Add(18, "0x0c0A", "BIP2152I: ( MGK.main ) Se ha recibido el mensaje de configuración. [23/12/2021 11:58:11.763]");
      Add(19, "0x040C", "BIP2152I: ( MGK.main ) Message de configuration reçu. [23/12/2021 11:58:11.763]");
      Add(20, "0x0410", "BIP2152I: ( MGK.main ) È stato ricevuto un messaggio di configurazione. [23/12/2021 11:58:11.763]");
      Add(21, "0x0411", "BIP2152I: ( MGK.main ) 構成メッセージが受信されました。 [23/12/2021 11:58:11.763]");
      Add(22, "0x0412", "BIP2152I: ( MGK.main ) 구성 메시지를 수신했습니다. [23/12/2021 11:58:11.763]");
      Add(23, "0x0415", "BIP2152I: ( MGK.main ) Odebrano komunikat dotyczący konfiguracji. [23/12/2021 11:58:11.763]");
      Add(24, "0x0416", "BIP2152I: ( MGK.main ) Mensagem de configuração recebida. [23/12/2021 11:58:11.763]");
      Add(25, "0x0419", "BIP2152I: ( MGK.main ) Получено сообщение конфигурации. [23/12/2021 11:58:11.763]");
      Add(26, "0x041F", "BIP2152I: ( MGK.main ) Yapılandırma iletisi alındı. [23/12/2021 11:58:11.763]");
      Add(27, "0x0804", "BIP2152I: ( MGK.main ) 接收到配置消息。 [23/12/2021 11:58:11.763]");
      Add(28, "0x0404", "BIP2152I: ( MGK.main ) 接收到配置訊息。 [23/12/2021 11:58:11.763]");
      // test missing cases as hex LCID
      Add(29, "0x0437", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // ka-GE is not present in dll or MTA so expect gracefull fall back to en-US
      Add(30, "0x0809", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // en-GB is not present in dll or MTA so expect gracefull fall back to en-US
      // test again with culture as decimal LCID
      Add(31, "1033", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]");
      Add(32, "1031", "BIP2152I: ( MGK.main ) Konfigurationsnachricht empfangen. [23/12/2021 11:58:11.763]");
      Add(33, "3082", "BIP2152I: ( MGK.main ) Se ha recibido el mensaje de configuración. [23/12/2021 11:58:11.763]");
      Add(34, "1036", "BIP2152I: ( MGK.main ) Message de configuration reçu. [23/12/2021 11:58:11.763]");
      Add(35, "1040", "BIP2152I: ( MGK.main ) È stato ricevuto un messaggio di configurazione. [23/12/2021 11:58:11.763]");
      Add(36, "1041", "BIP2152I: ( MGK.main ) 構成メッセージが受信されました。 [23/12/2021 11:58:11.763]");
      Add(37, "1042", "BIP2152I: ( MGK.main ) 구성 메시지를 수신했습니다. [23/12/2021 11:58:11.763]");
      Add(38, "1045", "BIP2152I: ( MGK.main ) Odebrano komunikat dotyczący konfiguracji. [23/12/2021 11:58:11.763]");
      Add(39, "1046", "BIP2152I: ( MGK.main ) Mensagem de configuração recebida. [23/12/2021 11:58:11.763]");
      Add(40, "1049", "BIP2152I: ( MGK.main ) Получено сообщение конфигурации. [23/12/2021 11:58:11.763]");
      Add(41, "1055", "BIP2152I: ( MGK.main ) Yapılandırma iletisi alındı. [23/12/2021 11:58:11.763]");
      Add(42, "2052", "BIP2152I: ( MGK.main ) 接收到配置消息。 [23/12/2021 11:58:11.763]");
      Add(43, "1028", "BIP2152I: ( MGK.main ) 接收到配置訊息。 [23/12/2021 11:58:11.763]");
      // test missing cases as decimal LCID
      Add(44, "1079", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // ka-GE is not present in dll or MTA so expect gracefull fall back to en-US
      Add(45, "2057", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // en-GB is not present in dll or MTA so expect gracefull fall back to en-US
      // test edge cases as hex LCID
      Add(46, "0x0000", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // 0 is not present in dll or MTA so expect gracefull fall back to en-US
      Add(47, "0xFFFF", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // 65535 is not present in dll or MTA so expect gracefull fall back to en-US
      // test edge cases as decimal LCID
      Add(48, "0", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // 0 is not present in dll or MTA so expect gracefull fall back to en-US
      Add(49, "65535", "BIP2152I: ( MGK.main ) Configuration message received. [23/12/2021 11:58:11.763]"); // 65535 is not present in dll or MTA so expect gracefull fall back to en-US
    }
  }

  private static readonly List<string> miscMaxResults = new()
  {
    "2155I: ( Msg 2155 ) [MAX(FFFF)] Message 2155 in LCID FFFF (MAX). Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [MAX(FFFF)] Message 3132 in LCID FFFF (MAX). Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [MAX(FFFF)] Message 2208 in LCID FFFF (MAX). Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [MAX(FFFF)] Message 1234 in LCID FFFF (MAX). Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [MAX(FFFF)] Message 1234 in LCID FFFF (MAX). Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [MAX(FFFF)] Message 1234 in LCID FFFF (MAX). Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscUKResults = new()
  {
    "2155I: ( Msg 2155 ) [English GB(2057)] Message 2155 in En-GB. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [English GB(2057)] Message 3132 in En-GB. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [English GB(2057)] Message 2208 in En-GB. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [English GB(2057)] Message 1234 in En-GB. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [English GB(2057)] Message 1234 in En-GB. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [English GB(2057)] Message 1234 in En-GB. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscFrenchResults = new()
  {
    "2155I: ( Msg 2155 ) [French(1036)] Message 2155 in French. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [French(1036)] Message 3132 in French. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [French(1036)] Message 2208 in French. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [French(1036)] Message 1234 in French. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [French(1036)] Message 1234 in French. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [French(1036)] Message 1234 in French. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]",
  };

  private static readonly List<string> miscUSResults = new()
  {
    "2155I: ( Msg 2155 ) [English US(1033)] Message 2155 in En-US. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [English US(1033)] Message 3132 in En-US. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [English US(1033)] Message 2208 in En-US. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [English US(1033)] Message 1234 in En-US. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [English US(1033)] Message 1234 in En-US. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [English US(1033)] Message 1234 in En-US. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscGermanResults = new()
  {
    "2155I: ( Msg 2155 ) [German(1031)] Message 2155 in German. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [German(1031)] Message 3132 in Gernam. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [German(1031)] Message 2208 in German. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [German(1031)] Message 1234 in German. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [German(1031)] Message 1234 in German. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [German(1031)] Message 1234 in German. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscDanishResults = new()
  {
    "2155I: ( Msg 2155 ) [Danish(1030)] Message 2155 in Danish. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [Danish(1030)] Message 3132 in Danish. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [Danish(1030)] Message 2208 in Danish. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [Danish(1030)] Message 1234 in Danish. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [Danish(1030)] Message 1234 in Danish. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [Danish(1030)] Message 1234 in Danish. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscDutchResults = new()
  {
    "2155I: ( Msg 2155 ) [Dutch(19)] Message 2155 in Dutch. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [Dutch(19)] Message 3132 in Dutch. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [Dutch(19)] Message 2208 in Dutch. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [Dutch(19)] Message 1234 in Dutch. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [Dutch(19)] Message 1234 in Dutch. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [Dutch(19)] Message 1234 in Dutch. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscArabicResults = new()
  {
    "2155I: ( Msg 2155 ) [Arabic(1)] Message 2155 in Arabic. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [Arabic(1)] Message 3132 in Arabic. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [Arabic(1)] Message 2208 in Arabic. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [Arabic(1)] Message 1234 in Arabic. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [Arabic(1)] Message 1234 in Arabic. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [Arabic(1)] Message 1234 in Arabic. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  private static readonly List<string> miscLanguageNeutralResults = new()
  {
    "2155I: ( Msg 2155 ) [LanguageNeutral(0)] Message 2155 in LanguageNeutral 0. Insert 2: 'Information Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.420]",
    "3132W: ( Msg 3132 ) [LanguageNeutral(0)] Message 3132 in LanguageNeutral 0. Insert 2: 'Warning Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.422]",
    "2208E: ( Msg 2208 ) [LanguageNeutral(0)] Message 2208 in LanguageNeutral 0. Insert 2: 'Error Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I: ( Msg 1234 ) [LanguageNeutral(0)] Message 1234 in LanguageNeutral 0. Insert 2: 'Success Event', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.425]",
    "1234I: ( Msg 1234 ) [LanguageNeutral(0)] Message 1234 in LanguageNeutral 0. Insert 2: 'Failure Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.426]",
    "1234I: ( Msg 1234 ) [LanguageNeutral(0)] Message 1234 in LanguageNeutral 0. Insert 2: 'Success Audit', Insert 3: 'Insert Three'. [18/09/2023 15:36:04.428]"
  };

  class MiscTestsSpecificData : TheoryData<int, string, string, List<string>>
  {
    private static readonly string ibase = "../../../../../test/EventLogMonitorTests/SampleEventLogsMisc_Dlls/";
    private static readonly string iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0 = ibase + "misc-LCID-65535-2057-1036-1033-1031-1030-19-1-0.evtx";
    private static readonly string iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1 = ibase + "misc-LCID-65535-2057-1036-1033-1031-1030-19-1.evtx";
    private static readonly string iMisc_LCID_65535_1036_1033_1031_1030_19_1 = ibase + "misc-LCID-65535-1036-1033-1031-1030-19-1.evtx";
    private static readonly string iMisc_LCID_65535_1036_1031_1030_19_0 = ibase + "misc-LCID-65535-1036-1031-1030-19-0.evtx";
    private static readonly string iMisc_LCID_65535_1036_1031_1030_19_1 = ibase + "misc-LCID-65535-1036-1031-1030-19-1.evtx";
    private static readonly string iMisc_LCID_65535_1036_1031_1030_19 = ibase + "misc-LCID-65535-1036-1031-1030-19.evtx";
    private static readonly string iMisc_LCID_65535 = ibase + "misc-LCID-65535.evtx";


    public MiscTestsSpecificData()
    {
      // first check we can get all the messages out of the "complete" dll with all the languages
      Add(1, "0x0", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscLanguageNeutralResults); // 0
      Add(2, "Ar", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscArabicResults);           // 1
      Add(3, "Nl-NL", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscDutchResults);         // 19
      Add(4, "Da-DK", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscDanishResults);        // 1030
      Add(5, "De-DE", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscGermanResults);        // 1031
      Add(6, "en-US", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscUSResults);            // 1033
      Add(7, "Fr-FR", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscFrenchResults);        // 1036
      Add(8, "en-GB", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscUKResults);            // 2057
      Add(9, "0xFFFF", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscMaxResults);          // 65535

      // now check that we fall back to 0 when we ask for italian even when uk english and us english are present if 0 is present (italian not present on purpose)
      Add(10, "it-IT", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscLanguageNeutralResults);

      // now check that we fall back to US when we ask for italian even when uk english and us english are present but 0 is not (italian not present on purpose)
      Add(11, "it-IT", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1, miscUSResults);

      // now check that we fall back to US when we ask for uk english and us is present but 0 is not
      Add(12, "en-GB", iMisc_LCID_65535_1036_1033_1031_1030_19_1, miscUSResults);

      // now check that we fall back to 0 when we ask for uk or us english and us english is not present and 0 is present
      Add(13, "en-GB", iMisc_LCID_65535_1036_1031_1030_19_0, miscLanguageNeutralResults);
      Add(14, "en-US", iMisc_LCID_65535_1036_1031_1030_19_0, miscLanguageNeutralResults);

      // now check that we fall back to 1 when we ask for uk or us english and there is no 0 or us english
      Add(15, "en-GB", iMisc_LCID_65535_1036_1031_1030_19_1, miscArabicResults);
      Add(16, "en-US", iMisc_LCID_65535_1036_1031_1030_19_1, miscArabicResults);

      // now check that we fall back to 19 when we ask for uk or us english and there is no 1 or 0 or us english
      Add(17, "en-GB", iMisc_LCID_65535_1036_1031_1030_19, miscDutchResults);
      Add(18, "en-US", iMisc_LCID_65535_1036_1031_1030_19, miscDutchResults);

      // fall back to Max when it is the only msg table in the dll
      Add(19, "en-US", iMisc_LCID_65535, miscMaxResults);

      // calling with no culture should give lang neutral if present, or local LCID or US or else lowest numbered LCID
      // see the flow chart in ExportingEvents.md for the rules
      // Note. As there is no culture specified (on purpose) it is possible for some of these tests to fail if
      // they are run on a machine with the local set to one that is already present in the dll.
      // E.G test 21 below fails on US (1033) machines as that takes priority over the 2057 set the thread by the test
      // unless it is run on a UK (2057) locale machine.
      Add(20, "", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1_0, miscLanguageNeutralResults); // 0
      // Add(21, "", iMisc_LCID_65535_2057_1036_1033_1031_1030_19_1, miscUKResults); // 2057. Disabled. This fails on US lang machines so breaks when run by github actions
      Add(22, "", iMisc_LCID_65535_1036_1033_1031_1030_19_1, miscUSResults); // 1033
      Add(23, "", iMisc_LCID_65535_1036_1031_1030_19_0, miscLanguageNeutralResults); // 0
      Add(24, "", iMisc_LCID_65535_1036_1031_1030_19_1, miscArabicResults); // 1
      Add(25, "", iMisc_LCID_65535_1036_1031_1030_19, miscDutchResults); // 19
      Add(26, "", iMisc_LCID_65535, miscMaxResults); // 65535 this is the "only" and therefore "lowest numbered" msg table
    }
  }

  // Results when entry without a dll is PATCHED
  private static readonly List<string> patchedWithBinaryResults =
  [
    "2155I [P]: Msg 2155.",
    "Information Event.",
    "Insert Three. [18/09/2023 15:36:04.420]",
    "3132W [P]: Msg 3132.",
    "Warning Event.",
    "Insert Three. [18/09/2023 15:36:04.422]",
    "2208E [P]: Msg 2208.",
    "Error Event.",
    "Insert Three. [18/09/2023 15:36:04.425]",
    "namarie. Index: 874790",
    "1234I [P]: Msg 1234.",
    "Success Event.",
    "Insert Three. [18/09/2023 15:36:04.425]",
    "1234I [P]: Msg 1234.",
    "Failure Audit.",
    "Insert Three. [18/09/2023 15:36:04.426]",
    "1234I [P]: Msg 1234.",
    "Success Audit.",
    "Insert Three. [18/09/2023 15:36:04.428]",
  ];

  private static readonly List<string> patchedWithNoBinaryResults =
  [
    "2155I [P]: Msg 2155.",
    "Information Event - no extra binary data included.",
    "Insert Three. [31/12/2023 09:35:30.312]",
    "3132W [P]: Msg 3132.",
    "Warning Event - no extra binary data included.",
    "Insert Three. [31/12/2023 09:35:30.314]",
    "2208E [P]: Msg 2208.",
    "Error Event - no extra binary data included.",
    "Insert Three. [31/12/2023 09:35:30.315]",
    "<Entry has no binary data>. Index: 911306",
    "1234I [P]: Msg 1234.",
    "Success Event - no extra binary data included.",
    "Insert Three. [31/12/2023 09:35:30.317]",
    "1234I [P]: Msg 1234.",
    "Failure Audit - no extra binary data included.",
    "Insert Three. [31/12/2023 09:35:30.318]",
    "1234I [P]: Msg 1234.",
    "Success Audit - no extra binary data included.",
    "Insert Three. [31/12/2023 09:35:30.320]",
  ];

  private static readonly List<string> patchedWithOnlyBinaryResults =
  [
    "5678I: The description for Event ID 5678 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer.",
    "If the event originated on another computer, the display information had to be saved with the event.",
    "The following information was included with the event:",
    "The message resource is present but the message was not found in the message table [31/12/2023 11:03:43.279]"
  ];

  private static readonly List<string> patchedWithNoBinaryOrOtherInsertsResults =
  [
    "5678I: The description for Event ID 5678 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer.",
    "If the event originated on another computer, the display information had to be saved with the event.",
    "The message resource is present but the message was not found in the message table [31/12/2023 12:13:09.60]"
  ];

  private static readonly List<string> patchedUniversalPrintResults =
  [
    "1I [P]: Device is AAD/Domain Joined..",
    "mcpmanagementservice.dll. [12/02/2023 13:45:04.385]",
    "1I [P]: Using cached state. State: Enabled.",
    "mcpmanagementservice.dll. [12/02/2023 13:45:04.387]",
  ];

  class PatchedTestsSpecificData : TheoryData<int, string, List<string>, int>
  {
    private static readonly string iPatchSampleEventLogLocationWithBinary = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-with-extra-binary-data.evtx";
    private static readonly string iPatchSampleEventLogLocationWithNoBinary = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-no-extra-binary-data.evtx";
    private static readonly string iPatchSampleEventLogLocationWithOnlyBinaryInsert = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-with-only-binary-data-as-insert.evtx";
    private static readonly string iPatchSampleEventLogLocationWithNoBinaryOrOtherInserts = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-no-extra-binary-data-or-inserts.evtx";
    private static readonly string iPatchSampleEventLogLocationUniversalPrint = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/UniversalPrint.evtx";

    public PatchedTestsSpecificData()
    {
      Add(1, iPatchSampleEventLogLocationWithBinary, patchedWithBinaryResults, 6);
      Add(2, iPatchSampleEventLogLocationWithNoBinary, patchedWithNoBinaryResults, 6);
      Add(3, iPatchSampleEventLogLocationWithOnlyBinaryInsert, patchedWithOnlyBinaryResults, 1);
      Add(4, iPatchSampleEventLogLocationWithNoBinaryOrOtherInserts, patchedWithNoBinaryOrOtherInsertsResults, 1);
      // Note test 5 expects the "Universal Print" provider to be registered in the registry with a valid DLL, but
      // at the time of writing the DLL references an MUI which is not found and this is the crux of this test.
      // The interenet shows this has been the case for a long time, but if MS ever fix it this test will break,
      // and should probably be retired at that point and "Universal Print" removed from the list of special providers.
      Add(5, iPatchSampleEventLogLocationUniversalPrint, patchedUniversalPrintResults, 2);
    }
  }

  // Results when entry without a dll is NOT PATCHED
  private static readonly List<string> notPatchedWithBinaryResults =
    [
      "2155I: The description for Event ID 2155 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [18/09/2023 15:36:04.420]",
      "3132W: The description for Event ID 3132 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [18/09/2023 15:36:04.422]",
      "2208E: The description for Event ID 2208 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [18/09/2023 15:36:04.425]",
      "namarie. Index: 874790",
      "1234I: The description for Event ID 1234 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [18/09/2023 15:36:04.425]",
      "1234I: The description for Event ID 1234 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [18/09/2023 15:36:04.426]",
      "1234I: The description for Event ID 1234 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [18/09/2023 15:36:04.428]"
    ];

  private static readonly List<string> notPatchedWithNoBinaryResults =
  [
    "2155I: The description for Event ID 2155 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 09:35:30.312]",
    "3132W: The description for Event ID 3132 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 09:35:30.314]",
    "2208E: The description for Event ID 2208 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 09:35:30.315]",
    "<Entry has no binary data>. Index: 911306",
    "1234I: The description for Event ID 1234 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 09:35:30.317]",
    "1234I: The description for Event ID 1234 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 09:35:30.318]",
    "1234I: The description for Event ID 1234 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 09:35:30.320]"
  ];

  private static readonly List<string> notPatchedWithOnlyBinaryResults =
  [
    "5678I: The description for Event ID 5678 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 11:03:43.279]"
  ];

  private static readonly List<string> notPatchedWithNoBinaryOrOtherInsertsResults =
  [
    "5678I: The description for Event ID 5678 from source EventLogMonitorTestLogSource cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [31/12/2023 12:13:09.60]"
  ];

  private static readonly List<string> notPatchedUniversalPrintResults =
  [
    "1I: The description for Event ID 1 from source Universal Print cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [12/02/2023 13:45:04.385]",
    "1I: The description for Event ID 1 from source Universal Print cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [12/02/2023 13:45:04.387]",
  ];

  class NotPatchedTestsSpecificData : TheoryData<int, string, List<string>, int>
  {
    private static readonly string iPatchSampleEventLogLocationWithBinary = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-with-extra-binary-data.evtx"; // both binary and not binary inserts
    private static readonly string iPatchSampleEventLogLocationWithNoBinary = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-no-extra-binary-data.evtx"; // only inserts that are not binary
    private static readonly string iPatchSampleEventLogLocationWithOnlyBinaryInsert = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-with-only-binary-data-as-insert.evtx"; // only one insert which is binary
    private static readonly string iPatchSampleEventLogLocationWithNoBinaryOrOtherInserts = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/misc-no-extra-binary-data-or-inserts.evtx"; // no inserts at all
    private static readonly string iPatchSampleEventLogLocationUniversalPrint = "../../../../../test/EventLogMonitorTests/SampleEventLogsPatch_EventOnly/UniversalPrint.evtx";

    public NotPatchedTestsSpecificData()
    {
      Add(1, iPatchSampleEventLogLocationWithBinary, notPatchedWithBinaryResults, 6);
      Add(2, iPatchSampleEventLogLocationWithNoBinary, notPatchedWithNoBinaryResults, 6);
      Add(3, iPatchSampleEventLogLocationWithOnlyBinaryInsert, notPatchedWithOnlyBinaryResults, 1);
      Add(4, iPatchSampleEventLogLocationWithNoBinaryOrOtherInserts, notPatchedWithNoBinaryOrOtherInsertsResults, 1);
      Add(5, iPatchSampleEventLogLocationUniversalPrint, notPatchedUniversalPrintResults, 2);
    }
  }

  public EventLogMonitorTests(ITestOutputHelper testOutputHelper)
  {
    // set up to capture stdout
    stdoutput = testOutputHelper;

    // Several tests produce output that includes the expected date and time in UK (En-GB - LCID 2057) format,
    // so we must force UK style output even when the machine running the tests is not in this locale.
    Thread.CurrentThread.CurrentCulture = enGBCulture;
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
  /*
    [Fact]
    public void UsingBothPatchOptionsGivesAnErrorMessage()
    {
      // replace stdout to capture it
      var output = new StringWriter();
      Console.SetOut(output);

      string[] args = new string[] { "-patch", "test,bar", "-nopatch" };
      EventLogMonitor monitor = new();
      monitor.Initialize(args);
      string help = output.ToString();
      Assert.StartsWith("Invalid arguments or invalid argument combination: only one of options '-patch' and '-nopatch' may be specified.", help);
    }
  */
  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsByDefault1(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);
    string extraOptions = ""; // make sure empty options ignored

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, extraOptions };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsByDefault2(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);
    string extraOptions = "-1"; // -1 is the default output type if not present or overridden with -2 or -3

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, extraOptions };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithMediumOutput(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-2" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithFullOutput(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-3" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void UsingMoreThanOneOutputTypeReturnsAnError(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-3", "-2" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: only one of options '-1', '-2' and '-3' may be specified.", logOut);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2WithTFReturnsTwoMostRecentPreviousEventsWithTimestampFirst(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-tf" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2WithTFReturnsTwoMostRecentPreviousEventsWithTimestampFirstInUTC(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-tf", "-utc" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    // Note that UTC is the same as none UTC for these date / times as DST is not in effect
    Assert.Equal("23/12/2021 11:58:12.195: BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully.", lines[0]);
    Assert.Equal("23/12/2021 11:58:12.195: BIP2154I: ( MGK.main ) Integration server finished with Configuration message.", lines[1]);
    Assert.StartsWith("2 Entries shown from the", lines[2]);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithBinaryDataAsUnicode(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-b1" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithBinaryDataAsHex(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-b2" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsWithExtraVerboseOutput(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-v" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsInGerman(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-c", "de" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionInvalidCultureReturnsError(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // note that as we do not specify a -p option, all events are returned after the expected warning message
    string[] args = new string[] { "-l", ace11SampleEventLogLocation, "-c", "fake" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(66, lines.Length); // one extra closing test line is returned
                                    // expected warning line
    Assert.Equal("Culture is not supported. 'fake' is an invalid culture identifier. Defaulting to 'En-US'.", lines[0].TrimEnd());
    // oldest 2 entries
    Assert.Equal("BIP2001I: ( MGK ) The IBM App Connect Enterprise service has started at version '110011'; process ID 7192. [18/11/2021 18:21:16.344]", lines[1]);
    Assert.Equal("BIP3132I: ( MGK ) The HTTP Listener has started listening on port ''4414'' for ''RestAdmin http'' connections. [18/11/2021 18:21:26.571]", lines[2]);
    // most recent 2 entries
    Assert.Equal("BIP2269I: ( MGK.main ) Deployed resource ''test'' (uuid=''test'',type=''MessageFlow'') started successfully. [23/12/2021 11:58:12.195]", lines[63]);
    Assert.Equal("BIP2154I: ( MGK.main ) Integration server finished with Configuration message. [23/12/2021 11:58:12.195]", lines[64]);
    Assert.StartsWith("64 Entries shown from the", lines[65]);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionP2ReturnsTwoMostRecentPreviousEventsInEnglishWithInvalidCulture(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "2", "-l", ace11SampleEventLogLocation, "-c", "ABC" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionPStarReturnsAll64PreviousEvents(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionIndexReturnsSingleEvent(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282229", "-b1", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionIndexRangeReturnsThreeEventsInclusive(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282229-282259", "-b1", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionIndexWithOptionP3WithSparseIndexReturnsThreeEventsAfterIndex(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "240568", "-p", "3", "-b1", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionIndexWithSmallIndexAndLargerPValueReturnsCorrectIndexInError(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "5", "-p", "6", "-b1", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionIndexWithOptionP3ReturnsThreeEventsEitherSideOfTheIndex(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "240584", "-p", "3", "-b1", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionIndexWithOptionPStarReturnsLast8Events(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282271", "-p", "*", "-b1", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void InvalidIndexRangeReturnsError(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282-229-345", "-l", ace11SampleEventLogLocation };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: invalid range, use 'x-y' to specify a range.", logOut);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void UsingAnIndexWithOptionSIsAnError(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "229-282", "-s", "newSource", "-l", ace11SampleEventLogLocation };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: -s not allowed with -i.", logOut);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void InvalidIndexRangeEndLessThanStartReturnsError(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282271-282270", "-l", ace11SampleEventLogLocation };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.False(initialized, $"{initialized} should be false");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    Assert.StartsWith("Invalid arguments or invalid argument combination: index max > index min.", logOut);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void IndexHigherThanHigestEntryInLogIsOKReturnsNoEvents(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "282280", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterIncludeReturns5EventsInEnglish(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "Listening", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterIncludeReturns5EventsInGerman(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "empfangsbereit", "-c", "De-DE", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterIncludeReturns4Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "shutdown", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterIncludeAndExcludeReturns2Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "shutdown", "-fx", "immediate", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterIncludeAndWarnReturns2Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fi", "shutdown", "-fw", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterErrorOnlyReturns0Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-fe", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogCultureSpecificData))]
  public void EventLogWithLocalMetaDataReturnsCorrectCultureSpecificMessages(int testNumber, string culture, string expectedResult)
  {
    // replace stdout to capture it
    Console.Write(testNumber); // write to force usage
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "1", "-l", ace11SampleEventLogLocaleMetaDataLocation, "-c", culture, "-fn", "2152" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    Assert.Equal(expectedResult, lines[0]);
    Assert.StartsWith("1 Entries shown from the", lines[1]);
  }

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogCultureSpecificData))]
  public void EventLogWithDLLsReturnsCorrectCultureSpecificMessages(int testNumber, string culture, string expectedResult)
  {
    // replace stdout to capture it
    Console.Write(testNumber); // write to force usage
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "1", "-l", ace11SampleEventLogDLLsLocation, "-c", culture, "-fn", "2152" };
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(2, lines.Length); // one extra closing test line is returned
                                   // most recent 2 entries
    Assert.Equal(expectedResult, lines[0]);
    Assert.StartsWith("1 Entries shown from the", lines[1]);
  }

  [Theory()] // Skip = "Skipped as testing EventLogMonitorTestLogSource"
  [ClassData(typeof(MiscTestsSpecificData))]
  public void MiscSpecificMessages(int testNumber, string culture, string dllLocation, List<string> expectedResult)
  {
    // make sure the EventLogMonitorTestLogSource does not exist in the registry as it will cause 
    // this test to fail when it's not supposed to work when installed
    if (EventLogSourceExists("Application\\EventLogMonitorTestLogSource"))
    {
      throw new Exception("SETUP ERROR: HKLM\\SYSTEM\\CurrentControlSet\\Services\\EventLog\\Application\\EventLogMonitorTestLogSource must be disabled before running this test: " + testNumber);
    }

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args;
    // force UTC time for this tests as otherwise we are an hour out
    if (String.IsNullOrEmpty(culture))
    {
      args = new string[] { "-l", dllLocation, "-utc" };
    }
    else
    {
      args = new string[] { "-l", dllLocation, "-c", culture, "-utc" };
    }

    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();

    // incase of an error, output expected and actual as it's easier to debug
    stdoutput.WriteLine("Test Run Output:");
    stdoutput.WriteLine(logOut);
    stdoutput.WriteLine("Expected Count and Output: " + expectedResult.Count);
    foreach (string line in expectedResult)
    {
      stdoutput.WriteLine(line);
    }

    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(8, lines.Length); // one extra closing test line is returned
    List<string> results = new(lines[0..^1]);

    stdoutput.WriteLine("\nResults Count and Output: " + results.Count);
    foreach (string line in results)
    {
      stdoutput.WriteLine(line);
    }

    Assert.Equivalent(expectedResult, results, strict: true);
    Assert.StartsWith("6 Entries shown from the", lines[7]);
  }

  [Theory]
  [ClassData(typeof(KernelPowerEventLogLocationData))]
  public void OptionFilterCriticalReturns5Entries(string kernelPowerSampleEventLogLocation)
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // force a culture specific search
    string[] args = new string[] { "-p", "*", "-fc", "-c", "En-US", "-l", kernelPowerSampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterSingleIncludedIDsReturns2Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLogLocation}", "-fn=2011" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterSingleExcludedIDsReturns54Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLogLocation}", "-fn=-2155" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterIncludedRangeReturns25Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLogLocation}", "-fn=2150-2160" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterExcludedRangeReturns39Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLogLocation}", "-fn=-2150-2160" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionFilterMixedEventIdsReturns22Entries(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();
    
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the = form for arguments as well
    string[] args = new string[] { "-p=*", $"-l={ace11SampleEventLogLocation}", "-fn=2150-2160,-2154-2155,2011,2001,-2152,3130-3140" };
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

  [Theory]
  [ClassData(typeof(SecuritySampleEventLogLocationData))]
  public void OptionFilterIncludedRangeIDsReturns4Entries(string securitySampleEventLogLocation)
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the '=' form for the fn arguments as well as a trailing bool argument to ensure '=' form does not have to be last
    string[] args = new string[] { "-p=*", $"-l={securitySampleEventLogLocation}", "-fn=0-10000", "-v" };
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

  [Theory]
  [ClassData(typeof(SecuritySampleEventLogLocationData))]
  public void OptionFilterIncludedRangeIDsReturns4EntriesInAltCulture(string securitySampleEventLogLocation)
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // we use the '=' form for the fn arguments as well as a trailing bool argument to ensure '=' form does not have to be last
    string[] args = new string[] { "-p=*", "-c=En-US", $"-l={securitySampleEventLogLocation}", "-fn=0-10000", "-v" };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionDShowsDetailsOfEventLogFile(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-l", ace11SampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(Ace11SampleEventLogLocationData))]
  public void OptionDShowsDetailsOfEventLogFileVerbose(string ace11SampleEventLogLocation)
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // set it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    ValidateBrokerRegistryKey();

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-d", "-v", "-l", ace11SampleEventLogLocation };
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
    var mockQuery = new Mock<EventLogQuery>("Application", PathType.LogName);
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
    mockEventRecord.Setup(x => x.Level).Returns((int)StandardEventLevel.Verbose); // test verbose output works
    mockEventRecord.Setup(x => x.LogName).Returns("Application");
    mockEventRecord.Setup(x => x.ProviderName).Returns("WebSphere Broker");
    mockEventRecord.Setup(x => x.Properties).Returns(new List<EventProperty>());
    mockEventRecord.Setup(x => x.TimeCreated).Returns(new DateTime(2000, 1, 1, 12, 0, 0));
    var mockQuery = new Mock<EventLogQuery>("Application", PathType.LogName);
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
    Assert.Equal("BIP42V: The description for Event ID 42 from source WebSphere Broker cannot be found. Either the component that raises this event is not installed on your local computer or the installation is corrupted. You can install or repair the component on the local computer. [01/01/2000 12:00:00.0]", lines[0]);
  }

  [Fact]
  public void InvalidLevelForNoneExistantMessageDefaultsToInformationMessage()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    // mock an event to raise
    var mockEventRecord = new Mock<EventRecord>();
    mockEventRecord.Setup(x => x.Id).Returns(42);
    mockEventRecord.Setup(x => x.Level).Returns((int)42); // test invalid level works (defaults to Information)
    mockEventRecord.Setup(x => x.LogName).Returns("Application"); //
    //mockEventRecord.Setup(x => x.ContainerLog).Returns(ace11SampleEventLogLocaleMetaDataLocation);
    mockEventRecord.Setup(x => x.ProviderName).Returns("WebSphere Broker");
    mockEventRecord.Setup(x => x.Properties).Returns(new List<EventProperty>());
    mockEventRecord.Setup(x => x.TimeCreated).Returns(new DateTime(2000, 1, 1, 12, 0, 0));
    var mockQuery = new Mock<EventLogQuery>("Application", PathType.LogName);
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

  [Theory]
  [ClassData(typeof(PowerShellSampleEventLogLocationData))]
  public void OptionMultiSourceReturnsEvents(string powerShellSampleEventLogLocation)
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);
    string[] args = new string[] { "-s", "Restart, Test, Power", "-l", powerShellSampleEventLogLocation, "-p", "*" };
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

  [Theory]
  [ClassData(typeof(PowerShellSampleEventLogLocationData))]
  public void PowerShellLogReturnsSingleLinesOutput(string powerShellSampleEventLogLocation)
  {
    // The "Windows PowerShell" log has an embedded '\r\n' in the event 800 which should be stripped out

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", powerShellSampleEventLogLocation };
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

  [Theory]
  [ClassData(typeof(VSSSampleEventLogLocationData))]
  public void VSSLogReturnsASCIIOutputOutput(string vssSampleEventLogLocation)
  {
    // The "VSS" log has an 2 error messages with ASCII binary data in it which should be automatically be shown

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", vssSampleEventLogLocation, "-s", "*" };
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

  [Theory]
  [ClassData(typeof(VSSSampleEventLogLocationData))]
  public void VSSLogReturnsCorrectOutputWithOption2(string vssSampleEventLogLocation)
  {
    // The "VSS" log has error messages that use '\r\n' rather than '\r\n\rn' and this checks we handle that case

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-i", "290107", "-l", vssSampleEventLogLocation, "-2" };
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

  [Theory]
  [ClassData(typeof(InvalidEventLogLocationData))]
  public void InvalidLogReturnsErrorWhenUsed(string invalidSampleEventLogLocation)
  {
    // The "Invalid-Log" log has a valid message from the VSS service in it but the FormatMessage API can't parse it
    // on an En-GB locale machine as the LocalMetadata MTA file is present for En-GB but does not have the message present in it.
    // Therefore, we are using En-GB as the LocalMetadata MTA file to force the error. This causes an exception and we produce
    // an error which we are looking for in this test. 
    // However, we need to force the thread locale to En-GB for this to happen or on a different locale machine, e,g En-US, this
    // will return the actual message which we don't want here and this test will fail.
    var currentLocale = GetThreadLocale(); // get to reset
    try
    {
      SetThreadLocale((ushort)CultureInfo.CurrentCulture.LCID); // it will be 2057 which is En-GB as set in the constructor

      // replace stdout to capture it
      var output = new StringWriter();
      Console.SetOut(output);

      string[] args = new string[] { "-p", "*", "-l", invalidSampleEventLogLocation, "-3" };
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
    finally
    {
      SetThreadLocale((ushort)currentLocale);
    }
  }

  [Theory]
  [ClassData(typeof(RestartManagerSampleEventLogLocationData))]
  public void RestartManagerLogReturnsUserIdWhenVerboseOptionSet(string restartManagerSampleEventLogLocation)
  {
    // The RestartManager log contains an example of a user id, process id and thread id

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", restartManagerSampleEventLogLocation, "-v" };
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
    Assert.EndsWith("\\RestartManager-Log.evtx. Source: Microsoft-Windows-RestartManager. User: S-1-5-18. ProcessId: 44120. ThreadId: 40204.", lines[1]);    // tail
    Assert.StartsWith("1 Entries shown from the", lines[2]);
  }

  [Theory]
  [ClassData(typeof(SecuritySampleEventLogLocationData))]
  public void SecurityLogReturns_F_And_S_SuffixedEventIds(string securitySampleEventLogLocation)
  {
    // The Security log should be formattable on any machine with En-US installed
    // TODO we should make our own catalogue for these security events just incase of a none En-US machine...

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = new string[] { "-p", "*", "-l", securitySampleEventLogLocation };
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

  [Theory()] // Skip = "Skipped as testing EventLogMonitorTestLogSource"
  [ClassData(typeof(PatchedTestsSpecificData))]
  public void PatchOptionReturnsJustInsertsAsEventLogMessage(int testNumber, string dllLocation, List<string> expectedResult, int expectedEventLogMessages)
  {
    // make sure the EventLogMonitorTestLogSource does not exist in the registry as it will cause 
    // this test to fail when it's not supposed to work when installed
    if (EventLogSourceExists("Application\\EventLogMonitorTestLogSource"))
    {
      throw new Exception("SETUP ERROR: HKLM\\SYSTEM\\CurrentControlSet\\Services\\EventLog\\Application\\EventLogMonitorTestLogSource must be disabled before running this test: " + testNumber);
    }

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);
    string extraOptions = "-3"; // -1 is the default output type if not present or overridden with -2 or -3

    string[] args = ["-l", dllLocation, "-utc", extraOptions];
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    var expectedResultCount = expectedResult.Count;
    Assert.Equal(expectedResultCount + 1, lines.Length); // one extra closing test line is returned
    List<string> results = new(lines[0..^1]);

    stdoutput.WriteLine("\nResults Count and Output: " + results.Count);
    foreach (string line in results)
    {
      stdoutput.WriteLine(line);
    }

    Assert.Equivalent(expectedResult, results, strict: true);
    Assert.StartsWith($"{expectedEventLogMessages} Entries shown from the", lines[expectedResultCount]);
  }

  [Theory()] // Skip = "Skipped as testing EventLogMonitorTestLogSource"
  [ClassData(typeof(NotPatchedTestsSpecificData))]
  public void NotPatchOptionReturnsJustInsertsAsEventLogMessage(int testNumber, string dllLocation, List<string> expectedResult, int expectedEventLogMessages)
  {
    // make sure the EventLogMonitorTestLogSource does not exist in the registry as it will cause 
    // this test to fail when it's not supposed to work when installed
    if (EventLogSourceExists("Application\\EventLogMonitorTestLogSource"))
    {
      throw new Exception("SETUP ERROR: HKLM\\SYSTEM\\CurrentControlSet\\Services\\EventLog\\Application\\EventLogMonitorTestLogSource must be disabled before running this test: " + testNumber);
    }

    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    string[] args = ["-nopatch", "-l", dllLocation, "-utc"]; // -nopatch to remove patching default behaviours for missing providers
    EventLogMonitor monitor = new();
    bool initialized = monitor.Initialize(args);
    Assert.True(initialized, $"{initialized} should be true");
    monitor.MonitorEventLog();
    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    var expectedResultCount = expectedResult.Count;
    Assert.Equal(expectedResultCount + 1, lines.Length); // one extra closing test line is returned
    List<string> results = new(lines[0..^1]);

    stdoutput.WriteLine("\nResults Count and Output: " + results.Count);
    foreach (string line in results)
    {
      stdoutput.WriteLine(line);
    }

    Assert.Equivalent(expectedResult, results, strict: true);
    Assert.StartsWith($"{expectedEventLogMessages} Entries shown from the", lines[expectedResultCount]);
  }

  static bool EventLogSourceExists(string entry)
  {
    string? dll = Registry.GetValue(@$"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\{entry}", "EventMessageFile", null) as string;
    if (!string.IsNullOrEmpty(dll))
    {
      bool exists = File.Exists(dll);
      if (exists)
      {
        return true;
      }
    }
    return false;
  }

  static bool ValidateBrokerRegistryKey()
  {
    // make sure the "IBM App Connect Enterprise v110011" log source does not exist in the registry or if
    // exists it must point to a valid DLL. Otherwise this test will fail if key exists without matching DLL.
    // This is because FormatMessage API will detect key exists with and invalid DLL and NOT use any MTA file present
    RegistryKey reg = Registry.LocalMachine;
    RegistryKey? brokerKey = reg.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\EventLog\Application\IBM App Connect Enterprise v110011", false);
    if (brokerKey == null)
    {
      return true; // not present is valid for our needs
    }

    string? dll = brokerKey.GetValue("EventMessageFile") as string;
    if (!string.IsNullOrEmpty(dll))
    {
      if (File.Exists(dll))
      {
        return true;
      }
    }

    // key exists, but dll not found - this will cause errors in the test and needs fixing before running the test.
    // either delete/rename the key or make the EventMessageFile point to a valid dll
    throw new Exception(@"SETUP ERROR: 'HKLM\SYSTEM\CurrentControlSet\Services\EventLog\Application\IBM App Connect Enterprise v110011' must be valid or removed before running this test");
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
