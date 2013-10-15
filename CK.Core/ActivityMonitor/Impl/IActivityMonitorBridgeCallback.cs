﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Internal interface that allows ActivityMonitorBridgeTarget to call back
    /// the ActivityMonitorBridges that are bound to it.
    /// </summary>
    interface IActivityMonitorBridgeCallback
    {
        /// <summary>
        /// Gets whether the bridge is in another Application Domain.
        /// </summary>
        bool IsCrossAppDomain { get; }

        /// <summary>
        /// Gets whether this bridge updates the Topic and AutoTags of its monitor whenever they change on the target monitor.
        /// </summary>
        bool PullTopicAndAutoTagsFromTarget { get; }

        /// <summary>
        /// Called when the target filter changed or is dirty. This can be called on any thread.
        /// </summary>
        void OnTargetActualFilterChanged();

        /// <summary>
        /// Called when the target AutoTags changed when IsCrossAppDomain is true.
        /// </summary>
        void OnTargetAutoTagsChanged( string marshalledNewTags );

        /// <summary>
        /// Called when the target AutoTags changed when IsCrossAppDomain is false.
        /// </summary>
        void OnTargetAutoTagsChanged( CKTrait newTags );

        /// <summary>
        /// Called when the target Topic changed.
        /// </summary>
        void OnTargetTopicChanged( string newTopic );
    }
}