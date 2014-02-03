﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring.GrandOutputHandlers;
using CK.RouteConfig;
using NUnit.Framework;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class GrandOutputTests
    {
        [SetUp]
        public void Setup()
        {
            TestHelper.InitalizePaths();
        }

        [Test]
        public void ApplyEmptyAndDefaultConfig()
        {
            TestHelper.CleanupFolder( SystemActivityMonitor.RootLogPath + "ApplyEmptyAndDefaultConfig" );

            using( GrandOutput g = new GrandOutput() )
            {
                var m = new ActivityMonitor( false );
                g.Register( m );
                m.Trace().Send( "NoShow-1" );
                Assert.That( g.SetConfiguration( new GrandOutputConfiguration(), TestHelper.ConsoleMonitor ) );
                m.Trace().Send( "NoShow-2" );
                Assert.That( g.SetConfiguration( CreateDefaultConfig( "ApplyEmptyAndDefaultConfig" ), TestHelper.ConsoleMonitor ) );
                m.Trace().Send( "Show-1" );
                Assert.That( g.SetConfiguration( new GrandOutputConfiguration(), TestHelper.ConsoleMonitor ) );
                m.Trace().Send( "NoShow-3" );
            }
            var replayed = new ActivityMonitor( false );
            var c = replayed.Output.RegisterClient( new StupidStringClient() );
            TestHelper.ReplayLogs( new DirectoryInfo( SystemActivityMonitor.RootLogPath + "ApplyEmptyAndDefaultConfig" ), true, mon => replayed, TestHelper.ConsoleMonitor );
            CollectionAssert.AreEqual( new[] { "<Missing log data>", "Show-1" }, c.Entries.Select( e => e.Text ), StringComparer.OrdinalIgnoreCase );
        }

        static GrandOutputConfiguration CreateDefaultConfig( string subFolder )
        {
            GrandOutputConfiguration def = new GrandOutputConfiguration();
            Debug.Assert( def.SourceOverrideFilterApplicationMode == SourceFilterApplyMode.None );
            Debug.Assert( def.AppDomainDefaultFilter == null );
            var route = new RouteConfiguration();
            route.ConfigData = new GrandOutputChannelConfigData();
            route.AddAction( new BinaryFileConfiguration( "All" ) { Path = subFolder } );
            def.ChannelsConfiguration = route;
            return def;
        }

        class RunInAnotherAppDomain : MarshalByRefObject, ISponsor
        {
            static ActivityMonitor _callerMonitor;
            static IDisposable _bridgeToCallerMonitor;
            static ActivityMonitor _localMonitor;

            public static string DomainRootLogPath { get { return FileUtil.NormalizePathSeparator( Path.Combine( TestHelper.GetTestFolder(), "GrandOutputConfigTests" ), true ); } }

            public static string DomainGrandOutputConfig { get { return DomainRootLogPath + "GrandOutput.config"; } }

            public void Initialize( ActivityMonitorBridgeTarget bridgeToConsole )
            {
                SystemActivityMonitor.RootLogPath = DomainRootLogPath;
                if( File.Exists( DomainGrandOutputConfig ) ) File.Delete( DomainGrandOutputConfig );
                _callerMonitor = new ActivityMonitor( false );
                _bridgeToCallerMonitor = _callerMonitor.Output.CreateStrongBridgeTo( bridgeToConsole );
                GrandOutput.EnsureActiveDefaultWithDefaultSettings( _callerMonitor );
                _localMonitor = new ActivityMonitor();
            }

            public void RunNoConfigFileDefaultsToTerse()
            {
                _localMonitor.Trace().Send( "NoConfigFile1-NOSHOW" );
                _localMonitor.Info().Send( "NoConfigFile2-NOSHOW" );
                _localMonitor.Warn().Send( "NoConfigFile3-NOSHOW" );
                _localMonitor.Error().Send( "NoConfigFile4" );
                _localMonitor.Fatal().Send( "NoConfigFile5" );
            }

            public LogFilter GetLocalMonitorActualFilter()
            {
                return _localMonitor.ActualFilter;
            }

            public int GetConfigurationAttemptCount()
            {
                return GrandOutput.Default.ConfigurationAttemptCount;
            }

            public void WaitForNextConfiguration( int configurationAttemptCount )
            {
                GrandOutput.Default.WaitForNextConfiguration( configurationAttemptCount, -1 );
            }

            internal void SendLine( LogLevel level, string msg )
            {
                if( _localMonitor.ShouldLogLine( level ) ) _localMonitor.UnfilteredLog( ActivityMonitor.Tags.Empty, level|LogLevel.IsFiltered, msg, _localMonitor.NextLogTime(), null );
            }

            public void RunWithConfigFileMonitorFilter()
            {
                _localMonitor.Trace().Send( "ConfigFileMonitorFilter-NOSHOW" );
                _localMonitor.Info().Send( "ConfigFileMonitorFilter-NOSHOW" );
                _localMonitor.Warn().Send( "ConfigFileMonitorFilter1" );
                _localMonitor.Error().Send( "ConfigFileMonitorFilter2" );
                _localMonitor.Fatal().Send( "ConfigFileMonitorFilter3" );
            }

            public void Close()
            {
                if( _bridgeToCallerMonitor != null )
                {
                    _bridgeToCallerMonitor.Dispose();
                    _bridgeToCallerMonitor = null;
                }
            }

            public override object InitializeLifetimeService()
            {
                ILease lease = (ILease)base.InitializeLifetimeService();
                if( lease.CurrentState == LeaseState.Initial )
                {
                    lease.Register( this );
                }
                return lease;
            }

            public TimeSpan Renewal( ILease lease )
            {
                return _bridgeToCallerMonitor != null ? lease.InitialLeaseTime : TimeSpan.Zero;
            }
        }

        [Test]
        public void DefaultConfiguration()
        {
            TestHelper.CleanupFolder( RunInAnotherAppDomain.DomainRootLogPath );

            AppDomain domain;
            RunInAnotherAppDomain exec;
            CreateDomainAndExecutor( out domain, out exec );

            try
            {
                exec.Initialize( TestHelper.ConsoleMonitor.Output.BridgeTarget );

                Assert.That( exec.GetLocalMonitorActualFilter(), Is.EqualTo( LogFilter.Terse ) );

                exec.RunNoConfigFileDefaultsToTerse();

                int confCount = exec.GetConfigurationAttemptCount();

                SetDomainConfigTextFile( @"
<GrandOutputConfiguration>
    <Channel MinimalFilter=""Monitor"">
        <Add Type=""BinaryFile"" Name=""AllFromConfig""  Path=""GrandOutputDefault"" />
    </Channel>
</GrandOutputConfiguration>" );

                exec.WaitForNextConfiguration( confCount + 1 );

                Assert.That( exec.GetConfigurationAttemptCount(), Is.EqualTo( confCount + 1 ) );

                Assert.That( exec.GetLocalMonitorActualFilter(), Is.EqualTo( LogFilter.Monitor ) );
                
                exec.RunWithConfigFileMonitorFilter();

                SetDomainConfigTextFile( null );
                
                exec.WaitForNextConfiguration( confCount + 2 );
                Assert.That( exec.GetConfigurationAttemptCount(), Is.EqualTo( confCount + 2 ) );

                Assert.That( exec.GetLocalMonitorActualFilter(), Is.EqualTo( LogFilter.Terse ) );
                exec.SendLine( LogLevel.Warn, "NOSHOW (since it now defaults to Terse filter)" );
                exec.SendLine( LogLevel.Error, "ErrorWithTerseFilter" );

                SetDomainConfigTextFile( @"
<GrandOutputConfiguration>
    <Channel MinimalFilter=""Debug"">
        <Add Type=""BinaryFile"" Name=""AllFromConfig""  Path=""GrandOutputDefault"" />
    </Channel>
</GrandOutputConfiguration>" );

                exec.WaitForNextConfiguration( confCount + 3 );
                Assert.That( exec.GetConfigurationAttemptCount(), Is.EqualTo( confCount + 3 ) );

                Assert.That( exec.GetLocalMonitorActualFilter(), Is.EqualTo( LogFilter.Debug ) );
                exec.SendLine( LogLevel.Trace, "TraceSinceDebug" );

                SetDomainConfigTextFile( "rename" );
                
                exec.WaitForNextConfiguration( confCount + 4 );
                Assert.That( exec.GetConfigurationAttemptCount(), Is.EqualTo( confCount + 4 ) );

                Assert.That( exec.GetLocalMonitorActualFilter(), Is.EqualTo( LogFilter.Terse ) );
                exec.SendLine( LogLevel.Warn, "NOSHOW (since it now defaults to Terse filter)" );
                exec.SendLine( LogLevel.Error, "ErrorWithTerseFilter2" );

                SetDomainConfigTextFile( "renameBack" );
                
                exec.WaitForNextConfiguration( confCount + 5 );
                Assert.That( exec.GetConfigurationAttemptCount(), Is.EqualTo( confCount + 5 ) );

                Assert.That( exec.GetLocalMonitorActualFilter(), Is.EqualTo( LogFilter.Debug ) );
                exec.SendLine( LogLevel.Trace, "TraceSinceDebug2" );

            }
            finally
            {
                try { exec.Close(); } catch {}
                AppDomain.Unload( domain );
            }

            List<StupidStringClient> logs = TestHelper.ReadAllLogs( new DirectoryInfo( RunInAnotherAppDomain.DomainRootLogPath + "GrandOutputDefault" ), false );

            Assert.That( logs.Count, Is.EqualTo( 6 ), "It contains the test monitor but also the monitoring of the reconfiguration due to the 5 file changes." );
            CollectionAssert.AreEqual(
                new[] { "NoConfigFile4", "NoConfigFile5", "ConfigFileMonitorFilter1", "ConfigFileMonitorFilter2", "ConfigFileMonitorFilter3", "ErrorWithTerseFilter", "TraceSinceDebug", "ErrorWithTerseFilter2", "TraceSinceDebug2" }, 
                logs[0].Entries.Select( e => e.Text ), StringComparer.OrdinalIgnoreCase );
        }

        private static void CreateDomainAndExecutor( out AppDomain domain, out RunInAnotherAppDomain exec )
        {
            AppDomainSetup setup = new AppDomainSetup()
            {
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                PrivateBinPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath
            };
            domain = AppDomain.CreateDomain( "GrandOutputConfigTests", null, setup );
            exec = (RunInAnotherAppDomain)domain.CreateInstanceAndUnwrap( typeof( RunInAnotherAppDomain ).Assembly.FullName, typeof( RunInAnotherAppDomain ).FullName );
        }

        private static void SetDomainConfigTextFile( string config )
        {
            if( config != null )
            {
                if( config.StartsWith( "rename" ) )
                {
                    if( config == "rename" )
                        File.Move( RunInAnotherAppDomain.DomainGrandOutputConfig, RunInAnotherAppDomain.DomainRootLogPath + "rename" );
                    else File.Move( RunInAnotherAppDomain.DomainRootLogPath + "rename", RunInAnotherAppDomain.DomainGrandOutputConfig );
                }
                else File.WriteAllText( RunInAnotherAppDomain.DomainGrandOutputConfig, config );
            }
            else File.Delete( RunInAnotherAppDomain.DomainGrandOutputConfig );
        }

    }
}