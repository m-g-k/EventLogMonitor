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
  private static readonly string[] separator = ["\n", "\r"];

  class VSSSampleEventLogLocationData : TheoryData<string, string>
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
    mockEventRecord.Setup(x => x.Properties).Returns(new List<string>());
    int enUSCulture = 1033;
    string enUSCultureName = "En-US";

    var message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
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
    mockEventRecord.Setup(x => x.Properties).Returns(new List<string>());
    int enUSCulture = 1033;
    string enUSCultureName = "En-US";

    var message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
    stdoutput.WriteLine(message);

    Assert.Empty(message);
  }

  [Fact]
  public void PatchedProviderWithNoMessageCatalogueWithInsertsReturnsFormattedString()
  {
    //arrange
    // test a special cases entry returns its inserts only
    var mockEventRecord = new Mock<CultureSpecificMessage.IEventLogRecordWrapper>();
    mockEventRecord.Setup(x => x.Id).Returns(42);
    mockEventRecord.Setup(x => x.LogName).Returns("Applicationz");
    mockEventRecord.Setup(x => x.ProviderName).Returns("MissingProvider");
    mockEventRecord.Setup(x => x.ContainerLog).Returns("NotFound.dll");
    mockEventRecord.Setup(x => x.Qualifiers).Returns(0);

    List<string> insertList = ["insert1", "insert2", "insert3"];
    mockEventRecord.Setup(x => x.Properties).Returns(insertList);

    int enUSCulture = 1033;
    string enUSCultureName = "En-US";

    // act
    // make sure GetCultureSpecificMessage returns empty string for a missing provider
    var message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
    var patchedMessage = CultureSpecificMessage.GetPatchedMessageFromFormatString(mockEventRecord.Object);
    stdoutput.WriteLine(patchedMessage);
    string[] lines = patchedMessage.Split(separator, StringSplitOptions.RemoveEmptyEntries);

    // assert
    Assert.Empty(message);
    Assert.NotEmpty(patchedMessage);
    Assert.Equal(3, lines.Length);
    Assert.Equal("insert1.", lines[0]);
    Assert.Equal("insert2.", lines[1]);
    Assert.Equal("insert3.", lines[2]);
  }

  [Fact]
  public void MissingProvidersAreSpecialCasedPatchedMessageReturnsInsertsOnlyResult()
  {
    //arrange
    var mockEventRecord = new Mock<CultureSpecificMessage.IEventLogRecordWrapper>();
    mockEventRecord.Setup(x => x.Id).Returns(42);
    mockEventRecord.Setup(x => x.LogName).Returns("Applicationz");
    mockEventRecord.Setup(x => x.ProviderName).Returns("MissingProvider");
    mockEventRecord.Setup(x => x.ContainerLog).Returns("NotFound.dll");
    mockEventRecord.Setup(x => x.Qualifiers).Returns(0);

    List<string> insertList = ["insert11", "insert22", "insert33"];
    mockEventRecord.Setup(x => x.Properties).Returns(insertList);

    int enUSCulture = 1033;
    string enUSCultureName = "En-US";

    // act

    // make sure GetCultureSpecificMessage returns empty string for a missing provider
    var message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
    var patchedMessage = CultureSpecificMessage.GetPatchedMessageFromFormatString(mockEventRecord.Object);
    stdoutput.WriteLine(patchedMessage);
    string[] lines = patchedMessage.Split(separator, StringSplitOptions.RemoveEmptyEntries);

    // assert
    Assert.Empty(message);
    Assert.NotEmpty(patchedMessage);
    Assert.Equal(3, lines.Length);
    Assert.Equal("insert11.", lines[0]);
    Assert.Equal("insert22.", lines[1]);
    Assert.Equal("insert33.", lines[2]);
  }

  [Fact]
  public void SpecialCasedEntriesListCanBeClearedAndEmptyStringReturned()
  {
    //arrange
    var specialCasingProvidersFlag = CultureSpecificMessage.SpecialCaseMissingProviders;
    try
    {
      CultureSpecificMessage.SpecialCaseMissingProviders = false; // disable special casing

      var mockEventRecord = new Mock<CultureSpecificMessage.IEventLogRecordWrapper>();
      mockEventRecord.Setup(x => x.Id).Returns(42);
      mockEventRecord.Setup(x => x.LogName).Returns("Applicationz");
      mockEventRecord.Setup(x => x.ProviderName).Returns("MissingProviders");
      mockEventRecord.Setup(x => x.ContainerLog).Returns("NotFound.dll");
      mockEventRecord.Setup(x => x.Qualifiers).Returns(0);

      List<string> insertList = ["insert11", "insert22", "insert33"];
      mockEventRecord.Setup(x => x.Properties).Returns(insertList);

      int enUSCulture = 1033;
      string enUSCultureName = "En-US";

      // act
      var message = CultureSpecificMessage.GetCultureSpecificMessage(mockEventRecord.Object, enUSCulture, enUSCultureName);
      stdoutput.WriteLine(message);
      string[] lines = message.Split(separator, StringSplitOptions.RemoveEmptyEntries);

      // assert
      Assert.Empty(lines);
    }
    finally
    {
      // reset the default
      CultureSpecificMessage.SpecialCaseMissingProviders = specialCasingProvidersFlag;
    }
  }

}