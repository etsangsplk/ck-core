﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CK.Mon2Htm
{
    static class Program
    {
        static string CKMON2HTM_MUTEX_ID = @"{3E9AD123-6134-4A25-97EC-37F92E5B1B07}";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool isNew, mustRun;

            // If we indicate "Local\", this mutex will not be shared among multiple Terminal Server connections (like RDP sessions).
            // It will be kept in the session namespace. See: http://msdn.microsoft.com/en-us/library/windows/desktop/ms682411(v=vs.85).aspx
            using( Mutex m = new Mutex( false, @"Local\" + CKMON2HTM_MUTEX_ID, out isNew ) )
            {
                mustRun = true;

                if( !isNew )
                {
                    mustRun = false;

                    // Get file from arg
                    string[] args = Environment.GetCommandLineArgs();
                    string[] activationData = AppDomain.CurrentDomain.SetupInformation.ActivationArguments == null ? null : AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;

                    if( activationData != null && activationData.Length > 0 ) // ClickOnce file parameters
                    {
                        WriteStashFile( activationData[0] );
                    }
                    else if( args.Length > 1 )
                    {
                        WriteStashFile( args[1] );
                    }


                    // App already exists. Call it back.
                    NativeMethods.PostMessage(
                        (IntPtr)NativeMethods.HWND_BROADCAST,
                        NativeMethods.WM_SHOWME,
                        IntPtr.Zero,
                        IntPtr.Zero );
                }

                if( mustRun )
                {
                    // Run a new instance
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault( false );

                    Application.Run( new MainForm() );
                }
            }
        }


        /// <summary>
        /// Gets an existing process with the same name and same path, in the same session.
        /// </summary>
        /// <returns>null if none exist, or the target Process.</returns>
        //public static Process GetExistingProcess()
        //{
        //    Process currentProcess = Process.GetCurrentProcess();

        //    Process[] processes = Process.GetProcessesByName( currentProcess.ProcessName );

        //    foreach( Process p in processes )
        //    {
        //        if( (p.Id != currentProcess.Id) &&
        //            (p.MainModule.FileName == currentProcess.MainModule.FileName) &&
        //            (p.SessionId == currentProcess.SessionId) )
        //            return p;
        //    }
        //    return null;
        //}

        public static IEnumerable<string> ReadStashFiles()
        {
            string p = GetStashPath();
            if( !File.Exists( p ) ) return new string[0];

            IEnumerable<string> lines = File.ReadAllLines( p ).Where( x => !String.IsNullOrWhiteSpace( x ) && File.Exists( x ) );

            File.Delete( p );

            return lines;
        }
        public static void WriteStashFiles( IEnumerable<string> files )
        {
            files = files.Where( x => File.Exists( x ) ).Select( x => Path.GetFullPath( x ) );

            string p = GetStashPath();
            if( File.Exists( p ) ) File.Delete( p );

            File.WriteAllLines( p, files );
        }
        public static void WriteStashFile( string file )
        {
            if( !File.Exists( file ) ) return;

            file = Path.GetFullPath( file );

            string p = GetStashPath();
            if( File.Exists( p ) ) File.Delete(p);

            File.WriteAllText( p, file );
        }

        static string GetStashPath()
        {
            return Path.Combine( Path.GetTempPath(), CKMON2HTM_MUTEX_ID );
        }
    }

    /// <summary>
    /// WIN32 wrappers used to call back existing instances with a custom WindowMessage.
    /// </summary>
    internal class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWME = RegisterWindowMessage( "WM_SHOWME" );

        [DllImport( "user32" )]
        public static extern bool PostMessage( IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam );

        [DllImport( "user32" )]
        public static extern int RegisterWindowMessage( string message );
    }
}
