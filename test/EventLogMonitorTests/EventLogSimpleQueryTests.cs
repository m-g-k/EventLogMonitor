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
using System.Diagnostics.CodeAnalysis;

namespace EventLogMonitor;

[Collection("EventLogMonitor")]
public class EventLogSimpleQueryTests
{
  [SuppressMessage("Microsoft.Usage", "IDE0052:RemoveUnreadPrivateMember", MessageId = "stdoutput")]
  private readonly ITestOutputHelper stdoutput;

  public EventLogSimpleQueryTests(ITestOutputHelper testOutputHelper)
  {
    stdoutput = testOutputHelper;
  }

  [Fact]
  public void EmptyStringReturnsMatchAllWildcard()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(*)]]", query);
  }

  [Fact]
  public void OnlySpacesReturnsMatchAllWildcard()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("    ");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(*)]]", query);
  }

  [Fact]
  public void SingleCommaNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator(","));
    Assert.Equal("Invalid Event ID filter: ','", exception.Message);
  }

  [Fact]
  public void SingleHyphenNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-"));
    Assert.Equal("Invalid exclusive filter '-'. The event ID must be specified", exception.Message);
  }

  [Fact]
  public void MultipleLeadingHyphenNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("----42"));
    Assert.Equal("Invalid exclusive range filter: '----42'", exception.Message);
  }

  [Fact]
  public void ExtraLeadingHyphenNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("--42"));
    Assert.Equal("Invalid exclusive range filter -''-'42'. Both parts of the range are required", exception.Message);
  }

  [Fact]
  public void MultipleInclusiveRangeHyphenNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("42--44"));
    Assert.Equal("Invalid inclusive range filter: '42--44'", exception.Message);
  }

  [Fact]
  public void MultipleSupresedRangeHyphenNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-42--44"));
    Assert.Equal("Invalid exclusive range filter: '-42--44'", exception.Message);
  }

  [Fact]
  public void MissingInclusiveRangeEndNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("42-"));
    Assert.Equal("Invalid inclusive range filter '42'-''. Both parts of the range are required", exception.Message);
  }

  [Fact]
  public void MissingExclusiveRangeEndNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-42-"));
    Assert.Equal("Invalid exclusive range filter -'42'-''. Both parts of the range are required", exception.Message);
  }

  [Fact]
  public void InclusiveRangeBeginGreaterThanEndNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("42-41"));
    Assert.Equal("Invalid inclusive range filter '42-41'. Begin must be < end", exception.Message);
  }

  [Fact]
  public void ExclusiveRangeBeginGreaterThanEndNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-42-40"));
    Assert.Equal("Invalid exclusive range filter '-42-40'. Begin must be < end", exception.Message);
  }

  [Fact]
  public void InclusiveRangeBeginEqualsEndNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("42-42"));
    Assert.Equal("Invalid inclusive range filter '42-42'. Begin must be < end", exception.Message);
  }

  [Fact]
  public void ExclusiveRangeBeginEqualsEndNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-41-41"));
    Assert.Equal("Invalid exclusive range filter '-41-41'. Begin must be < end", exception.Message);
  }

  [Fact]
  public void SingleCommaWithSpacesNotAllowed()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("   ,   "));
    Assert.Equal("Invalid Event ID filter: ','", exception.Message);
  }

  [Fact]
  public void SingleEventIdIncluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("42");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID = 42))]]", query);
  }

  [Fact]
  public void SingleBipMessagePrefixedEventIdIncluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("BIP42");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID = 42))]]", query);
  }

  [Fact]
  public void MultipleEventIdIncluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator(" 42 , 43 , 44 ");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID = 42 or EventID = 43 or EventID = 44))]]", query);
  }

  [Fact]
  public void SingleEventIdExcluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator(" -42");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID != 42))]]", query);
  }

  [Fact]
  public void MultipleEventIdExcluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator(" -42 , -43 , -44");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID != 42 and EventID != 43 and EventID != 44))]]", query);
  }

  [Fact]
  public void SingleEventIdIncludedAndExcludedThrowsException()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("32, -42"));
    Assert.Equal("Invalid Event ID filter. Cannot have included and excluded events in a filter without a range: '32, -42'", exception.Message);
  }

  [Fact]
  public void RangeEventIncludeWithRangeEventExcludeWithinTheRangeAndIncludesOutsideRangeIsAllowed()
  {
    //EventLog manual query for this: 0-999,-0,1003,-3,1025,-300-500 - 3026 events returned
    var queryGenerator = new EventLogSimpleQueryGenerator("0-999,-0,1003,-3,1025,-300-500");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(((EventID = 1003 or EventID = 1025) or (EventID >= 0 and EventID <= 999)) and ((EventID != 0 and EventID != 3) and (EventID < 300 or EventID > 500)))]]", query);
  }

  [Fact]
  public void MultipleEventIdIncludedAndExcludedThrowsException()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator(" 32, -42, 33, -44 "));
    Assert.Equal("Invalid Event ID filter. Cannot have included and excluded events in a filter without a range: '32, -42, 33, -44'", exception.Message);
  }

  [Fact]
  public void SingleEventIdRangeIncluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("42-49");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID >= 42 and EventID <= 49))]]", query);
  }

  [Fact]
  public void MultipleEventIdRangeIncluded()
  {
    // overlapping ranges don't make much sense but are allowed
    var queryGenerator = new EventLogSimpleQueryGenerator(" 42-48 , 43-80 , 44 - 50 ");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(((EventID >= 42 and EventID <= 48) or (EventID >= 43 and EventID <= 80) or (EventID >= 44 and EventID <= 50)))]]", query);
  }

  [Fact]
  public void SingleEventIdRangeExcluded()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("-42-49");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID < 42 or EventID > 49))]]", query);
  }

  [Fact]
  public void MultipleEventIdRangeExcluded()
  {
    // overlapping ranges don't make much sense but are allowed - it becomes the narrowest range of all
    var queryGenerator = new EventLogSimpleQueryGenerator(" -60-200 , -400-800 , -1025 - 1028 ");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(((EventID < 60 or EventID > 200) and (EventID < 400 or EventID > 800) and (EventID < 1025 or EventID > 1028)))]]", query);
  }

  [Fact]
  public void MultipleEventIdRangeExcludedWithSingleInclude()
  {
    // none overlapping ranges work fine
    var queryGenerator = new EventLogSimpleQueryGenerator(" -60-200 , -400-800 , 1026, -1025 - 1028, ");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((EventID = 1026) and ((EventID < 60 or EventID > 200) and (EventID < 400 or EventID > 800) and (EventID < 1025 or EventID > 1028)))]]", query);
  }

  [Fact]
  public void MixedRangeAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("1,2,5-99,-45,-15");

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(((EventID = 1 or EventID = 2) or (EventID >= 5 and EventID <= 99)) and (EventID != 45 and EventID != 15))]]", query);
  }

  [Fact]
  public void LogLevelForWarningsIs3OrBelow()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("", 3);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 3))]]", query);
  }

  [Fact]
  public void LogLevelForErrorsIs2OrBelow()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("", 2);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 2))]]", query);
  }

  [Fact]
  public void LogLevelForCriticalErrorsIs1()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("", 1);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 1))]]", query);
  }

  [Fact]
  public void LogLevelForAndIncludeIsAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("42", 3);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 3) and ((EventID = 42)))]]", query);
  }

  [Fact]
  public void LogLevelForAndExcludeIsAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("-1", 3);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 3) and ((EventID != 1)))]]", query);
  }

  [Fact]
  public void LogLevelForAndIncludeRangeIsAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("42-44", 3);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 3) and ((EventID >= 42 and EventID <= 44)))]]", query);
  }

  [Fact]
  public void LogLevelForAndExcludeRangeIsAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("-11-21", 3);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 3) and ((EventID < 11 or EventID > 21)))]]", query);
  }

  [Fact]
  public void LogLevelAndMixedFilterIsAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator("1,2,5-99,-45,-15", 3);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[((Level > 0 and Level <= 3) and (((EventID = 1 or EventID = 2) or (EventID >= 5 and EventID <= 99)) and (EventID != 45 and EventID != 15)))]]", query);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed1()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("a"));
    Assert.Equal("Invalid Event ID filter: 'a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed1a()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-a"));
    Assert.Equal("Invalid Event ID filter: '-a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed2()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("4a"));
    Assert.Equal("Invalid Event ID filter: '4a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed2a()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-4a"));
    Assert.Equal("Invalid Event ID filter: '-4a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed3()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("4-a"));
    Assert.Equal("Invalid inclusive range filter: '4-a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed3a()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-4-a"));
    Assert.Equal("Invalid exclusive range filter: '-4-a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed4()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("4-4a"));
    Assert.Equal("Invalid inclusive range filter: '4-4a'", exception.Message);
  }

  [Fact]
  public void InvalidCharactersAreNotAllowed4a()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EventLogSimpleQueryGenerator("-4-4a"));
    Assert.Equal("Invalid exclusive range filter: '-4-4a'", exception.Message);
  }

  [Fact]
  public void MixOfMultipleRangesAndSingleIDsAreAllowed()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator(" 5-99 , -2-7, -45 , -15, 1, 2 ", -1);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(((EventID = 1 or EventID = 2) or (EventID >= 5 and EventID <= 99)) and ((EventID != 45 and EventID != 15) and (EventID < 2 or EventID > 7)))]]", query);
  }

  [Fact]
  public void SingleAndRange()
  {
    var queryGenerator = new EventLogSimpleQueryGenerator(" 44,45,46,60-70 ", -1);

    string query = queryGenerator.QueryString;
    Assert.True(SanityCheckBraces(query));

    Assert.Equal("*[System[(((EventID = 44 or EventID = 45 or EventID = 46) or (EventID >= 60 and EventID <= 70)))]]", query);
  }

  static bool SanityCheckBraces(string toCheck)
  {
    int squareOpen = 0;
    int squareClose = 0;
    int square = 0;
    int roundOpen = 0;
    int roundClose = 0;
    int round = 0;
    foreach (char item in toCheck)
    {
      switch (item)
      {
        case '[':
          {
            ++squareOpen;
            ++square;
            break;
          }
        case ']':
          {
            ++squareClose;
            --square;
            break;
          }
        case '(':
          {
            ++roundOpen;
            ++round;
            break;
          }
        case ')':
          {
            ++roundClose;
            --round;
            break;
          }
      }
    }

    if (round > 0 || square > 0)
    {
      Console.WriteLine($"Query is not valid, Round: {round}, Square: {square}. Mismatch brackets.");
      Console.WriteLine($" : RoundOpen: {roundOpen}, RoundClose: {roundClose}.");
      Console.WriteLine($" : SquareOpen: {squareOpen}, SquareClose: {squareClose}.");
      return false;
    }
    return true;
  }

}