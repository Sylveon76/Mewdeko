﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mewdeko.Core.Common.TypeReaders.Models
{
    public class StoopidTime
    {
        private static readonly Regex _regex = new(
            @"^(?:(?<years>\d)y)?(?:(?<months>\d)mo)?(?:(?<weeks>\d{1,2})w)?(?:(?<days>\d{1,2})d)?(?:(?<hours>\d{1,4})h)?(?:(?<minutes>\d{1,5})m)?(?:(?<seconds>\d{1,6})s)?$"
            ,
            RegexOptions.Compiled | RegexOptions.Multiline);

        private StoopidTime()
        {
        }

        public string Input { get; set; }
        public TimeSpan Time { get; set; }

        public static StoopidTime FromInput(string input)
        {
            var m = _regex.Match(input);

            if (m.Length == 0) throw new ArgumentException("Invalid string input format.");

            var output = "";
            var namesAndValues = new Dictionary<string, int>();

            foreach (var groupName in _regex.GetGroupNames())
            {
                if (groupName == "0") continue;
                if (!int.TryParse(m.Groups[groupName].Value, out var value))
                {
                    namesAndValues[groupName] = 0;
                    continue;
                }

                if (value < 1) throw new ArgumentException($"Invalid {groupName} value.");

                namesAndValues[groupName] = value;
                output += m.Groups[groupName].Value + " " + groupName + " ";
            }

            var ts = new TimeSpan(356 * namesAndValues["years"] +
                                  30 * namesAndValues["months"] +
                                  7 * namesAndValues["weeks"] +
                                  namesAndValues["days"],
                namesAndValues["hours"],
                namesAndValues["minutes"],
                namesAndValues["seconds"]);

            return new StoopidTime
            {
                Input = input,
                Time = ts
            };
        }
    }
}