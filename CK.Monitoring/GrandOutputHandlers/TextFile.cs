using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Monitoring.GrandOutputHandlers
{
    /// <summary>
    /// Binary file handler.
    /// </summary>
    public sealed class TextFile : HandlerBase
    {
        readonly MonitorTextFileOutput _file;

        /// <summary>
        /// Initializes a new <see cref="TextFile"/> bound to its <see cref="TextFileConfiguration"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public TextFile( TextFileConfiguration config )
            : base( config )
        {
            if( config == null ) throw new ArgumentNullException( "config" );
            _file = new MonitorTextFileOutput( config.Path, config.MaxCountPerFile, config.UseGzipCompression );
            _file.FileWriteThrough = config.FileWriteThrough;
            _file.FileBufferSize = config.FileBufferSize;
            _file.MonitorColumn = config.MonitorColumn; 
        }

        /// <summary>
        /// Initialization of the handler: computes the path.
        /// </summary>
        /// <param name="m"></param>
        public override void Initialize( IActivityMonitor m )
        {
            using( m.OpenGroup( LogLevel.Trace, string.Format( "Initializing TextFile handler '{0}' (MaxCountPerFile = {1}).", Name, _file.MaxCountPerFile ), null ) )
            {
                _file.Initialize( m );
            }
        }

        /// <summary>
        /// Writes a log entry (that can actually be a <see cref="IMulticastLogEntry"/>).
        /// </summary>
        /// <param name="logEvent">The log entry.</param>
        /// <param name="parrallelCall">True if this is a parrallel call.</param>
        public override void Handle( GrandOutputEventInfo logEvent, bool parrallelCall )
        {
            _file.Write( logEvent.Entry );
        }

        /// <summary>
        /// Closes the file if it is opened.
        /// </summary>
        /// <param name="m">The monitor to use to track activity.</param>
        public override void Close( IActivityMonitor m )
        {
            m.SendLine( LogLevel.Info, string.Format( "Closing file for TextFile handler '{0}'.", Name ), null );
            _file.Close();
        }

    }

}
