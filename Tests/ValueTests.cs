﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Henchmen.Lua.Tests
{
	[TestClass]
	public class ValueTests
	{
		[TestMethod]
		public void Nils()
		{
			var val = new Value();
			Assert.IsTrue( val.IsNil );
			Assert.IsFalse( val.ToBool() );
			Assert.AreEqual( LValueType.Nil, val.ValueType );

			val.Set( true );
			Assert.IsFalse( val.IsNil );

			val.Set( Value.Nil );
			Assert.IsTrue( val.IsNil );

			val = new Value();
			Assert.IsTrue( val.IsNil );

			val = Value.Nil;
			Assert.IsTrue( val.IsNil );

			val.Set( true );
			Assert.IsFalse( val.IsNil );

			val.SetNil();
			Assert.IsTrue( val.IsNil );
		}

		[TestMethod]
		public void Bools()
		{
			Value val = true;
			Assert.AreEqual( LValueType.Bool, val.ValueType );
			Assert.IsTrue( val.ToBool() );
			Assert.IsTrue( (bool)val );

			val.Set( false );
			Assert.IsFalse( val.ToBool() );
			Assert.IsFalse( (bool)val );

			val.Set( 0 );
			Assert.IsTrue( val.ToBool() );
		}

		[TestMethod,
		ExpectedExceptionAttribute( typeof( InvalidCastException ) )]
		public void ExplicitBoolThrow()
		{
			Value val = 0;
			Assert.IsTrue( (bool)val );
		}

		[TestMethod]
		public void Numbers()
		{
			Value val = 4.5;
			Assert.AreEqual( LValueType.Number, val.ValueType );
			Assert.AreEqual( 4.5, (double)val );
			Assert.AreEqual( 4.5, val.ToDouble() );

			Assert.AreEqual( 4, val.ToInt32() );

			val = uint.MaxValue;
			Assert.AreEqual( uint.MaxValue, (uint)val );
			Assert.AreEqual( uint.MaxValue, val.ToUInt32() );

			val = int.MaxValue;
			Assert.AreEqual( int.MaxValue, (int)val );
			Assert.AreEqual( int.MaxValue, val.ToInt32() );
			
			val = int.MinValue;
			Assert.AreEqual( int.MinValue, val.ToInt32() );

			val.Set( Math.PI );
			Assert.AreEqual( Math.PI, val.ToDouble() );

			for( int i = 0; i < 2048; i++ )
			{
				var nipi = i * Math.PI;
				Value vipi = nipi;

				Assert.AreEqual( nipi, vipi );
				Assert.AreEqual( nipi, vipi.ToDouble() );
				Assert.AreEqual( nipi, (double)vipi );
			}
		}

		[TestMethod]
		public void Strings()
		{
			var a1 = new LString( "A" );
			var a2 = new LString( "A" );
			var b = new LString( "B" );

			Value va1 = a1;
			Value va2 = a2;
			Value vb = b;

			Assert.AreEqual( LValueType.String, va1.ValueType );
			Assert.AreEqual( LValueType.String, va2.ValueType );
			Assert.AreEqual( LValueType.String, vb.ValueType );

			Assert.IsTrue( va1.Equals( a1 ) );
			Assert.IsTrue( va1.Equals( a2 ) );
			Assert.IsFalse( va1.Equals( b ) );

			Assert.IsTrue( Value.Equals( va1, va2 ) );
			Assert.IsFalse( Value.Equals( va1, vb ) );

			Assert.IsTrue( va1.Equals( va2 ) );
			Assert.IsFalse( va1.Equals( vb ) );

			Assert.AreEqual( va1, va2 );
			Assert.AreNotEqual( va1, vb );

			Value nil = 4;
			Assert.AreEqual( 4, nil );
			nil.Set( new LString() );
			Assert.AreEqual( Value.Nil, nil );
			Assert.IsTrue( nil.IsNil );
		}

		[TestMethod]
		public void Sentinels()
		{
			var s0 = Value.CreateSentinel( "AAA" );
			var s1 = Value.CreateSentinel( "AAA" );
			var s2 = Value.CreateSentinel( "AAB" );

			Assert.AreEqual( s0, s0 );
			Assert.AreEqual( s1, s1 );
			Assert.AreEqual( s2, s2 );

			Assert.AreNotEqual( s0, s1 );
			Assert.AreNotEqual( s0, s2 );
			Assert.AreNotEqual( s1, s2 );
		}
	}
}
