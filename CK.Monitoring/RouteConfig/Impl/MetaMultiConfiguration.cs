﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.RouteConfig.Impl
{
    /// <summary>
    /// Base class for meta configuration object that handles multiple items.
    /// </summary>
    public abstract class MetaMultiConfiguration<T> : MetaConfiguration
    {
        readonly List<T> _items;

        public MetaMultiConfiguration( T first, params T[] other )
        {
            _items = new List<T>();
            if( first != null ) _items.Add( first );
            _items.AddRange( other.Where( i => i != null ) );
        }

        protected IReadOnlyList<T> Items
        {
            get { return _items.AsReadOnly(); }
        }

        protected void Add( T item )
        {
            if( item != null ) _items.Add( item );
        }

    }
}