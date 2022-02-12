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

namespace EventLogMonitor;

[Collection("EventLogMonitor")]
public class BinaryDataFormatterTests
{
  [SuppressMessage("Microsoft.Usage", "IDE0052:RemoveUnreadPrivateMember", MessageId = "stdoutput")]
  private readonly ITestOutputHelper stdoutput;
  public BinaryDataFormatterTests(ITestOutputHelper testOutputHelper)
  {
    stdoutput = testOutputHelper;
  }

  [Fact]
  public void ZeroBytesPasedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = Array.Empty<byte>();
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 0);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.False(ret);
    Assert.Single(lines);
    Assert.Equal("<Entry has no binary data>. Index: 0", lines[0]);
  }

  [Fact]
  public void NullPasedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(null, 0);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.False(ret);
    Assert.Single(lines);
    Assert.Equal("<Entry has no binary data>. Index: 0", lines[0]);
  }

  [Fact]
  public void ZeroBytesPasedInIsAllowedAsString()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = Array.Empty<byte>();
    bool ret = BinaryDataFormatter.OutputFormattedBinaryDataAsString(input, 0);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.False(ret);
    Assert.Single(lines);
    Assert.Equal("<Entry has no binary data>. Index: 0", lines[0]);
  }

  [Fact]
  public void NullPasedInIsAllowedAsString()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    bool ret = BinaryDataFormatter.OutputFormattedBinaryDataAsString(null, 0);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.False(ret);
    Assert.Single(lines);
    Assert.Equal("<Entry has no binary data>. Index: 0", lines[0]);
  }

  [Fact]
  public void ControlCharIsSeenAsBinaryForString()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x10 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryDataAsString(input, 1);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Single(lines);
    Assert.Equal("<Entry has binary data - use '-b2' to view>. Index: 1", lines[0]);
  }

  [Fact]
  public void InvalidUnicodeIsHandledForString()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0xCD, 0xDF }; // invalid unicode sequence which forces an exception in the convertor
    bool ret = BinaryDataFormatter.OutputFormattedBinaryDataAsString(input, 1);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Single(lines);
    Assert.Equal("<Entry has binary data - use '-b2' to view>. Index: 1", lines[0]);
  }

  [Fact]
  public void OneBytePassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 1);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 1", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000001: 41                      A", lines[2]);
    Assert.Equal("Index: 1", lines[3]);
  }

  [Fact]
  public void TwoBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 2);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 2", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000002: 41 42                   AB", lines[2]);
    Assert.Equal("Index: 2", lines[3]);
  }

  [Fact]
  public void ThreeBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42, 0x43 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 3);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 3", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000003: 41 42 43                ABC", lines[2]);
    Assert.Equal("Index: 3", lines[3]);
  }

  [Fact]
  public void FourBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42, 0x43, 0x44 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 4);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 4", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000004: 41 42 43 44             ABCD     44434241", lines[2]);
    Assert.Equal("Index: 4", lines[3]);
  }

  [Fact]
  public void FiveBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 5);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 5", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000005: 41 42 43 44-45          ABCDE    44434241", lines[2]);
    Assert.Equal("Index: 5", lines[3]);
  }

  [Fact]
  public void SixBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 6);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 6", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000006: 41 42 43 44-45 46       ABCDEF   44434241", lines[2]);
    Assert.Equal("Index: 6", lines[3]);
  }

  [Fact]
  public void SevenBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 7);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 7", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000007: 41 42 43 44-45 46 47    ABCDEFG  44434241", lines[2]);
    Assert.Equal("Index: 7", lines[3]);
  }

  [Fact]
  public void EightBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 8);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 8", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000008: 41 42 43 44-45 46 47 48 ABCDEFGH 44434241 48474645", lines[2]);
    Assert.Equal("Index: 8", lines[3]);
  }

  // test below are with unprintable characters

  [Fact]
  public void OneUnprintableBytePassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x00 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 1);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 1", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000001: 00                      .", lines[2]);
    Assert.Equal("Index: 1", lines[3]);
  }

  [Fact]
  public void TwoUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x01, 0x02 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 2);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 2", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000002: 01 02                   ..", lines[2]);
    Assert.Equal("Index: 2", lines[3]);
  }

  [Fact]
  public void ThreeUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x03, 0x04, 0x05 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 3);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 3", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000003: 03 04 05                ...", lines[2]);
    Assert.Equal("Index: 3", lines[3]);
  }

  [Fact]
  public void FourUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x06, 0x07, 0x08, 0x09 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 4);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 4", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000004: 06 07 08 09             ....     09080706", lines[2]);
    Assert.Equal("Index: 4", lines[3]);
  }

  [Fact]
  public void FiveUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x10, 0x11, 0x12, 0x13, 0x14 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 5);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 5", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000005: 10 11 12 13-14          .....    13121110", lines[2]);
    Assert.Equal("Index: 5", lines[3]);
  }

  [Fact]
  public void SixUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x15, 0x16, 0x17, 0x18, 0x19, 0x80 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 6);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 6", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000006: 15 16 17 18-19 80       ......   18171615", lines[2]);
    Assert.Equal("Index: 6", lines[3]);
  }

  [Fact]
  public void SevenUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 7);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 7", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000007: 81 82 83 84-85 86 87    .......  84838281", lines[2]);
    Assert.Equal("Index: 7", lines[3]);
  }

  [Fact]
  public void EightUnprintableBytesPassedInIsAllowed()
  {
    // replace stdout to capture it
    var output = new StringWriter();
    Console.SetOut(output);

    byte[] input = new byte[] { 0x88, 0x89, 0x90, 0x91, 0x92, 0x93, 0x94, 0x95 };
    bool ret = BinaryDataFormatter.OutputFormattedBinaryData(input, 8);

    string logOut = output.ToString();
    stdoutput.WriteLine(logOut);
    string[] lines = logOut.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert.True(ret);
    Assert.Equal(4, lines.Length);
    Assert.Equal("Binary Data size: 8", lines[0]);
    Assert.Equal("Count   : 00 01 02 03-04 05 06 07  ASCII         00       04", lines[1]);
    Assert.Equal("00000008: 88 89 90 91-92 93 94 95 ........ 91908988 95949392", lines[2]);
    Assert.Equal("Index: 8", lines[3]);
  }

}