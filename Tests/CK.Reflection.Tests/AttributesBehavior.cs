#region LGPL License
/*----------------------------------------------------------------------------
* This file (Tests\CK.Reflection.Tests\AttributesBehavior.cs) is part of CiviKey. 
*  
* CiviKey is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Lesser General Public License as published 
* by the Free Software Foundation, either version 3 of the License, or 
* (at your option) any later version. 
*  
* CiviKey is distributed in the hope that it will be useful, 
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
* GNU Lesser General Public License for more details. 
* You should have received a copy of the GNU Lesser General Public License 
* along with CiviKey.  If not, see <http://www.gnu.org/licenses/>. 
*  
* Copyright © 2007-2015, 
*     Invenietis <http://www.invenietis.com>,
*     In’Tech INFO <http://www.intechinfo.fr>,
* All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace CK.Reflection.Tests
{
    [TestFixture]
    [ExcludeFromCodeCoverage]
    class AttributesBehavior
    {

        interface IMarker
        {
        }

        [AttributeUsage( AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property )]
        class MarkerAttribute : Attribute, IMarker
        {
        }

        [AttributeUsage( AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property )]
        class Marker2Attribute : Attribute, IMarker
        {
        }

        [Marker]
        class Test
        {
            [Marker]
            public void Method() { }

            [Marker]
            [Marker2]
            public void Method2() { }
        }

        [Test]
        public void WorksWithAbstractions()
        {
            Assert.That( typeof( Test ).GetTypeInfo().IsDefined( typeof( IMarker ), false ), Is.True, "IsDefined works with any base type of attributes." );
            Assert.That( typeof( Test ).GetMethod( "Method" ).IsDefined( typeof( IMarker ), false ), Is.True, "IsDefined works with any base type of attributes." );

            Assert.That( typeof( Test ).GetMethod( "Method2" ).IsDefined( typeof( IMarker ), false ), Is.True, "IsDefined works with multiple attributes." );
            Assert.That( typeof( Test ).GetMethod( "Method2" ).GetCustomAttributes( typeof( IMarker ), false ).Count(), Is.EqualTo( 2 ), "GetCustomAttributes works with multiple base type attributes." );

        }
        
        [Test]
        public void CreatedEachTimeGetCustomAttributesIsCalled()
        {
            object a1 = typeof( Test ).GetMethod( "Method" ).GetCustomAttributes( typeof( IMarker ), false ).First();
            object a2 = typeof( Test ).GetMethod( "Method" ).GetCustomAttributes( typeof( IMarker ), false ).First();
            Assert.That( a1, Is.Not.SameAs( a2 ) );
        }
    }
}
