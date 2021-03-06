using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;

namespace CK.RouteConfig.Impl
{
    /// <summary>
    /// Internal class used by <see cref="RouteConfiguration.Resolve"/>.
    /// </summary>
    class RouteResolver
    {
        internal readonly RouteConfigurationResolved Root;
        internal readonly Dictionary<string,SubRouteConfigurationResolved> NamedSubRoutes;

        class PreRoute : IRouteConfigurationContext
        {
            readonly IActivityMonitor _monitor;
            readonly IProtoRoute _protoRoute;
            readonly Dictionary<string,ActionConfigurationResolved> _actionsByName;
            readonly List<ActionConfigurationResolved> _actions;

            internal PreRoute( IActivityMonitor monitor, IProtoRoute protoRoute )
            {
                _monitor = monitor;
                _protoRoute = protoRoute;
                _actionsByName = new Dictionary<string,ActionConfigurationResolved>();
                _actions = new List<ActionConfigurationResolved>();
                foreach( var meta in _protoRoute.MetaConfigurations ) meta.Apply( this );
            }

            public List<ActionConfigurationResolved> FinalizeActions()
            {
                for( int i = _actions.Count - 1; i >= 0; --i )
                {
                    if( !_actionsByName.ContainsKey( _actions[i].Name ) ) _actions.RemoveAt( i );
                }
                return _actions;
            }

            #region IRouteConfigurationContext

            IActivityMonitor IRouteConfigurationContext.Monitor { get { return _monitor; } }

            IProtoRoute IRouteConfigurationContext.ProtoRoute { get { return _protoRoute; } }

            IEnumerable<ActionConfigurationResolved> IRouteConfigurationContext.CurrentActions { get { return _actionsByName.Values; } }

            ActionConfigurationResolved IRouteConfigurationContext.FindExisting( string name )
            {
                return _actionsByName.GetValueWithDefault( name, null );
            }

            bool IRouteConfigurationContext.RemoveAction( string name )
            {
                if( !_actionsByName.Remove( name ) )
                {
                    _monitor.SendLine( LogLevel.Warn, string.Format( "Action declaration '{0}' to remove is not found.", name ), null );
                    return false;
                }
                return true;
            }

            bool IRouteConfigurationContext.AddDeclaredAction( string name, string declaredName, bool fromMetaInsert )
            {
                var a = _protoRoute.FindDeclaredAction( declaredName );
                if( a == null ) 
                {
                    if( fromMetaInsert ) _monitor.SendLine( LogLevel.Warn, string.Format( "Action declaration '{0}' not found. Action '{1}' can not be registered.", declaredName, name ), null );
                    return false;
                }
                ActionConfigurationResolved exists = _actionsByName.GetValueWithDefault( name, null );
                if( exists != null )
                {
                    _monitor.SendLine( LogLevel.Error, string.Format( "Action '{0}' can not be added. An action with the same name already exists.", name ), null );
                    return false;
                }
                var added = ActionConfigurationResolved.Create( _monitor, a, true, _actionsByName.Count );
                _actionsByName.Add( name, added );
                _actions.Add( added );
                return true;
            }

            #endregion
        }

        public RouteResolver( IActivityMonitor monitor, RouteConfiguration c )
        {
            try
            {
                using( monitor.OpenGroup( LogLevel.Info, c.Name.Length > 0 ? string.Format( "Resolving root configuration (name is '{0}').", c.Name ) : "Resolving root configuration.", null ) )
                {
                    ProtoResolver protoResolver = new ProtoResolver( monitor, c );
                    NamedSubRoutes = new Dictionary<string, SubRouteConfigurationResolved>();
                    using( monitor.OpenGroup( LogLevel.Info, "Building final routes.", null ) )
                    {
                        var preRoot = new PreRoute( monitor, protoResolver.Root );
                        Root = new RouteConfigurationResolved( protoResolver.Root.FullName, c.ConfigData, preRoot.FinalizeActions() );
                        foreach( IProtoSubRoute sub in protoResolver.NamedSubRoutes.Values )
                        {
                            var preRoute = new PreRoute( monitor, sub );
                            NamedSubRoutes.Add( sub.FullName, new SubRouteConfigurationResolved( sub, preRoute.FinalizeActions() ) );
                        }
                        Root.SubRoutes = protoResolver.Root.SubRoutes.Select( p => NamedSubRoutes[p.FullName] ).ToArray();
                        foreach( IProtoSubRoute sub in protoResolver.NamedSubRoutes.Values )
                        {
                            NamedSubRoutes[sub.FullName].SubRoutes = sub.SubRoutes.Select( p => NamedSubRoutes[p.FullName] ).ToArray();
                        }
                    }
                }
            }
            catch( Exception ex )
            {
                monitor.SendLine( LogLevel.Fatal, null, ex );
            }
        }
    }

}
