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
public class EventLogUtilsTests
{
  //[SuppressMessage("Microsoft.Usage", "IDE0052:RemoveUnreadPrivateMember", MessageId = "stdoutput")]
  private readonly ITestOutputHelper stdoutput;

  public EventLogUtilsTests(ITestOutputHelper testOutputHelper)
  {
    stdoutput = testOutputHelper;
  }

  [Theory]
  [InlineData(01, ".ONE1 \r\n.TWO2.", ".ONE1.TWO2.")]
  [InlineData(02, ".ONE1 \r\n TWO2.", ".ONE1. TWO2.")]
  [InlineData(03, ".ONE1 \r\n TWO2 ", ".ONE1. TWO2")]
  [InlineData(04, ".ONE1.\r\n.TWO2.", ".ONE1..TWO2.")]
  [InlineData(05, ".ONE1.\r\n.TWO2 ", ".ONE1..TWO2")]
  [InlineData(06, ".ONE1.\r\n TWO2 ", ".ONE1. TWO2")]
  [InlineData(07, ".ONE1.\r\n TWO2.", ".ONE1. TWO2.")]
  [InlineData(08, ".ONE1 \r\n.TWO2 ", ".ONE1.TWO2")]
  [InlineData(09, " ONE1.\r\n.TWO2.", "ONE1..TWO2.")]
  [InlineData(10, " ONE1.\r\n.TWO2 ", "ONE1..TWO2")]
  [InlineData(11, " ONE1 \r\n.TWO2.", "ONE1.TWO2.")]
  [InlineData(12, " ONE1 \r\n.TWO2 ", "ONE1.TWO2")]
  [InlineData(13, " ONE1 \r\n TWO2.", "ONE1. TWO2.")]
  [InlineData(14, " ONE1.\r\n TWO2.", "ONE1. TWO2.")]
  [InlineData(15, " ONE1.\r\n TWO2 ", "ONE1. TWO2")]
  [InlineData(16, " ONE1 \r\n TWO2 ", "ONE1. TWO2")]
  public void MessageFromMessageCatalogueDllReturned(int testNumber, string testMessageString, string expectedResult)
  {
    ReadOnlySpan<char> messageSpan = testMessageString;
    string separator = "\r\n";
    int count = messageSpan.Count(separator);

    var message = EventLogUtils.RemoveChars(testMessageString, messageSpan, separator, count);
    Assert.Equal(expectedResult, message);
    Assert.Equal(testNumber, testNumber); //force usage of variable
  }

}