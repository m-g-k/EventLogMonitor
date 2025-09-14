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
using System.Text;

namespace EventLogMonitor;

public static class BinaryDataFormatter
{
  // convertor that throws on errors  
  readonly private static UnicodeEncoding iConvertor = new(false, false, true);
  readonly private static Char[] iTrimChars = [' ', '\n', '\t', '\r'];
  readonly private static string[] formatStrings =
  [
    "{0:D8}: {1:X2}                      {2}\n", // 1 byte
    "{0:D8}: {1:X2} {2:X2}                   {3}{4}\n", // 2 bytes
    "{0:D8}: {1:X2} {2:X2} {3:X2}                {4}{5}{6}\n", // 3 bytes
    "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}             {5}{6}{7}{8}     {9:X2}{10:X2}{11:X2}{12:X2}\n", // 4 bytes
    "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2}          {6}{7}{8}{9}{10}    {11:X2}{12:X2}{13:X2}{14:X2}\n", // 5 bytes
    "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2} {6:x2}       {7}{8}{9}{10}{11}{12}   {13:X2}{14:X2}{15:X2}{16:X2}\n", // 6 bytes
    "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2} {6:x2} {7:x2}    {8}{9}{10}{11}{12}{13}{14}  {15:X2}{16:X2}{17:X2}{18:X2}\n", // 7 bytes
    "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2} {6:x2} {7:x2} {8:x2} {9}{10}{11}{12}{13}{14}{15}{16} {17:X2}{18:X2}{19:X2}{20:X2} {21:X2}{22:X2}{23:X2}{24:X2}\n", // 8 bytes
  ];

  public static bool OutputFormattedBinaryDataAsString(byte[] data, long index)
  {
    if (data == null || data.Length == 0)
    {
      return OutputNoDataError(index);
    }

    // quick and dirty test for ascii only string data or possible binary only data.
    bool isAscii = true;
    bool isBinary = false;
    foreach (byte x in data)
    {
      if (x is < 0x20 or > 0x7F)
      {
        if (x is < 0x20 and not 0x0)
        {
          // if we are probably binary we can leave early
          isBinary = true;
          isAscii = false;
          break;
        }
        // we can't break here as we could find a NULL or a char >0x7F
        // before a byte <0x20 so we keep going.
        isAscii = false;
      }
    }

    string message;
    if (isAscii)
    {
      message = Encoding.ASCII.GetString(data);
    }
    else if (isBinary)
    {
      message = "<Entry has binary data - use '-b2' to view>";
    }
    else
    {
      // The default static Unicode object is not configured to throw on invalid
      // unicode characters so we use our own.
      try
      {
        message = iConvertor.GetString(data);
      }
      catch (ArgumentException)
      {
        // not valid unicode
        message = "<Entry has binary data - use '-b2' to view>";
      }
    }

    message = message.Replace('\0', ' '); // strip embedded nulls
    message = message.TrimEnd(iTrimChars); // remove junk
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write(message + ". Index: " + index + "\n");
    Console.ResetColor();

    return true;
  }

  public static bool OutputFormattedBinaryData(byte[] data, long index)
  {
    if (data == null || data.Length == 0)
    {
      return OutputNoDataError(index);
    }

    int currentOffset = 0; // current offset into data
    int dataSize = data.Length;
    int lineCount = dataSize / 8;
    int lineFraction = dataSize % 8;

    StringBuilder buffer = new();

    Console.Write("Binary Data size: {0}\n" +
            "Count   : 00 01 02 03-04 05 06 07  ASCII         00       04\n", dataSize);

    // first print whole lines
    var formatString = formatStrings[7];
    for (int count = 0; count < lineCount; ++count)
    {
      currentOffset = count * 8;
      buffer.AppendFormat(
        formatString,
        currentOffset + 8,
        data[currentOffset + 0],
        data[currentOffset + 1],
        data[currentOffset + 2],
        data[currentOffset + 3],
        data[currentOffset + 4],
        data[currentOffset + 5],
        data[currentOffset + 6],
        data[currentOffset + 7],
        GetPrintableChar(data[currentOffset + 0]),
        GetPrintableChar(data[currentOffset + 1]),
        GetPrintableChar(data[currentOffset + 2]),
        GetPrintableChar(data[currentOffset + 3]),
        GetPrintableChar(data[currentOffset + 4]),
        GetPrintableChar(data[currentOffset + 5]),
        GetPrintableChar(data[currentOffset + 6]),
        GetPrintableChar(data[currentOffset + 7]),
        data[currentOffset + 3],
        data[currentOffset + 2],
        data[currentOffset + 1],
        data[currentOffset + 0],
        data[currentOffset + 7],
        data[currentOffset + 6],
        data[currentOffset + 5],
        data[currentOffset + 4]);
    }

    // now do any fractions
    if (lineFraction > 0)
    {
      currentOffset = lineCount * 8; // current offset into data
      OutputFormattedBinaryDataFraction(data, lineFraction, currentOffset, buffer);
    }

    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write(buffer.ToString());
    Console.Write("Index: " + index + "\n");
    Console.ResetColor();
    return true;
  }

  private static void OutputFormattedBinaryDataFraction(byte[] data, int lineFraction, int currentOffset, StringBuilder buffer)
  {
    string formatString = formatStrings[lineFraction - 1];
    switch (lineFraction)
    {
      case 7: // 7 bytes to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 7,
          data[currentOffset + 0], // hex
          data[currentOffset + 1],
          data[currentOffset + 2],
          data[currentOffset + 3],
          data[currentOffset + 4],
          data[currentOffset + 5],
          data[currentOffset + 6],
          GetPrintableChar(data[currentOffset + 0]), // ascii
          GetPrintableChar(data[currentOffset + 1]),
          GetPrintableChar(data[currentOffset + 2]),
          GetPrintableChar(data[currentOffset + 3]),
          GetPrintableChar(data[currentOffset + 4]),
          GetPrintableChar(data[currentOffset + 5]),
          GetPrintableChar(data[currentOffset + 6]),
          data[currentOffset + 3], // word
          data[currentOffset + 2],
          data[currentOffset + 1],
          data[currentOffset + 0]);
        break;
      case 6: // 6 bytes to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 6,
          data[currentOffset + 0],
          data[currentOffset + 1],
          data[currentOffset + 2],
          data[currentOffset + 3],
          data[currentOffset + 4],
          data[currentOffset + 5],
          GetPrintableChar(data[currentOffset + 0]),
          GetPrintableChar(data[currentOffset + 1]),
          GetPrintableChar(data[currentOffset + 2]),
          GetPrintableChar(data[currentOffset + 3]),
          GetPrintableChar(data[currentOffset + 4]),
          GetPrintableChar(data[currentOffset + 5]),
          data[currentOffset + 3],
          data[currentOffset + 2],
          data[currentOffset + 1],
          data[currentOffset + 0]);
        break;
      case 5: // 5 bytes to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 5,
          data[currentOffset + 0],
          data[currentOffset + 1],
          data[currentOffset + 2],
          data[currentOffset + 3],
          data[currentOffset + 4],
          GetPrintableChar(data[currentOffset + 0]),
          GetPrintableChar(data[currentOffset + 1]),
          GetPrintableChar(data[currentOffset + 2]),
          GetPrintableChar(data[currentOffset + 3]),
          GetPrintableChar(data[currentOffset + 4]),
          data[currentOffset + 3],
          data[currentOffset + 2],
          data[currentOffset + 1],
          data[currentOffset + 0]);
        break;
      case 4: // 4 bytes to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 4,
          data[currentOffset + 0],
          data[currentOffset + 1],
          data[currentOffset + 2],
          data[currentOffset + 3],
          GetPrintableChar(data[currentOffset + 0]),
          GetPrintableChar(data[currentOffset + 1]),
          GetPrintableChar(data[currentOffset + 2]),
          GetPrintableChar(data[currentOffset + 3]),
          data[currentOffset + 3],
          data[currentOffset + 2],
          data[currentOffset + 1],
          data[currentOffset + 0]);
        break;
      case 3: // 3 bytes to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 3,
          data[currentOffset + 0],
          data[currentOffset + 1],
          data[currentOffset + 2],
          GetPrintableChar(data[currentOffset + 0]),
          GetPrintableChar(data[currentOffset + 1]),
          GetPrintableChar(data[currentOffset + 2]));
        break;
      case 2: // 2 bytes to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 2,
          data[currentOffset + 0],
          data[currentOffset + 1],
          GetPrintableChar(data[currentOffset + 0]),
          GetPrintableChar(data[currentOffset + 1]));
        break;
      case 1: // 1 byte to output
        buffer.AppendFormat(
          formatString,
          currentOffset + 1,
          data[currentOffset + 0],
          GetPrintableChar(data[currentOffset + 0]));
        break;
    }
  }

  private static char GetPrintableChar(byte candidate)
  {
    return candidate is not (< 0x20 or > 0x7F) ? (char)candidate : '.';
  }

  // private static bool IsPrintable(byte candidate)
  // {
  //  return candidate is not (< 0x20 or > 0x7F);
  // }

  private static bool OutputNoDataError(long index)
  {
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write("<Entry has no binary data>. Index: " + index + "\n");
    Console.ResetColor();
    return false;
  }

}