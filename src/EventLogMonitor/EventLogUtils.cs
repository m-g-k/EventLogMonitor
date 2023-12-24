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

public class EventLogUtils
{

  public static string RemoveChars(string source, ReadOnlySpan<char> sourceRange, string separatorToRemove, int countToRemove)
  {
    Span<Range> parts = stackalloc Range[countToRemove + 1]; // always 1 more range entry than items to remove 
    int count = sourceRange.Split(parts, separatorToRemove, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    StringBuilder result = new(source.Length);

    for (int index = 0; index < count; ++index)
    {
      var part = parts[index];
      result.Append(source[part]);

      // only if there are more parts to come
      if (index + 1 < count)
      {
        // See if the break is immediately before or after a '.'
        // and just remove it if so, else replace with a prettier break.
        // Note: we can't be at the beginning or end as the trim would have already removed it.
        bool currentEndsInAPeriod = source[parts[index].End.Value - 1] == '.';
        bool nextStartsWithAPeriod = source[parts[index + 1].Start.Value] == '.';

        if (!currentEndsInAPeriod && !nextStartsWithAPeriod)
        {
          result.Append(". ");
        }
        else if (!nextStartsWithAPeriod)
        {
          result.Append(' ');
        }
      }
    }
    return result.ToString();
  }

}