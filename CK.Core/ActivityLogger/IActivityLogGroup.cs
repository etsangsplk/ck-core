﻿using System;

namespace CK.Core
{
    /// <summary>
    /// Exposes all the relevant information fo a currently opened group.
    /// Groups are linked together from the current one to the very first one 
    /// thanks to the <see cref="Parent"/> property.
    /// </summary>
    public interface IActivityLogGroup
    {
        /// <summary>
        /// Gets the origin <see cref="IActivityLogger"/> for the log group.
        /// </summary>
        IActivityLogger OriginLogger { get; }

        /// <summary>
        /// Get the previous group in its <see cref="OriginLogger"/>. Null if this is a top level group.
        /// </summary>
        IActivityLogGroup Parent { get; }

        /// <summary>
        /// Gets the depth of this group in its <see cref="OriginLogger"/> (1 for top level groups).
        /// </summary>
        int Depth { get; }

        /// <summary>
        /// Gets the <see cref="LogLevelFilter"/> for this group. Initialized with 
        /// the <see cref="IActivityLogger.Filter"/> at the time the group has been opened.
        /// </summary>
        LogLevelFilter Filter { get; }

        /// <summary>
        /// Gets the level associated to this group.
        /// </summary>
        LogLevel GroupLevel { get; }

        /// <summary>
        /// Getst the text associated to this group.
        /// </summary>
        string GroupText { get; }

        /// <summary>
        /// Gets the associated <see cref="Exception"/> if it exists.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// Gets whether the <see cref="GroupText"/> is actually the <see cref="Exception"/> message.
        /// </summary>
        bool IsGroupTextTheExceptionMessage { get; }
    }
}
