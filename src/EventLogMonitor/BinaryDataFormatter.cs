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
  readonly private static Char[] iTrimChars = new Char[] { ' ', '\n', '\t', '\r' };

  public static bool OutputFormattedBinaryDataAsString(byte[] data, long index)
  {
    if (data == null || data.Length == 0)
    {
      return OutputNoDataError(index);
    }

    // quick and dirty test for ascii only string data or possible binary only data
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
    Console.WriteLine(message + ". Index: " + index);
    Console.ResetColor();

    return true;
  }

  public static bool OutputFormattedBinaryData(byte[] data, long index)
  {
    if (data == null || data.Length == 0)
    {
      return OutputNoDataError(index);
    }

    int counter1;
    int counter2;
    int dataSize = data.Length;
    int lineCount = dataSize / 8;
    int lineFraction = dataSize % 8;

    StringBuilder buffer = new();

    Console.Write("Binary Data size: {0}\n" +
            "Count   : 00 01 02 03-04 05 06 07  ASCII         00       04\n", dataSize);

    // first print whole lines
    for (counter1 = 0; counter1 < lineCount; ++counter1)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2} {6:x2} {7:x2} {8:x2} {9}{10}{11}{12}{13}{14}{15}{16} {17:X2}{18:X2}{19:X2}{20:X2} {21:X2}{22:X2}{23:X2}{24:X2}\n",
        counter2 + 8,
        data[counter2 + 0],
        data[counter2 + 1],
        data[counter2 + 2],
        data[counter2 + 3],
        data[counter2 + 4],
        data[counter2 + 5],
        data[counter2 + 6],
        data[counter2 + 7],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.',
        IsPrintable(data[counter2 + 2]) ? (char)data[counter2 + 2] : '.',
        IsPrintable(data[counter2 + 3]) ? (char)data[counter2 + 3] : '.',
        IsPrintable(data[counter2 + 4]) ? (char)data[counter2 + 4] : '.',
        IsPrintable(data[counter2 + 5]) ? (char)data[counter2 + 5] : '.',
        IsPrintable(data[counter2 + 6]) ? (char)data[counter2 + 6] : '.',
        IsPrintable(data[counter2 + 7]) ? (char)data[counter2 + 7] : '.',
        data[counter2 + 3],
        data[counter2 + 2],
        data[counter2 + 1],
        data[counter2 + 0],
        data[counter2 + 7],
        data[counter2 + 6],
        data[counter2 + 5],
        data[counter2 + 4]);
    }

    // now do any fractions
    if (lineFraction == 7)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2} {6:x2} {7:x2}    {8}{9}{10}{11}{12}{13}{14}  {15:X2}{16:X2}{17:X2}{18:X2}\n",
        counter2 + 7,
        data[counter2 + 0],
        data[counter2 + 1],
        data[counter2 + 2],
        data[counter2 + 3],
        data[counter2 + 4],
        data[counter2 + 5],
        data[counter2 + 6],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.',
        IsPrintable(data[counter2 + 2]) ? (char)data[counter2 + 2] : '.',
        IsPrintable(data[counter2 + 3]) ? (char)data[counter2 + 3] : '.',
        IsPrintable(data[counter2 + 4]) ? (char)data[counter2 + 4] : '.',
        IsPrintable(data[counter2 + 5]) ? (char)data[counter2 + 5] : '.',
        IsPrintable(data[counter2 + 6]) ? (char)data[counter2 + 6] : '.',
        data[counter2 + 3],
        data[counter2 + 2],
        data[counter2 + 1],
        data[counter2 + 0]);
    }
    else if (lineFraction == 6)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2} {6:x2}       {7}{8}{9}{10}{11}{12}   {13:X2}{14:X2}{15:X2}{16:X2}\n",
        counter2 + 6,
        data[counter2 + 0],
        data[counter2 + 1],
        data[counter2 + 2],
        data[counter2 + 3],
        data[counter2 + 4],
        data[counter2 + 5],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.',
        IsPrintable(data[counter2 + 2]) ? (char)data[counter2 + 2] : '.',
        IsPrintable(data[counter2 + 3]) ? (char)data[counter2 + 3] : '.',
        IsPrintable(data[counter2 + 4]) ? (char)data[counter2 + 4] : '.',
        IsPrintable(data[counter2 + 5]) ? (char)data[counter2 + 5] : '.',
        data[counter2 + 3],
        data[counter2 + 2],
        data[counter2 + 1],
        data[counter2 + 0]);
    }
    else if (lineFraction == 5)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}-{5:x2}          {6}{7}{8}{9}{10}    {11:X2}{12:X2}{13:X2}{14:X2}\n",
        counter2 + 5,
        data[counter2 + 0],
        data[counter2 + 1],
        data[counter2 + 2],
        data[counter2 + 3],
        data[counter2 + 4],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.',
        IsPrintable(data[counter2 + 2]) ? (char)data[counter2 + 2] : '.',
        IsPrintable(data[counter2 + 3]) ? (char)data[counter2 + 3] : '.',
        IsPrintable(data[counter2 + 4]) ? (char)data[counter2 + 4] : '.',
        data[counter2 + 3],
        data[counter2 + 2],
        data[counter2 + 1],
        data[counter2 + 0]);
    }
    else if (lineFraction == 4)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2} {3:X2} {4:x2}             {5}{6}{7}{8}     {9:X2}{10:X2}{11:X2}{12:X2}\n",
        counter2 + 4,
        data[counter2 + 0],
        data[counter2 + 1],
        data[counter2 + 2],
        data[counter2 + 3],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.',
        IsPrintable(data[counter2 + 2]) ? (char)data[counter2 + 2] : '.',
        IsPrintable(data[counter2 + 3]) ? (char)data[counter2 + 3] : '.',
        data[counter2 + 3],
        data[counter2 + 2],
        data[counter2 + 1],
        data[counter2 + 0]);
    }
    else if (lineFraction == 3)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2} {3:X2}                {4}{5}{6}\n",
        counter2 + 3,
        data[counter2 + 0],
        data[counter2 + 1],
        data[counter2 + 2],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.',
        IsPrintable(data[counter2 + 2]) ? (char)data[counter2 + 2] : '.');
    }
    else if (lineFraction == 2)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
        "{0:D8}: {1:X2} {2:X2}                   {3}{4}\n",
        counter2 + 2,
        data[counter2 + 0],
        data[counter2 + 1],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.',
        IsPrintable(data[counter2 + 1]) ? (char)data[counter2 + 1] : '.');
    }
    else if (lineFraction == 1)
    {
      counter2 = counter1 * 8;
      buffer.AppendFormat(
       "{0:D8}: {1:X2}                      {2}\n",
        counter2 + 1,
        data[counter2 + 0],
        IsPrintable(data[counter2 + 0]) ? (char)data[counter2 + 0] : '.');
    }
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write(buffer.ToString());
    Console.WriteLine("Index: " + index);
    Console.ResetColor();
    return true;
  }

  private static bool IsPrintable(byte candidate)
  {
    return candidate is not (< 0x20 or > 0x7F);
  }

  private static bool OutputNoDataError(long index)
  {
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("<Entry has no binary data>. Index: " + index);
    Console.ResetColor();
    return false;
  }

}