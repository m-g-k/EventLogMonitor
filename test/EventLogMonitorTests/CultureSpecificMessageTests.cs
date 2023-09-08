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
using Moq;
using System.Diagnostics.Eventing.Reader;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;

namespace EventLogMonitor;

[Collection("EventLogMonitor")]
public class CultureSpecificMessageTests
{
  [SuppressMessage("Microsoft.Usage", "IDE0052:RemoveUnreadPrivateMember", MessageId = "stdoutput")]
  private readonly ITestOutputHelper stdoutput;
  private readonly string invalidMessageCatalogueDll;

  class VSSSampleEventLogLocationData : TheoryData<string,string>
  {
    public VSSSampleEventLogLocationData()
    {
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/VSS-Log.evtx", "The VSS service is shutting down due to idle timeout. %1 ");
      Add("../../../../../test/EventLogMonitorTests/SampleEventLogs_LocaleMetaData/VSS-Log.evtx", ""); // there is no DLL to use in this case
    }
  }

  public CultureSpecificMessageTests(ITestOutputHelper testOutputHelper)
  {
    stdoutput = testOutputHelper;
    // only exists in the dlls folder
    invalidMessageCatalogueDll = "../../../../../test/EventLogMonitorTests/SampleEventLogs_Dlls/InvalidMessageCatalogue.dll";
  }

  [Theory]
  [ClassData(typeof(VSSSampleEventLogLocationData))]
  public void MessageFromMessageCatalogueDllReturned(string vssSampleEventLogLocation, string expectedResult)
  {
    // test we can pull out a raw message from a message catalogue dll
    var mockEventRecord = new Mock<CultureSpecificMessage.IEventLogRecordWrapper>();
    mockEventRecord.Setup(x => x.Id).Returns(8224); // this must be a valid info message number
    mockEventRecord.Setup(x => x.LogName).Returns("Application");
    mockEventRecord.Setup(x => x.ProviderName).Returns("ORIGINALLY_FROM_VSS"); // must not be valid to prevent the LocaleMetaData version finding the provider
    mockEventRecord.Setup(x => x.ContainerLog).Returns(vssSampleEventLogLocation); // valid event log name name
    mockEventRecord.Setup(x => x.Qualifiers).Returns(0);
    mockEventRecord.Setup(x => x.Properties).Returns(new List<EventProperty>());
    int enUSCulture = 1033;
    string enUSCultureName = "En-US";

    string message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
    stdoutput.WriteLine(message);

    Assert.Equal(expectedResult, message);
  }

  [Fact]
  public void InvalidMessageCatalogueReturnsAnEmptyString()
  {
    // test an invalid message catalogue is ignored
    var mockEventRecord = new Mock<CultureSpecificMessage.IEventLogRecordWrapper>();
    mockEventRecord.Setup(x => x.Id).Returns(42);
    mockEventRecord.Setup(x => x.LogName).Returns("Applicationz");
    mockEventRecord.Setup(x => x.ProviderName).Returns("I Dont Exist");
    mockEventRecord.Setup(x => x.ContainerLog).Returns(invalidMessageCatalogueDll);
    mockEventRecord.Setup(x => x.Qualifiers).Returns(0);
    mockEventRecord.Setup(x => x.Properties).Returns(new List<EventProperty>());
    int enUSCulture = 1033;
    string enUSCultureName = "En-US";

    string message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
    stdoutput.WriteLine(message);

    Assert.Empty(message);
  }

}