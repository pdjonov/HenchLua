﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Debug = System.Diagnostics.Debug;

namespace LuaSharp
{
	public enum ValueType
	{
		Nil,

		Bool,

		Number,

		String,
		Table,
		UserData,
		Function,
		Thread,
	}

	public struct Value
	{
		public static readonly Value Nil;

		internal object RefVal;
		internal double NumVal;

		/// <summary>
		/// Numbers are represented with RefVal == NumTypeTag
		/// and NumVal == the numeric value.
		/// </summary>
		internal static readonly object NumTypeTag = new object();
		/// <summary>
		/// Open upvalues (and these should only be seen in upvalue
		/// arrays in closures!) are represented with RefVal == OpenUpValueTag
		/// and NumVal == the stack index it points to.
		/// </summary>
		internal static readonly object OpenUpValueTag = new object();

		public ValueType ValueType
		{
			get
			{
				if( RefVal == null )
					return ValueType.Nil;

				if( RefVal == BoolBox.True ||
					RefVal == BoolBox.False )
					return ValueType.Bool;

				if( RefVal == NumTypeTag )
					return ValueType.Number;

				if( RefVal is byte[] )
					return ValueType.String;

				if( RefVal is Table )
					return ValueType.Table;

				if( Callable.IsCallable( RefVal ) )
					return ValueType.Function;

				if( RefVal is Thread )
					return ValueType.Thread;

				return ValueType.UserData;
			}
		}

		#region Constructors

		public Value( bool value )
		{
			RefVal = value ? BoolBox.True : BoolBox.False;
			NumVal = 0;
		}

		public Value( int value )
		{
			RefVal = NumTypeTag;
			NumVal = value;
		}

		public Value( uint value )
		{
			RefVal = NumTypeTag;
			NumVal = value;
		}

		public Value( double value )
		{
			RefVal = NumTypeTag;
			NumVal = value;
		}

		public Value( String value )
		{
			RefVal = value.InternalData;
			NumVal = 0;
		}

		public Value( Table value )
		{
			RefVal = value;
			NumVal = 0;
		}

		public Value( Thread value )
		{
			RefVal = value;
			NumVal = 0;
		}

		public Value( Callable value )
		{
			RefVal = value.Val;
			NumVal = 0;
		}

		public Value( object value )
			: this()
		{
			Set( value );
		}

		#region implicit operator

		public static implicit operator Value( bool value )
		{
			return new Value( value );
		}

		public static implicit operator Value( int value )
		{
			return new Value( value );
		}

		public static implicit operator Value( uint value )
		{
			return new Value( value );
		}

		public static implicit operator Value( double value )
		{
			return new Value( value );
		}

		public static implicit operator Value( String value )
		{
			return new Value( value );
		}

		public static implicit operator Value( Table value )
		{
			return new Value( value );
		}

		public static implicit operator Value( Thread value )
		{
			return new Value( value );
		}

		public static implicit operator Value( Callable value )
		{
			return new Value( value );
		}

		#endregion

		#endregion

		#region Set

		public void SetNil()
		{
			RefVal = null;
		}

		public void Set( bool value )
		{
			RefVal = value ? BoolBox.True : BoolBox.False;
		}

		public void Set( int value )
		{
			RefVal = NumTypeTag;
			NumVal = value;
		}

		public void Set( uint value )
		{
			RefVal = NumTypeTag;
			NumVal = value;
		}

		public void Set( double value )
		{
			RefVal = NumTypeTag;
			NumVal = value;
		}

		public void Set( String value )
		{
			RefVal = value.InternalData;
		}

		public void Set( Table value )
		{
			RefVal = value;
		}
		
		public void Set( Thread value )
		{
			RefVal = value;
		}

		public void Set( Callable value )
		{
			RefVal = value.Val;
		}

		public void Set( Value value )
		{
			this = value;
		}

		public void Set( object value )
		{
			if( value == null )
			{
				RefVal = null;
				return;
			}

			if( value.GetType().IsValueType )
			{
				//the slow case
				SetFromValueType( value );
				return;
			}

			if( value is byte[] )
				value = new UserDataWrapper( value );

			RefVal = value;
		}

		private void SetFromValueType( object value )
		{
			//the quick check
			if( value is Value )
			{
				this = (Value)value;
				return;
			}

			//core types first

			if( value is bool )
				Set( (bool)value );
			else if( value is int )
				Set( (int)value );
			else if( value is double )
				Set( (double)value );
			else if( value is String )
				Set( (String)value );
			else if( value is Callable )
				Set( (Callable)value );

			//and now all the odd cases (note the leading else!)

			else if( value is uint )
				Set( (double)(uint)value );
			else if( value is float )
				Set( (double)(float)value );
			else if( value is sbyte )
				Set( (double)(sbyte)value );
			else if( value is byte )
				Set( (double)(byte)value );
			else if( value is short )
				Set( (double)(short)value );
			else if( value is ushort )
				Set( (double)(ushort)value );

			//unsure whether I should make this an error or not...

			else
				RefVal = value;
		}

		#endregion

		#region To*

		public bool IsNil { get { return RefVal == null; } }

		/// <summary>
		/// Returns true if the value is non-nil and not false.
		/// </summary>
		public bool ToBool()
		{
			return RefVal != null && RefVal != BoolBox.False;
		}

		/// <summary>
		/// Returns the numeric value (if it is a number, else zero).
		/// Does not perform string conversions.
		/// </summary>
		public double ToDouble()
		{
			return RefVal == NumTypeTag ? NumVal : 0;
		}

		/// <summary>
		/// Returns the numeric value (if it is a number, else zero).
		/// Rounds via truncation.
		/// Does not perform string conversions.
		/// </summary>
		public int ToInt32()
		{
			return RefVal == NumTypeTag ? (int)NumVal : 0;
		}

		/// <summary>
		/// Returns the numeric value (if it is a number, else zero).
		/// Rounds via truncation.
		/// Does not perform string conversions.
		/// </summary>
		public uint ToUInt32()
		{
			return RefVal == NumTypeTag ? (uint)NumVal : 0;
		}

		/// <summary>
		/// Returns the value as a string (or a nil string if the value
		/// is not a string).
		/// Does not perform string conversions.
		/// </summary>
		public String ToLString()
		{
			String ret;
			ret.InternalData = RefVal as byte[];
			return ret;
		}

		/// <summary>
		/// Returns the value as a table (if it is a table, else returns null).
		/// </summary>
		public Table ToTable()
		{
			return RefVal as Table;
		}

		/// <summary>
		/// Returns the value as a table (if it is a table, else returns null).
		/// </summary>
		public Thread ToThread()
		{
			return RefVal as Thread;
		}

		/// <summary>
		/// Gets the value as userdata (if it is userdata, else returns nil).
		/// Note that builtin types don't count as userdata.
		/// </summary>
		public object ToUserData()
		{
			if( RefVal == null || RefVal == BoolBox.True ||
				RefVal == BoolBox.False || RefVal == NumTypeTag )
				return null;

			if( RefVal is byte[] || RefVal is Table ||
				Callable.IsCallable( RefVal ) )
				return null;

			var asWrapper = RefVal as UserDataWrapper;
			if( asWrapper != null )
				return asWrapper.Value;

			return RefVal;
		}

		#region explicit operator

		//these are similar to the To* methods
		//except they throw InvalidCastException
		//if the underlying type doesn't match

		public static explicit operator bool( Value value )
		{
			if( value.RefVal == BoolBox.True )
				return true;
			if( value.RefVal == BoolBox.False )
				return false;

			throw new InvalidCastException();
		}

		public static explicit operator int( Value value )
		{
			if( value.RefVal != NumTypeTag )
				throw new InvalidCastException();
			
			return (int)value.NumVal;
		}

		public static explicit operator uint( Value value )
		{
			if( value.RefVal != NumTypeTag )
				throw new InvalidCastException();

			return (uint)value.NumVal;
		}

		public static explicit operator double( Value value )
		{
			if( value.RefVal != NumTypeTag )
				throw new InvalidCastException();

			return value.NumVal;
		}

		public static explicit operator String( Value value )
		{
			var asStr = value.RefVal as byte[];
			if( asStr == null )
				throw new InvalidCastException();

			return new String() { InternalData = asStr };
		}

		public static explicit operator Table( Value value )
		{
			return (Table)value.RefVal;
		}

		public static explicit operator Thread( Value value )
		{
			return (Thread)value.RefVal;
		}

		#endregion

		#endregion

		#region GetHashCode

		internal static int GetHashCode( bool value )
		{
			return value ? 1 : 0;
		}

		internal static int GetHashCode( double value )
		{
			return value.GetHashCode();
		}

		internal static int GetHashCode( object value )
		{
			return System.Runtime.CompilerServices.
				RuntimeHelpers.GetHashCode( value );
		}

		public override int GetHashCode()
		{
			if( RefVal == null || RefVal == BoolBox.False )
				return 0;

			if( RefVal == BoolBox.True )
				return 1;

			if( RefVal == NumTypeTag )
				return GetHashCode( NumVal );

			var asStr = RefVal as byte[];
			if( asStr != null )
				return String.InternalGetHashCode( asStr );

			var val = RefVal;

			var asWrapper = val as UserDataWrapper;
			if( asWrapper != null )
				val = asWrapper.Value;

			return GetHashCode( val );
		}

		#endregion

		#region Equals

		public bool Equals( bool value )
		{
			return RefVal == (value ? BoolBox.True : BoolBox.False);
		}

		public bool Equals( double value )
		{
			return RefVal == NumTypeTag &&
				NumVal == value;
		}

		public bool Equals( String str )
		{
			var asStr = RefVal as byte[];
			return String.InternalEquals( asStr, str.InternalData );
		}

		public bool Equals( Value other )
		{
			return Equals( this, other );
		}

		public override bool Equals( object obj )
		{
			var asVal = obj is Value ? (Value)obj : new Value( obj );
			return Equals( this, asVal );
		}

		public static bool Equals( Value a, Value b )
		{
			if( a.RefVal == b.RefVal )
				return a.RefVal != NumTypeTag || a.NumVal == b.NumVal;

			var asStrA = a.RefVal as byte[];
			if( asStrA != null )
				return String.InternalEquals( asStrA, b.RefVal as byte[] );

			return false;
		}

		#region operators

		public static bool operator ==( Value a, Value b )
		{
			return Equals( a, b );
		}

		public static bool operator !=( Value a, Value b )
		{
			return !Equals( a, b );
		}

		public static bool operator ==( Value v, bool b )
		{
			return v.Equals( b );
		}

		public static bool operator !=( Value v, bool b )
		{
			return !v.Equals( b );
		}

		public static bool operator ==( Value v, int n )
		{
			return v.Equals( n );
		}

		public static bool operator ==( Value v, uint n )
		{
			return v.Equals( n );
		}

		public static bool operator ==( Value v, double n )
		{
			return v.Equals( n );
		}

		public static bool operator !=( Value v, int n )
		{
			return !v.Equals( n );
		}

		public static bool operator !=( Value v, uint n )
		{
			return !v.Equals( n );
		}

		public static bool operator !=( Value v, double n )
		{
			return !v.Equals( n );
		}

		public static bool operator ==( Value v, String s )
		{
			return v.Equals( s );
		}

		public static bool operator !=( Value v, String s )
		{
			return !v.Equals( s );
		}

		public static bool operator ==( Value v, object o )
		{
			return v.Equals( o );
		}

		public static bool operator !=( Value v, object o )
		{
			return !v.Equals( o );
		}

		#endregion

		#endregion

		public override string ToString()
		{
			if( RefVal == null )
				return "(nil)";

			if( RefVal == BoolBox.True )
				return "true";

			if( RefVal == BoolBox.False )
				return "false";

			if( RefVal == NumTypeTag )
				return NumVal.ToString();

			var asStr = RefVal as byte[];
			if( asStr != null )
				return String.InternalFromData( asStr ).ToString();

			var val = RefVal;

			var asWrapper = val as UserDataWrapper;
			if( asWrapper != null )
				val = asWrapper.Value;

			return val.ToString();
		}

		internal class UserDataWrapper
		{
			public object Value;

			public UserDataWrapper( object value )
			{
				Debug.Assert( value != null );

				this.Value = value;
			}
		}
	}

	/// <summary>
	/// A more compact Value representation.
	/// This type boxes its numbers, and reuses the boxes to reduce GC thrashing.
	/// Do -NOT- blithely copy this type around, as it can cause issues with the boxed numbers.
	/// </summary>
	internal struct CompactValue
	{
		internal object Val;

		public CompactValue( Value value )
		{
			if( value.RefVal == Value.NumTypeTag )
				Val = new NumBox( value.NumVal );
			else
				Val = value.RefVal;
		}

		public CompactValue( double value )
		{
			Val = new NumBox( value );
		}

		public CompactValue( String value )
		{
			Val = value.InternalData;
		}

		public void Set( ref Value value )
		{
			if( value.RefVal == Value.NumTypeTag )
				Set( value.NumVal );
			else
				Val = value.RefVal;
		}

		public void Set( double value )
		{
			var asNum = Val as NumBox;
			if( asNum == null )
				Val = asNum = new NumBox();
			asNum.Value = value;
		}

		public double ToDouble()
		{
			var asNum = Val as NumBox;
			return asNum != null ? asNum.Value : 0;
		}

		public void ToValue( out Value v )
		{
			var asNum = Val as NumBox;
			if( asNum != null )
			{
				v.RefVal = Value.NumTypeTag;
				v.NumVal = asNum.Value;
			}
			else
			{
				v.RefVal = Val;
				v.NumVal = 0;
			}
		}

		public Value ToValue()
		{
			Value ret;
			ToValue( out ret );
			return ret;
		}

		public static bool Equals( CompactValue a, CompactValue b )
		{
			if( a.Val == b.Val )
				return true;

			var asNumA = a.Val as NumBox;
			if( asNumA != null )
			{
				var asNumB = b.Val as NumBox;
				return asNumB != null && asNumA.Value == asNumB.Value;
			}

			var asStrA = a.Val as byte[];
			if( asStrA != null )
				return String.InternalEquals( asStrA, b.Val as byte[] );

			return false;
		}

		public bool Equals( CompactValue other )
		{
			return Equals( this, other );
		}

		public override bool Equals( object obj )
		{
			return obj is CompactValue && Equals( (CompactValue)obj );
		}

		public bool Equals( Value value )
		{
			if( Val == value.RefVal )
				return true;

			if( value.RefVal == Value.NumTypeTag )
			{
				var asNum = Val as NumBox;
				return asNum != null && asNum.Value == value.NumVal;
			}

			var asStr = Val as byte[];
			if( asStr != null )
				return String.InternalEquals( asStr, value.RefVal as byte[] );

			return false;
		}

		public bool Equals( double value )
		{
			var asNum = Val as NumBox;
			return asNum != null && asNum.Value == value;
		}

		public bool Equals( String value )
		{
			Debug.Assert( value.InternalData != null );
			return String.InternalEquals( value.InternalData, Val as byte[] );
		}

		public override int GetHashCode()
		{
			if( Val == null || Val == BoolBox.False )
				return 0;

			if( Val == BoolBox.True )
				return 1;

			var asNum = Val as NumBox;
			if( asNum != null )
				return Value.GetHashCode( asNum.Value );

			var asStr = Val as byte[];
			if( asStr != null )
				return String.InternalGetHashCode( asStr );

			var val = Val;

			var asWrapper = val as Value.UserDataWrapper;
			if( asWrapper != null )
				val = asWrapper.Value;

			return Value.GetHashCode( val );
		}

		public override string ToString()
		{
			return ToValue().ToString();
		}
	}
}
