﻿using System;
using BlackBerry.NativeCore.QConn.Model;

namespace BlackBerry.NativeCore.QConn.Visitors
{
    /// <summary>
    /// Interface informing about additional status changes of the visitors.
    /// </summary>
    public interface IFileServiceVisitorMonitor
    {
        /// <summary>
        /// Event triggered, when visiting has started.
        /// </summary>
        event EventHandler<VisitorEventArgs> Started;

        /// <summary>
        /// Event fired any time data transfer progress has changed.
        /// </summary>
        event EventHandler<VisitorProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Event fired each time transfer has failed.
        /// </summary>
        event EventHandler<VisitorFailureEventArgs> Failed;

        /// <summary>
        /// Event triggered, when visiting finished.
        /// </summary>
        event EventHandler<VisitorEventArgs> Completed;

        /// <summary>
        /// Waits until visiting is not fully completed.
        /// </summary>
        bool Wait();
    }
}