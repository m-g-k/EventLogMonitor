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
using System.Collections.Generic;
using System.Text;

namespace EventLogMonitor;

public class EventLogSimpleQueryGenerator
{

  readonly private int iLogLevel = -1;
  readonly private string iRawEventIds;
  readonly private List<string> iIncludedEventIds = new();
  readonly private List<string> iExcludedEventIds = new();
  readonly private Dictionary<string, string> iIncludedEventRanges = new();
  readonly private Dictionary<string, string> iExcludedEventRanges = new();
  private bool iHasIncludedEvents = false;
  private bool iHasExcludedEvents = false;
  private bool iHasIncludedRangeEvents = false;
  private bool iHasExcludedRangeEvents = false;
  public EventLogSimpleQueryGenerator(string eventIds, int logLevel = -1)
  {
    iLogLevel = logLevel;
    iRawEventIds = eventIds;

    ParseRawEvents();
    ValidateEvents();
    GenerateQueryString();
  }

  private void ValidateEvents()
  {
    iHasIncludedEvents = (iIncludedEventIds.Count > 0);
    iHasExcludedEvents = (iExcludedEventIds.Count > 0);
    iHasIncludedRangeEvents = (iIncludedEventRanges.Count > 0);
    iHasExcludedRangeEvents = (iExcludedEventRanges.Count > 0);

    // if we have included and excluded events they must be in an included range
    // we could check for overlap but leave it for now at technically the built in
    // event viewer allows this condition even though it makes no sense!
    if (iHasIncludedEvents && !iHasIncludedRangeEvents)
    {
      if (iHasExcludedEvents && !iHasIncludedRangeEvents)
      {
        throw new ArgumentException($"Invalid Event ID filter. Cannot have included and excluded events in a filter without a range: '{iRawEventIds.Trim()}'");
      }
    }

    // todo, add more conditions here

  }

  public string QueryString { get; private set; }

  private void GenerateQueryString()
  {
    string includedEvents = GenerateIncludedEvents();
    string excludedEvents = GenerateExcludedEvents(); ;
    string includedEventRanges = GenerateIncludedEventRanges();
    string excludedEventRanges = GenerateExcludedEventRanges();

    // we need to follow this simple rule when building the query string:
    // (LogLevel AND ( ((IncludedEvents) OR (IncludedRanges)) AND ((ExcludedEvents) AND (ExcludedRanges)) ) )

    // head
    string queryString = "*[System[(";
    bool first = true;
    bool logLevelPresent = false;
    bool includeExtraClosingBrace = false;

    // LogLevel
    if (iLogLevel != -1)
    {
      // LogAlways = 0,
      // Critical = 1,
      // Error = 2,
      // Warning = 3,
      // Informational = 4,
      // Verbose = 5
      // Customer events = > 5

      queryString += $"(Level > 0 and Level <= {this.iLogLevel})";
      logLevelPresent = true;

      if (iHasIncludedEvents || iHasExcludedEvents || iHasIncludedRangeEvents || iHasExcludedRangeEvents)
      {
        // LogLevel must "AND" with all following events
        queryString += " and (";
        includeExtraClosingBrace = true;
      }
    }

    // included events
    if (!string.IsNullOrEmpty(includedEvents))
    {
      if (iHasIncludedRangeEvents)
      {
        queryString += "(";
      }

      queryString += includedEvents;
      first = false;
    }

    // included range events
    if (!string.IsNullOrEmpty(includedEventRanges))
    {
      if (!first)
      {
        queryString += " or ";
      }
      queryString += includedEventRanges;

      if (iHasIncludedEvents)
      {
        queryString += ")";
      }

      first = false;
    }

    // excluded events
    if (!string.IsNullOrEmpty(excludedEvents))
    {
      if (!first)
      {
        queryString += " and ";
      }
      if (iHasExcludedRangeEvents)
      {
        queryString += "(";
      }

      queryString += excludedEvents;
      first = false;
    }

    // excluded range events
    if (!string.IsNullOrEmpty(excludedEventRanges))
    {
      if (!first)
      {
        queryString += " and ";
      }
      queryString += excludedEventRanges;
      if (iHasExcludedEvents)
      {
        queryString += ")";
      }
      first = false;
    }

    if (first && !logLevelPresent)
    {
      // no filters included - use match all wildcard
      queryString += "*";
    }

    if (includeExtraClosingBrace)
    {
      // close extra LogLevel bracket
      queryString += ")";
    }

    // tail
    queryString += ")]]";

    // save
    QueryString = queryString;
  }

  private string GenerateIncludedEvents()
  {
    // INCLUDED EVENTS
    if (iIncludedEventIds.Count == 0)
    {
      return String.Empty;
    }

    bool first = true;
    string queryString = String.Empty;
    foreach (string item in iIncludedEventIds)
    {
      if (first)
      {
        queryString += $"(EventID = {item}";
        first = false;
      }
      else
      {
        queryString += $" or EventID = {item}";
      }
    }

    if (!first)
    {
      queryString += ")";
    }
    return queryString;
  }

  private string GenerateExcludedEvents()
  {
    // EXCLUDED EVENTS
    if (iExcludedEventIds.Count == 0)
    {
      return String.Empty;
    }

    bool first = true;
    string queryString = String.Empty;
    foreach (string item in iExcludedEventIds)
    {
      if (first)
      {
        queryString += $"(EventID != {item}";
        first = false;
      }
      else
      {
        queryString += $" and EventID != {item}";
      }
    }

    if (!first)
    {
      queryString += ")";
    }
    return queryString;
  }

  private string GenerateIncludedEventRanges()
  {
    // INCLUDED RANGE EVENTS
    if (iIncludedEventRanges.Count == 0)
    {
      return String.Empty;
    }

    bool first = true;
    string queryString = String.Empty;
    foreach (var item in iIncludedEventRanges)
    {
      if (first)
      {
        if (iIncludedEventRanges.Count > 1)
        {
          queryString += "(";
        }
        queryString += $"(EventID >= {item.Key} and EventID <= {item.Value})";
        first = false;
      }
      else
      {
        queryString += $" or (EventID >= {item.Key} and EventID <= {item.Value})";
      }
    }

    if (!first && iIncludedEventRanges.Count > 1)
    {
      queryString += ")";
    }
    return queryString;
  }

  private string GenerateExcludedEventRanges()
  {
    // EXCLUDED RANGE EVENTS
    if (iExcludedEventRanges.Count == 0)
    {
      return String.Empty;
    }

    bool first = true;
    string queryString = String.Empty;
    foreach (var item in iExcludedEventRanges)
    {
      if (first)
      {
        if (iExcludedEventRanges.Count > 1)
        {
          queryString += "(";
        }
        queryString += $"(EventID < {item.Key} or EventID > {item.Value})";
        first = false;
      }
      else
      {
        queryString += $" and (EventID < {item.Key} or EventID > {item.Value})";
      }
    }

    if (!first && iExcludedEventRanges.Count > 1)
    {
      queryString += ")";
    }
    return queryString;
  }

  private void ParseRawEvents()
  {
    State state = State.StartState;
    string eventAccumulator = String.Empty;
    string rangeAccumulator = String.Empty;

    foreach (char token in iRawEventIds)
    {
      switch (state)
      {
        case State.StartState:
          {
            switch (token)
            {
              case ' ': continue;
              case '-':
                {
                  state = State.InSupressEvent;
                  continue;
                }
              case char digit when Char.IsDigit(token):
                {
                  eventAccumulator += digit;
                  state = State.InEvent;
                  continue;
                }
              default:
                {
                  throw new ArgumentException($"Invalid Event ID filter: '{iRawEventIds.Trim()}'");
                }
            }
          }
        case State.InEvent:
          {
            switch (token)
            {
              case ' ': continue;
              case ',':
                {
                  StoreCurrentEvent(eventAccumulator, "", state);
                  eventAccumulator = rangeAccumulator = "";
                  state = State.StartState;
                  continue;
                }
              case '-':
                {
                  state = State.InRange;
                  continue;
                }
              case char digit when Char.IsDigit(token):
                {
                  eventAccumulator += digit;
                  continue;
                }
              default:
                {
                  throw new ArgumentException($"Invalid Event ID filter: '{iRawEventIds.Trim()}'");
                }
            }
          }
        case State.InRange:
          {
            switch (token)
            {
              case ' ': continue;
              case ',':
                {
                  StoreCurrentEvent(eventAccumulator, rangeAccumulator, state);
                  eventAccumulator = rangeAccumulator = "";
                  state = State.StartState;
                  continue;
                }
              case char digit when Char.IsDigit(token):
                {
                  rangeAccumulator += digit;
                  continue;
                }
              default:
                {
                  throw new ArgumentException($"Invalid inclusive range filter: '{iRawEventIds.Trim()}'");
                }
            }
          }
        case State.InSupressEvent:
          {
            switch (token)
            {
              case ' ': continue;
              case ',':
                {
                  StoreCurrentEvent(eventAccumulator, "", state);
                  eventAccumulator = rangeAccumulator = "";
                  state = State.StartState;
                  continue;
                }
              case '-':
                {
                  state = State.InSuppressRange;
                  continue;
                }
              case char digit when Char.IsDigit(token):
                {
                  eventAccumulator += digit;
                  continue;
                }
              default:
                {
                  throw new ArgumentException($"Invalid Event ID filter: '{iRawEventIds.Trim()}'");
                }
            }
          }
        case State.InSuppressRange:
          {
            switch (token)
            {
              case ' ': continue;
              case ',':
                {
                  StoreCurrentEvent(eventAccumulator, rangeAccumulator, state);
                  eventAccumulator = rangeAccumulator = "";
                  state = State.StartState;
                  continue;
                }
              case char digit when Char.IsDigit(token):
                {
                  rangeAccumulator += digit;
                  continue;
                }
              default:
                {
                  throw new ArgumentException($"Invalid exclusive range filter: '{iRawEventIds.Trim()}'");
                }
            }
          }
      }
    }

    // store last (or only) event
    if (state != State.StartState)
    {
      StoreCurrentEvent(eventAccumulator, rangeAccumulator, state);
    }
  }

  private void StoreCurrentEvent(string eventAccumulator, string rangeAccumulator, State state)
  {
    switch (state)
    {
      case State.InEvent:
        {
          iIncludedEventIds.Add(eventAccumulator);
          break;
        }
      case State.InRange:
        {
          if (string.IsNullOrEmpty(eventAccumulator) || string.IsNullOrEmpty(rangeAccumulator))
          {
            throw new ArgumentException($"Invalid inclusive range filter '{eventAccumulator}'-'{rangeAccumulator}'. Both parts of the range are required");
          }

          _ = int.TryParse(eventAccumulator, out int begin);
          _ = int.TryParse(rangeAccumulator, out int end);
          if (begin >= end)
          {
            throw new ArgumentException($"Invalid inclusive range filter '{begin}-{end}'. Begin must be < end");
          }

          iIncludedEventRanges.Add(eventAccumulator, rangeAccumulator);
          break;
        }
      case State.InSupressEvent:
        {
          if (string.IsNullOrEmpty(eventAccumulator))
          {
            throw new ArgumentException($"Invalid exclusive filter '-{eventAccumulator}'. The event ID must be specified");
          }

          iExcludedEventIds.Add(eventAccumulator);
          break;
        }
      case State.InSuppressRange:
        {
          if (string.IsNullOrEmpty(eventAccumulator) || string.IsNullOrEmpty(rangeAccumulator))
          {
            throw new ArgumentException($"Invalid exclusive range filter -'{eventAccumulator}'-'{rangeAccumulator}'. Both parts of the range are required");
          }

          _ = int.TryParse(eventAccumulator, out int begin);
          _ = int.TryParse(rangeAccumulator, out int end);
          if (begin >= end)
          {
            throw new ArgumentException($"Invalid exclusive range filter '-{begin}-{end}'. Begin must be < end");
          }

          iExcludedEventRanges.Add(eventAccumulator, rangeAccumulator);
          break;
        }
        // Cannot get State.StartState here
    }
  }

  protected enum State
  {
    StartState,
    InEvent,
    InRange,
    InSupressEvent,
    InSuppressRange
  };
}

