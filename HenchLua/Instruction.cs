﻿using System;
using System.Text;

using Debug = System.Diagnostics.Debug;

namespace Henchmen.Lua
{
	[Serializable]
	internal struct Instruction
	{
		public uint PackedValue;

		private const int OpCodeShift = 0;
		private const int OpCodeMask = 0x3F;

		public OpCode OpCode
		{
			get { return (OpCode)((PackedValue >> OpCodeShift) & OpCodeMask); }
			set { SetField( (int)value, OpCodeShift, OpCodeMask ); }
		}

		private const int AShift = 6;
		private const int AMask = 0xFF;

		public int A
		{
			get { return (int)(PackedValue >> AShift) & AMask; }
			set { SetField( value, AShift, AMask ); }
		}

		private const int BShift = 23;
		private const int BMask = 0x1FF;

		public int B
		{
			get { return (int)(PackedValue >> BShift) & BMask; }
			set { SetField( value, BShift, BMask ); }
		}

		private const int CShift = 14;
		private const int CMask = 0x1FF;

		public int C
		{
			get { return (int)(PackedValue >> CShift) & CMask; }
			set { SetField( value, CShift, CMask ); }
		}

		private const int BxShift = 14;
		private const int BxMask = 0x3FFFF;

		public int Bx
		{
			get { return (int)(PackedValue >> BxShift) & BxMask; }
			set { SetField( value, BxShift, BxMask ); }
		}

		private const int SBxMax = (BxMask >> 1);

		public int SBx
		{
			get { return Bx - SBxMax; }
			set { Bx = value + SBxMax; }			
		}

		private const int AxShift = 6;
		private const int AxMask = 0x3FFFFFF;

		public int Ax
		{
			get { return (int)(PackedValue >> AxShift) & AxMask; }
			set { SetField( value, AxShift, AxMask ); }
		}

		private void SetField( int value, int shift, int mask )
		{
			Debug.Assert( (value & ~mask) == 0 );
			PackedValue = (uint)(PackedValue & ~(mask << shift)) | (uint)(value << shift);
		}

		internal const int BitK = 1 << 8;

		internal const int FieldsPerFlush = 50;

#if DEBUG

		public override string ToString()
		{
			var ret = new StringBuilder();

			ret.Append( OpCode.ToString() );

			switch( OpCode )
			{
			case OpCode.Move:
				ret.AppendFormat( ": R{0} = R{1}", A, B );
				break;

			case OpCode.LoadBool:
				ret.AppendFormat( ": R{0} = {1}", A, B != 0 );
				if( C != 0 )
					ret.Append( "; PC++" );
				break;

			case OpCode.LoadConstant:
				ret.AppendFormat( ": R{0} = K{1}", A, Bx );
				break;

			case OpCode.LoadConstantEx:
				ret.AppendFormat( ": R{0} = K...", A );
				break;

			case OpCode.LoadNil:
				ret.AppendFormat( B == 0 ? ": R{0} = nil" : ": R{0}..R{1} = nil", A, A + B );
				break;

			case OpCode.GetTable:
				ret.AppendFormat( ": R{0} = R{1}[{2}]", A, B, Rk( C ) );
				break;

			case OpCode.SetTable:
				ret.AppendFormat( ": R{0}[{1}] = {2}", A, Rk( B ), Rk( C ) );
				break;	

			case OpCode.GetUpValue:
				ret.AppendFormat( ": R{0} = U{1}", A, B );
				break;

			case OpCode.SetUpValue:
				ret.AppendFormat( ": U{1} = R{0}", A, B );
				break;

			case OpCode.GetUpValueTable:
				ret.AppendFormat( ": R{0} = U{1}[{2}]", A, B, Rk( C ) );
				break;

			case OpCode.SetUpValueTable:
				ret.AppendFormat( ": U{0}[{1}] = {2}", A, Rk( B ), Rk( C ) );
				break;

			case OpCode.Self:
				ret.AppendFormat( ": R{0} = R{1}, R{2} = R{1}[{3}]", A + 1, B, A, Rk( C ) );
				break;

			case OpCode.NewTable:
				ret.AppendFormat( ": R{0} = NewTable( nArr={1}, nNod={2} )", A, Helpers.FbToInt( B ), Helpers.FbToInt( C ) );
				break;

			case OpCode.Add:
			case OpCode.Sub:
			case OpCode.Mul:
			case OpCode.Div:
			case OpCode.Mod:
			case OpCode.Pow:
				ret.AppendFormat( ": R{0} = {1} {3} {2}", A, Rk( B ), Rk( C ), GetArithOp( OpCode ) );
				break;

			case OpCode.Eq:
			case OpCode.Lt:
			case OpCode.Le:
				ret.AppendFormat( ": if( {0} {2} {1} ) PC++", Rk( B ), Rk( C ), GetCmpOp( OpCode, A != 0 ) );
				break;

			case OpCode.Jmp:
				ret.Append( ":" );
				if( SBx != 0 )
					ret.AppendFormat( " PC {1}= {0}", Math.Abs( SBx ), SBx > 0 ? "+" : "-" );
				if( A != 0 )
					ret.AppendFormat( " CloseUpVals( R# >= {0} )", A + 1 );
				break;

			case OpCode.TForLoop:
				ret.AppendFormat( ": if( R{1} != nil ) {{ R{0} = R{1}, PC {3}= {2} }}", A, A + 1, Math.Abs( SBx ), SBx > 0 ? "+" : "-" );
				break;

			case OpCode.TForCall:
				if( C == 0 )
					ret.AppendFormat( ": R{0}( R{1}, R{2} )", A, A + 1, A + 2 );
				else if( C == 1 )
					ret.AppendFormat( ": R{3} = R{0}( R{1}, R{2} )", A, A + 1, A + 2, A + 3 );
				else
					ret.AppendFormat( ": R{3}..R{4} = R{0}( R{1}, R{2} )", A, A + 1, A + 2, A + 3, A + 3 + C - 1 );
				break;

			case OpCode.Closure:
				ret.AppendFormat( ": R{0} = MakeClosure( P{1} )", A, Bx );
				break;

			case OpCode.Call:
				if( C == 0 )
					ret.AppendFormat( ": R{0}... =", A );
				else if( C == 2 )
					ret.AppendFormat( ": R{0} =", A );
				else if( C > 2 )
					ret.AppendFormat( ": R{0}..R{1} =", A, A + C - 2 );

				ret.AppendFormat( " R{0}", A );

				if( B == 0 )
					ret.AppendFormat( "( R{0}... )", A + 1 );
				else if( B == 1 )
					ret.Append( "()" );
				else if( B == 2 )
					ret.AppendFormat( "( R{0} )", A + 1 );
				else
					ret.AppendFormat( "( R{0}..R{1} )", A + 1, A + 1 + B - 2 );
				break;

			case OpCode.TailCall:
				ret.AppendFormat( ": return R{0}", A );

				if( B == 0 )
					ret.AppendFormat( "( R{0}... )", A + 1 );
				else if( B == 1 )
					ret.Append( "()" );
				else if( B == 2 )
					ret.AppendFormat( "( R{0} )", A + 1 );
				else
					ret.AppendFormat( "( R{0}..R{1} )", A + 1, A + 1 + B - 2 );
				break;

			case OpCode.Return:
				if( B == 0 )
					ret.AppendFormat( ": return R{0}...", A );
				else if( B == 1 )
					ret.Append( ": return" );
				else if( B == 2 )
					ret.AppendFormat( ": return R{0}", A );
				else
					ret.AppendFormat( ": return R{0}..R{1}", A, A + B - 1 );
				break;

			case OpCode.Vararg:
				if( B == 0 )
					ret.AppendFormat( ": R{0}... = ...", A );
				else if( B == 1 )
					ret.AppendFormat( ": ..." );
				else if( B == 2 )
					ret.AppendFormat( ": R{0} = ...", A );
				else
					ret.AppendFormat( ": R{0}..R{1} = ...", A, A + B - 2 );
				break;

			case OpCode.ExtraArg:
				ret.AppendFormat( ": ...{0}", Ax );
				break;
			}

			return ret.ToString();
		}

		private static string GetArithOp( OpCode op )
		{
			switch( op )
			{
			case OpCode.Add: return "+";
			case OpCode.Sub: return "-";
			case OpCode.Mul: return "*";
			case OpCode.Div: return "/";
			case OpCode.Mod: return "%";
			case OpCode.Pow: return "^";
			default: return string.Empty;
			}
		}

		private static string GetCmpOp( OpCode op, bool reverse )
		{
			switch( op )
			{
			case OpCode.Eq: return reverse ? "!=" : "==";
			case OpCode.Lt: return reverse ? ">=" : "<";
			case OpCode.Le: return reverse ? ">" : "<=";
			default: return string.Empty;
			}
		}

		private static string Rk( int i )
		{
			if( (i & BitK) != 0 )
				return "K" + (i & ~BitK).ToString();
			else
				return "R" + i.ToString();
		}
#endif
	}

	/// <remarks>
	/// RK: register if high bit clear, else constant.
	/// 
	/// TableGet( Table, Key ) -> Table[Key]
	/// TableSet( Table, Key, Value )
	/// </remarks>
	[Serializable]
	internal enum OpCode
	{
		/// <summary>
		/// A B, R[A]= R[B]
		/// </summary>
		Move,
		
		/// <summary>
		/// A Bx, R[A] = K[Bx]
		/// </summary>
		LoadConstant,

		/// <summary>
		/// A, R[A] = K[extra arg]
		/// </summary>
		/// <remarks>
		/// the next 'instruction' is always ExtraArg.
		/// </remarks>
		LoadConstantEx,

		/// <summary>
		/// A B C, R[A] = (bool)B; if C -> pc++
		/// </summary>
		LoadBool,

		/// <summary>
		/// A B, R[A..A+B] = nil
		/// </summary>
		LoadNil,

		/// <summary>
		/// A B, R[A] = U[B]
		/// </summary>
		GetUpValue,

		/// <summary>
		/// A B C, R[A] = TableGet( U[B], RK[C] )
		/// </summary>
		GetUpValueTable,

		/// <summary>
		/// A B C, R[A] = GetTable( R[B], RK[C] )
		/// </summary>
		GetTable,

		/// <summary>
		/// A B C, SetTable( U[A], RK[B], RK[C] )
		/// </summary>
		SetUpValueTable,

		/// <summary>
		/// A B, U[B] = R[A]
		/// </summary>
		SetUpValue,
		
		/// <summary>
		/// A B C, SetTable( R[A], RK[B], RK[C]
		/// </summary>
		SetTable,

		/// <summary>
		/// A B C, R[A] = NewTable( FloatByte( B ) array slots, FloatByte( C ) objects )
		/// </summary>
		NewTable,

		/// <summary>
		/// A B C, R[A+1] = R[B]; R[A] = GetTable( R[B], RK[C] )
		/// </summary>
		Self,

		/// <summary>
		/// A B C, R[A] = RK[B] + RK[C]
		/// </summary>
		Add,
		
		/// <summary>
		/// A B C, R[A] = RK[B] - RK[C]
		/// </summary>
		Sub,

		/// <summary>
		/// A B C, R[A] = RK[B] * RK[C]
		/// </summary>
		Mul,

		/// <summary>
		/// A B C, R[A] = RK[B] / RK[C]
		/// </summary>
		Div,

		/// <summary>
		/// A B C, R[A] = RK[B] % RK[C]
		/// </summary>
		Mod,

		/// <summary>
		/// A B C, R[A] = RK[B] ^ RK[C]
		/// </summary>
		Pow,

		/// <summary>
		/// A B, R[A] = -RK[B]
		/// </summary>
		Negate,

		/// <summary>
		/// A B, R[A] = Not( RK[B] )
		/// </summary>
		Not,

		/// <summary>
		/// A B, R[A] = Length( RK[B] )
		/// </summary>
		Len,

		/// <summary>
		/// A B C, R[A] = R[B] .. ... .. R[C]
		/// </summary>
		Concat,

		/// <summary>
		/// A sBx, pc += sBx; if A -> close all upvalues >= R[A] + 1
		/// </summary>
		Jmp,
		
		/// <summary>
		/// A B C, if( (RK[B] == RK[C]) != A ) -> pc++
		/// </summary>
		/// <remarks>
		/// A Jmp must follow.
		/// </remarks>
		Eq,
		
		/// <summary>
		/// A B C, if( (RK[B] &lt; RK[C]) != A ) -> pc++
		/// </summary>
		/// <remarks>
		/// A Jmp must follow.
		/// </remarks>
		Lt,

		/// <summary>
		/// A B C, if( (RK[B] &lt;= RK[C]) != A ) -> pc++
		/// </summary>
		/// <remarks>
		/// A Jmp must follow.
		/// </remarks>
		Le,

		/// <summary>
		/// A C, if( (bool)R[A] != C ) -> pc++
		/// </summary>
		/// <remarks>
		/// A Jmp must follow.
		/// </remarks>
		Test,
		/// <summary>
		/// A B C, if( (bool)R[B] == C ) -> R[A] = R[B] else pc++
		/// </summary>
		/// <remarks>
		/// A Jmp must follow.
		/// </remarks>
		TestSet,

		/// <summary>
		/// A B C, R[A..A+C-2] = Call( R[A], R[A+1..A+B-1] )
		/// </summary>
		/// <remarks>
		/// if (B == 0) then B = top. If (C == 0), then `top' is set to last_result+1,
		/// so next open instruction (OP_CALL, OP_RETURN, OP_SETLIST) may use `top'.
		/// </remarks>
		Call,
		
		/// <summary>
		/// A B, return Call( R[A], R[A+1..A+B-1] )
		/// </summary>
		TailCall,

		/// <summary>
		/// A B, return R[A..A+B-2]
		/// </summary>
		/// <remarks>
		/// if (B == 0) then return up to `top'.
		/// </remarks>
		Return,

		/// <summary>
		/// A sBx, R[A] += R[A+2]; if( R[A] &lt;?= R[A+1] ) -> { pc += sBx, R[A+3] = R[A] }
		/// </summary>
		ForLoop,
		/// <summary>
		/// A sBx, R[A] -= R[A+2]; pc += sBx
		/// </summary>
		ForPrep,

		/// <summary>
		/// A C, R(A+3), ... ,R(A+2+C) := R(A)(R(A+1), R(A+2));
		/// </summary>
		TForCall,
		/// <summary>
		/// A sBx, if R(A+1) ~= nil then { R(A)=R(A+1); pc += sBx }
		/// </summary>
		TForLoop,

		/// <summary>
		/// A B C, R(A)[(C-1)*FPF+i] := R(A+i), 1 &lt;= i &lt;= B
		/// </summary>
		SetList,

		/// <summary>
		/// A Bx, R[A] = Closure( Proto[Bx] )
		/// </summary>
		Closure,

		/// <summary>
		/// A B, R[A..A+B-2] = vararg
		/// </summary>
		/// <remarks>
		/// if (B == 0) then use actual number of varargs and
		/// set top (like in OP_CALL with C == 0).
		/// </remarks>
		Vararg,

		/// <summary>
		/// Ax, Ax is an argument for the previous opcode
		/// </summary>
		ExtraArg,
	}
}
