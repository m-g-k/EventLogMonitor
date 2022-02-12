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

namespace EventLogMonitor;
public class SimpleArgumentProcessor
{
  public SimpleArgumentProcessor(string[] args)
  {
    this.iTotalArguments = args.Length;
    this.iRequiredUnFlaggedArgumentsCount = 0;
    this.iOptionalUnFlaggedArgumentsCount = 0;
    this.iTotalFlaggedArguments = 0;
    this.iTotalBooleanArguments = 0;
    this.iTotalInvalidArgumentCount = 0;

    string currentFlag = "";
    for (int i = 0; i < iTotalArguments; ++i)
    {
      string currentArgument = args[i];
      if (currentArgument.Length == 0)
      {
        continue; // skip an empty argument
      }

      iAllArguments.Add(currentArgument);
      if (currentArgument[0].Equals('-') || currentArgument[0].Equals('/'))
      {
        // currentArgument = "-" + currentArgument.Substring(1); //force to be a '-'
        currentArgument = "-" + currentArgument[1..]; // force to be a '-'

        // flagged
        if (!(currentFlag.Length == 0))
        {
          // we have an empty flagged argument (boolean)

          // check it does not already exist
          if (iFlaggedArguments.ContainsKey(currentFlag))
          {
            ++iTotalInvalidArgumentCount; // duplicate argument(s) found. Error is noticed in validate call
          }
          else
          {
            iFlaggedArguments.Add(currentFlag, "");
          }

          ++iTotalBooleanArguments;
        }
        currentFlag = currentArgument;
      }
      else
      {
        // unflagged
        if (currentFlag.Length == 0)
        {
          iUnflaggedArguments.Add(currentArgument);
        }
        else
        {
          // flagged (with value)

          // check it does not already exist
          if (iFlaggedArguments.ContainsKey(currentFlag))
          {
            ++iTotalInvalidArgumentCount; // duplicate argument(s) found. Error is noticed in validate call
          }
          else
          {
            iFlaggedArguments.Add(currentFlag, currentArgument);
          }

          currentFlag = "";
          ++iTotalFlaggedArguments;
        }
      }
    }

    // special case a dangling flagged argument
    if (!(currentFlag.Length == 0))
    {
      // we have an empty flagged argument left over

      // check it does not already exist
      if (iFlaggedArguments.ContainsKey(currentFlag))
      {
        ++iTotalInvalidArgumentCount; // duplicate argument(s) found. Error is noticed in validate call
      }
      else
      {
        iFlaggedArguments.Add(currentFlag, "");
      }

      ++iTotalBooleanArguments;
    }
  }

  public List<string> GetAllArgs()
  {
    return iAllArguments;
  }

  public string GetFlaggedArgument(string flag)
  {
    string match = "";
    if (iFlaggedArguments.ContainsKey(flag))
    {
      match = iFlaggedArguments[flag];
    }

    return match;
  }

  public string GetUnFlaggedArgument(int index)
  {
    string match = "";
    if (index < iUnflaggedArguments.Count)
    {
      match = iUnflaggedArguments[index];
    }

    return match;
  }

  public bool GetBooleanArgument(string flag)
  {
    bool match = false;

    if (iFlaggedArguments.ContainsKey(flag))
    {
      match = true;
    }

    return match;
  }

  // Note: a required argument needs a value, a boolean one does not (to be valid)
  public void SetRequiredFlaggedArgument(string args) { iRequiredFlaggedArguments.Add(args); }
  public void SetOptionalFlaggedArgument(string args) { iOptionalFlaggedArguments.Add(args); }
  public void SetRequiredBooleanArgument(string args) { iRequiredBooleanArguments.Add(args); }
  public void SetOptionalBooleanArgument(string args) { iOptionalBooleanArguments.Add(args); }
  public void SetRequiredUnFlaggedArgumentCount(int count) { iRequiredUnFlaggedArgumentsCount = count; }
  public void SetOptionalUnFlaggedArgumentCount(int count) { iOptionalUnFlaggedArgumentsCount = count; }

  public bool ValidateArguments(bool debug = false)
  {
    // validate the unflagged args next
    int totalUnflaggedArgs = iUnflaggedArguments.Count;
    int totalValidArguments = iRequiredUnFlaggedArgumentsCount + iOptionalUnFlaggedArgumentsCount;
    if (totalUnflaggedArgs < iRequiredUnFlaggedArgumentsCount ||
        totalUnflaggedArgs > totalValidArguments)
    {
      if (debug) { Console.WriteLine("Invalid unflagged argument count. Max expected: {0} , Received: {1}\n", totalValidArguments, totalUnflaggedArgs); }
      return false;
    }

    // validate required flagged arguments. We work on a copy so we can erase as we find
    Dictionary<string, string> flaggedArgumentsCopy = new(iFlaggedArguments);
    {
      foreach (string current in iRequiredFlaggedArguments)
      {
        if (!flaggedArgumentsCopy.ContainsKey(current))
        {
          // not found
          if (debug) { Console.WriteLine("Required argument '{0}' not found\n", current); }
          return false;
        }

        string value = flaggedArgumentsCopy[current];
        if (string.IsNullOrEmpty(value))
        {
          // expected value for flagged argument
          if (debug) { Console.WriteLine("Required argument '{0}' has no value\n", current); }
          return false;
        }

        flaggedArgumentsCopy.Remove(current);
      }
    }

    // validate required boolean arguments
    {
      foreach (string current in iRequiredBooleanArguments)
      {
        if (!flaggedArgumentsCopy.ContainsKey(current))
        {
          // not found
          if (debug) { Console.WriteLine("Required boolean argument '{0}' not found\n", current); }
          return false;
        }

        string value = flaggedArgumentsCopy[current];
        if (!string.IsNullOrEmpty(value))
        {
          // unexpected value for boolean argument
          if (debug) { Console.WriteLine("Required boolean argument '{0}' has an unexpected value '{1}'\n", current, value); }
          return false;
        }

        flaggedArgumentsCopy.Remove(current);
      }
    }

    // validate optional flagged arguments. 
    {
      foreach (string current in iOptionalFlaggedArguments)
      {
        // erase if found and not empty
        if (!flaggedArgumentsCopy.ContainsKey(current))
        {
          // not found
          continue;
        }

        string value = flaggedArgumentsCopy[current];
        if (string.IsNullOrEmpty(value))
        {
          // expected value for flagged argument
          if (debug) { Console.WriteLine("Optional argument '{0}' has no value\n", current); }
          return false;
        }

        flaggedArgumentsCopy.Remove(current);
      }
    }

    // validate optional boolean arguments.
    {
      foreach (string current in iOptionalBooleanArguments)
      {
        // erase if found and not empty
        if (!flaggedArgumentsCopy.ContainsKey(current))
        {
          // not found
          continue;
        }

        string value = flaggedArgumentsCopy[current];
        if (!string.IsNullOrEmpty(value))
        {
          // unexpected value for boolean argument
          if (debug) { Console.WriteLine("Optional boolean argument '{0}' has an unexpected value '{1}'\n", current, value); }
          return false;
        }

        flaggedArgumentsCopy.Remove(current);
      }
    }

    // see if we have any other arguments left over...
    if (flaggedArgumentsCopy.Count > 0)
    {
      // unexpected arguments present
      if (debug)
      {
        Console.WriteLine("Unexpected arguments found: ");
        bool first = true;
        foreach (KeyValuePair<string, string> current in flaggedArgumentsCopy)
        {
          if (!first)
          {
            Console.WriteLine(", ");
          }
          else
          {
            first = false;
          }
          if (string.IsNullOrEmpty(current.Value))
          {
            Console.WriteLine("'{0}'", current.Key);
          }
          else
          {
            Console.WriteLine("'{0}' : '{1}'", current.Key, current.Value);
          }
        }
        Console.WriteLine("\n");
      }
      return false;
    }

    // finally see if we have any errors already (do this last as the duplicate flags could be invalid ones...)
    if (iTotalInvalidArgumentCount != 0)
    {
      // error - invalid arguments found (multiple flags with same name entered)
      if (debug) { Console.WriteLine("Invalid arguments found, {0} flag(s) entered more than once.\n", iTotalInvalidArgumentCount); }
      return false;
    }

    // if we get here, we made it!
    if (debug) { Console.WriteLine("All provided arguments validated ok!\n"); }
    return true;
  }

  readonly private int iTotalArguments;
  readonly private int iTotalFlaggedArguments;
  readonly private int iTotalBooleanArguments;
  readonly private int iTotalInvalidArgumentCount;
  readonly private List<string> iAllArguments = new();
  readonly private List<string> iUnflaggedArguments = new();
  readonly private Dictionary<string, string> iFlaggedArguments = new();

  readonly private List<string> iRequiredFlaggedArguments = new();
  readonly private List<string> iOptionalFlaggedArguments = new();
  readonly private List<string> iRequiredBooleanArguments = new();
  readonly private List<string> iOptionalBooleanArguments = new();
  private int iRequiredUnFlaggedArgumentsCount;
  private int iOptionalUnFlaggedArgumentsCount;

}