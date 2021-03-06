using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class ChannelAndSourceFilterTests
    {
        [SetUp]
        public void Setup()
        {
            TestHelper.InitalizePaths();
        }

        [Test]
        public void FilteringBySource()
        {
            TestHelper.CleanupFolder( SystemActivityMonitor.RootLogPath + "FilteringBySource" );

            using( GrandOutput g = new GrandOutput() )
            {
                GrandOutputConfiguration config = new GrandOutputConfiguration();
                config.Load( XDocument.Parse( @"
<GrandOutputConfiguration>
    <Channel MinimalFilter=""Debug"">
        <Add Type=""BinaryFile"" Name=""All"" Path=""FilteringBySource"" />
        <Channel Name=""HiddenTopic"" MinimalFilter=""Off"" TopicRegex=""(hidden\s+topic|hide\s+this\s+topic)"" MatchOptions=""CultureInvariant, ExplicitCapture, Compiled, Multiline"" />
    </Channel>
    <SourceOverrideFilter>
        <Add File=""SourceFile-Debug.cs"" Filter=""Debug"" />
        <Add File=""SourceFile-Off.cs"" Filter=""Off"" />
        <Add File=""SourceFile-Strange.cs"" Filter=""{Trace,Fatal}"" />
    </SourceOverrideFilter>
</GrandOutputConfiguration>", LoadOptions.SetLineInfo ).Root, TestHelper.ConsoleMonitor );

                Assert.That( g.SetConfiguration( config, TestHelper.ConsoleMonitor ) );

                var m = new ActivityMonitor( false );
                g.Register( m );

                m.Fatal( fileName: "SourceFile-Off.cs" ).Send( "NOSHOW" );
                m.SetTopic( "This is a hidden topic..." );
                m.Trace( 0, "SourceFile-Debug.cs" ).Send( "Trace-1" );
                m.Trace().Send( "NOSHOW" );
                m.SetTopic( "Please, hide this topic!" );
                m.Trace( fileName: "SourceFile-Strange.cs" ).Send( "NOSHOW" );
                using( m.OpenTrace( fileName: "SourceFile-Strange.cs" ).Send( "Trace-2" ) )
                {
                    m.Trace( fileName: "SourceFile-Strange.cs" ).Send( "NOSHOW" );
                    m.Fatal( fileName: "SourceFile-Strange.cs" ).Send( "Fatal-1" );
                    m.Fatal( fileName: "SourceFile-Off.cs" ).Send( "NOSHOW" );
                }
                m.SetTopic( null );
                m.Trace( fileName: "SourceFile-Strange.cs" ).Send( "NOSHOW" );
                m.Fatal( fileName: "SourceFile-Off.cs" ).Send( "NOSHOW" );
                m.Trace().Send( "Trace-3" );
            }
            List<StupidStringClient> logs = TestHelper.ReadAllLogs( new DirectoryInfo( SystemActivityMonitor.RootLogPath + "FilteringBySource" ), false );
            Assert.That( logs.Count, Is.EqualTo( 1 ) );
            Assert.That( logs[0].ToString(), Does.Not.Contain( "NOSHOW" ) );
            var texts = logs[0].Entries.Select( e => e.Text ).ToArray();
            CollectionAssert.AreEqual( new string[] { 
                "Trace-1", 
                "Trace-2", 
                "Fatal-1", 
                ActivityMonitor.SetTopicPrefix, 
                "Trace-3", 
            }, texts, StringComparer.Ordinal );
        }

        [Test]
        public void FilteringByTopic()
        {
            TestHelper.CleanupFolder( SystemActivityMonitor.RootLogPath + "FilteringByTopic" );

            using( GrandOutput g = new GrandOutput() )
            {
                GrandOutputConfiguration config = new GrandOutputConfiguration();
                config.Load( XDocument.Parse( @"
<GrandOutputConfiguration>
    <Channel MinimalFilter=""Debug"">
        <Add Type=""BinaryFile"" Name=""All"" Path=""FilteringByTopic"" />
        <Channel Name=""HiddenTopic"" MinimalFilter=""Off"" TopicFilter=""*H*T?pic*"" >
            <Channel Name=""SavedHiddenTopic"" MinimalFilter=""Release"" TopicFilter=""*DOSHOW*"" >
            </Channel>
        </Channel>
        <Channel Name=""MonitoredTopic"" MinimalFilter=""Monitor"" TopicFilter=""*MONITOR*"" >
        </Channel>
    </Channel>
</GrandOutputConfiguration>", LoadOptions.SetLineInfo ).Root, TestHelper.ConsoleMonitor );

                Assert.That( g.SetConfiguration( config, TestHelper.ConsoleMonitor ) );

                var fullyHidden = new ActivityMonitor( false );
                g.Register( fullyHidden );
                fullyHidden.SetTopic( "A fully hidden topic: setting the topic before any send, totally hides the monitor if the Actual filter is Off. - NOSHOW" );
                fullyHidden.Fatal().Send( "NOSHOW" );
                using( fullyHidden.OpenFatal().Send( "NOSHOW" ) )
                {
                    fullyHidden.Fatal().Send( "NOSHOW" );
                }

                var m = new ActivityMonitor( false );
                g.Register( m );
                m.Trace().Send( "Trace-1" );
                m.SetTopic( "This is a Hidden Topic - NOSHOW" );
                m.Fatal().Send( "NOSHOW" );
                m.SetTopic( "Visible Topic" );
                m.Trace().Send( "Trace-2" );
                m.SetTopic( "This is a Hidden Topic but DOSHOW puts it in Release mode." );
                m.Trace().Send( "NOSHOW" );
                m.Info().Send( "NOSHOW" );
                m.Warn().Send( "NOSHOW" );
                m.Error().Send( "Error-1" );
                m.Fatal().Send( "Fatal-1" );
                m.SetTopic( "This is a HT?PIC (off). Match is case insensitive - NOSHOW" );
                m.Fatal().Send( "NOSHOW" );
                m.SetTopic( null );
                m.Trace().Send( "Trace-3" );
                m.SetTopic( "A MONITORed topic: If i wrote This is a t.o.p.i.c (without the dots), this would have matched the HiddenT.o.p.i.c channel..." );
                m.Trace().Send( "NOSHOW" );
                m.Warn().Send( "Warn-1" );
            }
            List<StupidStringClient> logs = TestHelper.ReadAllLogs( new DirectoryInfo( SystemActivityMonitor.RootLogPath + "FilteringByTopic" ), false );
            Assert.That( logs.Count, Is.EqualTo( 1 ), "Fully hidden monitor does not appear." );
            Assert.That( logs[0].ToString(), Does.Not.Contain( "NOSHOW" ) );
            var texts = logs[0].Entries.Select( e => e.Text ).ToArray();
            CollectionAssert.AreEqual( new string[] { 
                "Trace-1", 
                ActivityMonitor.SetTopicPrefix + "Visible Topic", 
                "Trace-2", 
                ActivityMonitor.SetTopicPrefix + "This is a Hidden Topic but DOSHOW puts it in Release mode.", 
                "Error-1", 
                "Fatal-1", 
                ActivityMonitor.SetTopicPrefix, 
                "Trace-3", 
                ActivityMonitor.SetTopicPrefix + "A MONITORed topic: If i wrote This is a t.o.p.i.c (without the dots), this would have matched the HiddenT.o.p.i.c channel...", 
                "Warn-1"
            }, texts, StringComparer.Ordinal );
        }

    }
}
