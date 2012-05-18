﻿using System;

namespace CK.Core
{
    /// <summary>
    /// Specialized <see cref="IActivityLogger"/> that contains a non removable <see cref="ActivityLoggerTap"/>.
    /// Concrete implementation must be obtained through <see cref="DefaultActivityLogger.Create"/> factrory method.
    /// </summary>
    public interface IDefaultActivityLogger : IActivityLogger
    {
        /// <summary>
        /// Gets the <see cref="ActivityLoggerTap"/> that manages <see cref="IActivityLoggerSink"/>
        /// for this <see cref="DefaultActivityLogger"/>.
        /// </summary>
        ActivityLoggerTap Tap { get; }

        /// <summary>
        /// Adds an <see cref="IActivityLoggerSink"/> to the <see cref="RegisteredSinks"/>.
        /// Duplicate <see cref="IActivityLoggerSink"/> are silently ignored.
        /// </summary>
        /// <param name="sink">An activity logger sink implementation.</param>
        /// <returns>This logger to enable fluent syntax.</returns>
        IDefaultActivityLogger Register( IActivityLoggerSink sink );

        /// <summary>
        /// Unregisters the given <see cref="IActivityLoggerSink"/> from the collection of loggers.
        /// Silently ignored unregistered logger.
        /// </summary>
        /// <param name="sink">An activity logger sink implementation.</param>
        /// <returns>This logger to enable fluent syntax.</returns>
        IDefaultActivityLogger Unregister( IActivityLoggerSink sink );

        /// <summary>
        /// Gets the list of registered <see cref="IActivityLoggerSink"/>.
        /// </summary>
        IReadOnlyList<IActivityLoggerSink> RegisteredSinks { get; }
    }
}