using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if NET451 || NET46
using System.Runtime.Remoting.Lifetime;
#endif
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring.GrandOutputHandlers;
using CK.RouteConfig;
using System.Threading;

namespace CK.Monitoring
{
    public sealed partial class GrandOutput
    {
        static object _watcherLock = new object();
        static string _configPath = null;
        static DateTime _lastConfigFileWriteTime = FileUtil.MissingFileLastWriteTimeUtc;
        static FileSystemWatcher _watcher;

        /// <summary>
        /// Ensures that the <see cref="Default"/> GrandOutput is created (see <see cref="EnsureActiveDefault"/>) and configured with default settings:
        /// only one one channel with its minimal filter sets to Debug with one text file handler that writes .txt files in "<see cref="SystemActivityMonitor.RootLogPath"/>\GrandOutputDefault" directory.
        /// The <see cref="SystemActivityMonitor.RootLogPath"/> must be valid and if a GrandOutput.config file exists inside, it is loaded as the configuration.
        /// If it exists, it must be valid (otherwise an exception is thrown).
        /// Once loaded, the file is monitored and any change that occurs to it dynamically triggers a <see cref="SetConfiguration"/> with the new file.
        /// </summary>
        /// <param name="monitor">An optional monitor.</param>
        static public GrandOutput EnsureActiveDefaultWithDefaultSettings( IActivityMonitor monitor = null )
        {
            lock( _defaultLock )
            {
                if( _default == null )
                {
                    if( monitor == null ) monitor = new SystemActivityMonitor( true, "GrandOutput" ) { MinimalFilter = GrandOutputMinimalFilter };
                    using( monitor.OpenGroup( LogLevel.Info, "Attempting Default GrandOutput configuration.", null ) )
                    {
                        try
                        {
                            SystemActivityMonitor.AssertRootLogPathIsSet();
                            _configPath = SystemActivityMonitor.RootLogPath + "GrandOutput.config";
                            GrandOutputConfiguration def = CreateDefaultConfig();
                            if( !File.Exists( _configPath ) )
                            {
                                File.WriteAllText( _configPath, _defaultConfig );
                            }
                            if( !def.LoadFromFile( _configPath, monitor ) )
                            {
                                throw new CKException( "Unable to load Configuration file: '{0}'.", _configPath );
                            }
                            GrandOutput output = new GrandOutput();
                            if( !output.SetConfiguration( def, monitor ) )
                            {
                                throw new CKException( "Failed to set Configuration." );
                            }
                            StartMonitoring( monitor );
                            _default = output;
                            ActivityMonitor.AutoConfiguration += m => _default.Register( m );
                        }
                        catch( Exception ex )
                        {
                            monitor.SendLine( LogLevel.Fatal, null, ex );
                            throw;
                        }
                    }
                }
            }
            return _default;
        }

        const string _defaultConfig =
@"<GrandOutputConfiguration>
    <Channel MinimalFilter=""Debug"">
        <Add Type=""TextFile"" Name=""All"" Path=""GrandOutputDefault"" MaxCountPerFile=""20000"" />
    </Channel>
</GrandOutputConfiguration>";

        static GrandOutputConfiguration CreateDefaultConfig()
        {
            GrandOutputConfiguration def = new GrandOutputConfiguration();
            Debug.Assert( def.SourceOverrideFilterApplicationMode == SourceFilterApplyMode.None );
            Debug.Assert( def.GlobalDefaultFilter == null );
            var route = new RouteConfiguration();
            route.ConfigData = new GrandOutputChannelConfigData() { MinimalFilter = LogFilter.Debug };
            route.AddAction( new TextFileConfiguration( "All" ) { Path = "GrandOutputDefault" } );
            def.ChannelsConfiguration = route;
            return def;
        }

        static void StartMonitoring( IActivityMonitor monitor )
        {
            if( _watcher != null ) _watcher.Dispose();
            _watcher = new FileSystemWatcher();
            _watcher.Path = SystemActivityMonitor.RootLogPath;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Filter = "GrandOutput.config";
            _watcher.Changed += _watcher_Changed;
            _watcher.Deleted += _watcher_Changed;
            _watcher.Renamed += _watcher_Changed;
            _watcher.Error += _watcher_Error;
            _watcher.EnableRaisingEvents = true;
        }

        static void _watcher_Error( object sender, ErrorEventArgs e )
        {
            ActivityMonitor.CriticalErrorCollector.Add( e.GetException(), String.Format( "While monitoring GrandOutput.Default configuration file '{0}'.", _watcher.Path ) );
        }

        static void _watcher_Changed( object sender, FileSystemEventArgs unusedEventArgs )
        {
            if( _watcher == null ) return;
            ThreadPool.UnsafeQueueUserWorkItem( Reload, null );
        }

        static void Reload( object state )
        {
            if( _watcher == null ) return;
            // Quick and dirty trick to handle Renamed events
            // or too quick events.
            Thread.Sleep( 100 );
            var time = File.GetLastWriteTimeUtc( _configPath );
            if( time != _lastConfigFileWriteTime )
            {
                GrandOutputConfiguration def;
                lock( _watcherLock )
                {
                    time = File.GetLastWriteTimeUtc( _configPath );
                    if( time == _lastConfigFileWriteTime ) return;
                    _lastConfigFileWriteTime = time;
                }
                var monitor = new SystemActivityMonitor( true, "GrandOutput.Default.Reconfiguration" ) { MinimalFilter = GrandOutputMinimalFilter };
                try
                {
                    using( monitor.OpenGroup( LogLevel.Info, string.Format( "AppDomain '{0}',  file '{1}' changed (change n°{2}).", AppDomain.CurrentDomain.FriendlyName, _configPath, _default.ConfigurationAttemptCount ), null ) )
                    {
                        def = CreateDefaultConfig();
                        if( File.Exists( _configPath ) )
                        {
                            if( time == FileUtil.MissingFileLastWriteTimeUtc ) _lastConfigFileWriteTime = File.GetLastWriteTimeUtc( _configPath );
                            def.LoadFromFile( _configPath, monitor );
                        }
                        else
                        {
                            _lastConfigFileWriteTime = FileUtil.MissingFileLastWriteTimeUtc;
                            monitor.SendLine( LogLevel.Trace, "File missing: applying catch-all default configuration.", null );
                        }
                        if( !_default._channelHost.IsDisposed ) _default.SetConfiguration( def, monitor );
                    }
                }
                catch( Exception ex )
                {
                    monitor.SendLine( LogLevel.Error, null, ex );
                }
            }
        }
    }
}
