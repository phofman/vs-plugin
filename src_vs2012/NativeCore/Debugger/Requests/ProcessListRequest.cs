﻿using System;
using System.Collections.Generic;
using System.Globalization;
using BlackBerry.NativeCore.Debugger.Model;

namespace BlackBerry.NativeCore.Debugger.Requests
{
    /// <summary>
    /// Request class for requesting list of processes.
    /// </summary>
    public sealed class ProcessListRequest : CliRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProcessListRequest()
            : base("info pidlist")
        {
            Processes = new ProcessInfo[0];
        }

        #region Properties

        /// <summary>
        /// Gets the list of processes.
        /// </summary>
        public ProcessInfo[] Processes
        {
            get;
            private set;
        }

        #endregion

        protected override void ProcessResponse(Response response)
        {
            Processes = ParseProcessList(response.Comments);
        }

        /// <summary>
        /// Parses typical response from GDB.
        /// </summary>
        private static ProcessInfo[] ParseProcessList(IEnumerable<string> info)
        {
            const string processEndToken = " - ";
            var result = new List<ProcessInfo>();

            foreach (var process in info)
            {
                int startAt = process.StartsWith("~\"") ? 2 : 0;
                int pidStartAt = process.LastIndexOf(processEndToken, StringComparison.Ordinal);

                if (pidStartAt > 0)
                {
                    string pidString;
                    uint pid;
                    int pidEndAt = process.IndexOf('/', pidStartAt);
                    if (pidEndAt < 0)
                        pidString = process.Substring(pidStartAt + processEndToken.Length).Trim();
                    else
                        pidString = process.Substring(pidStartAt + processEndToken.Length, pidEndAt - pidStartAt - processEndToken.Length).Trim();


                    if (uint.TryParse(pidString, NumberStyles.Number, null, out pid))
                    {
                        // find, if there isn't already a process with identical PID:
                        if (!Contains(result, pid))
                        {
                            var executablePath = process.Substring(startAt, pidStartAt - startAt);
                            result.Add(new ProcessInfo(pid, executablePath));
                        }
                    }
                }
            }

            return result.ToArray();
        }

        private static bool Contains(IEnumerable<ProcessInfo> list, uint pid)
        {
            foreach(var process in list)
                if (process.ID == pid)
                    return true;
            return false;
        }

        /// <summary>
        /// Searches for a process with specified executable (full name or partial)
        /// </summary>
        public ProcessInfo Find(string executable)
        {
            if (string.IsNullOrEmpty(executable))
                throw new ArgumentNullException("executable");

            // first try to find identical executable:
            foreach (var process in Processes)
            {
                if (string.CompareOrdinal(process.ExecutablePath, executable) == 0)
                    return process;
            }

            // is the name matching:
            foreach (var process in Processes)
            {
                if (string.CompareOrdinal(process.Name, executable) == 0)
                    return process;
            }

            // or maybe only ends with it?
            foreach (var process in Processes)
            {
                if (process.ExecutablePath != null && process.ExecutablePath.EndsWith(executable, StringComparison.Ordinal))
                    return process;
            }

            return null;
        }
    }
}