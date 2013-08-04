﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Debug = System.Diagnostics.Debug;

namespace LuaSharp
{
	public class Thread
	{
		/// <summary>
		/// The minimum amount of stack space available
		/// to C functions when they're called.
		/// </summary>
		public const int MinStack = 20;

		private Value[] stack = new Value[MinStack * 2];
		private int stackTop;

		public struct StackOps
		{
			private Thread owner;

			internal StackOps( Thread owner )
			{
				this.owner = owner;
			}

			public int Top
			{
				get { return owner.stackTop - owner.call.StackBase; }
			}

			public void CheckSpace( int spaceNeeded )
			{
				if( spaceNeeded < 0 )
					throw new ArgumentOutOfRangeException( "spaceNeeded" );

				owner.CheckStack( owner.stackTop + spaceNeeded );
			}

			public void Push( Value value )
			{
				owner.stack[owner.stackTop++] = value;
			}

			public Value this[int index]
			{
				get
				{
					if( index == 0 )
						throw new ArgumentOutOfRangeException( "index" );

					if( index < 0 )
					{
						index = owner.stackTop + index;
						if( index < owner.call.StackBase )
							throw new ArgumentOutOfRangeException( "index" );
					}
					else
					{
						index = owner.call.StackBase + index - 1;
						if( index >= owner.stackTop )
							throw new ArgumentOutOfRangeException( "index" );
					}

					return owner.stack[index];
				}
			}

			public void Pop( int count = 1 )
			{
				if( count < 0 )
					throw new ArgumentOutOfRangeException( "count" );
				if( count == 0 )
					return;

				int newTop = owner.stackTop - count;
				if( newTop < owner.call.StackBase )
					throw new InvalidOperationException( "Can't pop more values than exist on the current frame." );

				owner.stackTop = newTop;
			}
		}

		public StackOps Stack { get { return new StackOps( this ); } }

		private void CheckStack( int minLen )
		{
			if( stack.Length < minLen )
			{
				int growLen = stack.Length;
				growLen = growLen < 256 ? growLen * 2 : growLen + MinStack;
				Array.Resize( ref stack, Math.Max( growLen, minLen ) );
			}
		}

		private CallInfo call; //the top of the call stack
		private CallInfo[] callInfos = new CallInfo[16]; //the rest of the call stack
		private int numCallInfos; //the number of items in the call stack
		
		private struct CallInfo
		{
			public int StackBase;

			/// <summary>
			/// The currently executing op.
			/// </summary>
			public int PC;

			public object Callable;

			/// <summary>
			/// Where to find the function's varargs.
			/// </summary>
			public int VarArgsIndex;

			/// <summary>
			/// Where to stick the results.
			/// </summary>
			public int ResultIndex;
			/// <summary>
			/// How many results we want.
			/// </summary>
			public int ResultCount;
		}

		private void PushCallInfo()
		{
			if( numCallInfos == callInfos.Length )
				Array.Resize( ref callInfos, callInfos.Length + 16 );
			callInfos[numCallInfos++] = call;
		}

		private void PopCallInfo()
		{
			Debug.Assert( numCallInfos != 0 );
			call = callInfos[--numCallInfos];
		}

		/// <summary>
		/// Runs the code at the top of the callstack.
		/// </summary>
		internal void Execute()
		{
			var stackBase = call.StackBase;

			Value[] upValues = null;
			var proto = call.Callable as Proto;

			if( proto == null )
			{
				var asClosure = call.Callable as Closure;
				Debug.Assert( asClosure != null );

				proto = asClosure.Proto;
				upValues = asClosure.UpValues;
			}
			
			var code = proto.Code;
			var consts = proto.Constants;

			for( int pc = call.PC; pc < code.Length; call.PC = ++pc )
			{
				var op = code[pc];

				switch( op.OpCode )
				{
				case OpCode.Move:
					stack[stackBase + op.A] = stack[stackBase + op.B];
					break;

				case OpCode.LoadConstant:
					stack[stackBase + op.A] = consts[op.Bx];
					break;

				case OpCode.LoadConstantEx:
					{
						var op2 = code[pc++];
						Debug.Assert( op2.OpCode == OpCode.ExtraArg );
						stack[stackBase + op.A] = consts[op2.Ax];
					}
					break;

				case OpCode.LoadBool:
					stack[stackBase + op.A].RefVal =
						op.B != 0 ? BoolBox.True : BoolBox.False;
					if( op.C != 0 )
						pc++;
					break;

				case OpCode.LoadNil:
					{
						int a = stackBase + op.A;
						int b = op.B;
						do { stack[a].SetNil(); } while( b-- != 0 );
					}
					break;

				case OpCode.GetUpValue:
					ReadUpValue( ref upValues[op.B], out stack[stackBase + op.A] );
					break;

				case OpCode.GetUpValueTable:
					{
						int c = op.C;
						var key = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						Value upVal;
						ReadUpValue( ref upValues[op.B], out upVal );

						var table = (Table)upVal.RefVal;
						GetTable( table, ref key, out stack[stackBase + op.A] );
					}
					break;

				case OpCode.GetTable:
					{
						int c = op.C;
						var key = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						GetTable( stack[stackBase + op.B].RefVal,
							ref key, out stack[stackBase + op.A] );
					}
					break;

				case OpCode.SetUpValueTable:
					{
						int b = op.B;
						var key = (b & Instruction.BitK) != 0 ?
							consts[b & ~Instruction.BitK] :
							stack[stackBase + b];

						int c = op.C;
						var value = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						Value upVal;
						ReadUpValue( ref upValues[op.A], out upVal );

						var table = (Table)upVal.RefVal;
						SetTable( table, ref key, ref value );
					}
					break;

				case OpCode.SetUpValue:
					WriteUpValue( ref upValues[op.B], ref stack[stackBase + op.A] );
					break;

				case OpCode.SetTable:
					{
						int b = op.B;
						var key = (b & Instruction.BitK) != 0 ?
							consts[b & ~Instruction.BitK] :
							stack[stackBase + b];

						int c = op.C;
						var value = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						SetTable( stack[stackBase + op.A].RefVal,
							ref key, ref value );
					}
					break;

				case OpCode.NewTable:
					{
						int nArr = Helpers.FbToInt( op.B );
						int nNod = Helpers.FbToInt( op.C );

						stack[stackBase + op.A].RefVal = new Table( nArr, nNod );
					}
					break;

				case OpCode.Self:
					{
						stack[stackBase + op.A + 1] = stack[stackBase + op.B];
						var table = stack[stackBase + op.B].RefVal;

						int c = op.C;
						var key = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						GetTable( table, ref key, out stack[stackBase + op.A] );
					}
					break;

				case OpCode.Add:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						var ic = op.C;
						var c = (ic & Instruction.BitK) != 0 ?
							consts[ic & ~Instruction.BitK] :
							stack[stackBase + ic];

						if( b.RefVal == Value.NumTypeTag &&
							c.RefVal == Value.NumTypeTag )
						{
							stack[stackBase + op.A].Set( b.NumVal + c.NumVal );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Add, b.RefVal, c.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Sub:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						var ic = op.C;
						var c = (ic & Instruction.BitK) != 0 ?
							consts[ic & ~Instruction.BitK] :
							stack[stackBase + ic];

						if( b.RefVal == Value.NumTypeTag &&
							c.RefVal == Value.NumTypeTag )
						{
							stack[stackBase + op.A].Set( b.NumVal - c.NumVal );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Sub, b.RefVal, c.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Mul:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						var ic = op.C;
						var c = (ic & Instruction.BitK) != 0 ?
							consts[ic & ~Instruction.BitK] :
							stack[stackBase + ic];

						if( b.RefVal == Value.NumTypeTag &&
							c.RefVal == Value.NumTypeTag )
						{
							stack[stackBase + op.A].Set( b.NumVal * c.NumVal );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Mul, b.RefVal, c.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Div:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						var ic = op.C;
						var c = (ic & Instruction.BitK) != 0 ?
							consts[ic & ~Instruction.BitK] :
							stack[stackBase + ic];

						if( b.RefVal == Value.NumTypeTag &&
							c.RefVal == Value.NumTypeTag )
						{
							stack[stackBase + op.A].Set( b.NumVal / c.NumVal );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Div, b.RefVal, c.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Mod:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						var ic = op.C;
						var c = (ic & Instruction.BitK) != 0 ?
							consts[ic & ~Instruction.BitK] :
							stack[stackBase + ic];

						if( b.RefVal == Value.NumTypeTag &&
							c.RefVal == Value.NumTypeTag )
						{
							//yes, this is the correct mod formula
							stack[stackBase + op.A].Set( b.NumVal % c.NumVal );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Mod, b.RefVal, c.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Pow:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						var ic = op.C;
						var c = (ic & Instruction.BitK) != 0 ?
							consts[ic & ~Instruction.BitK] :
							stack[stackBase + ic];

						if( b.RefVal == Value.NumTypeTag &&
							c.RefVal == Value.NumTypeTag )
						{
							stack[stackBase + op.A].Set( Math.Pow( b.NumVal, c.NumVal ) );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Add, b.RefVal, c.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Negate:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK] :
							stack[stackBase + ib];

						if( b.RefVal == Value.NumTypeTag )
						{
							stack[stackBase + op.A].Set( -b.NumVal );
						}
						else
						{
							DoArith( TypeInfo.TagMethod_Unm, b.RefVal, b.RefVal,
								out stack[stackBase + op.A] );
						}
					}
					break;

				case OpCode.Not:
					{
						var ib = op.B;
						var b = (ib & Instruction.BitK) != 0 ?
							consts[ib & ~Instruction.BitK].RefVal :
							stack[stackBase + ib].RefVal;

						var res = b == null || b == BoolBox.False;
						stack[stackBase + op.A].RefVal = res ? BoolBox.True : BoolBox.False;
					}
					break;

				case OpCode.Len:
					throw new NotImplementedException();

				case OpCode.Concat:
					throw new NotImplementedException();

				case OpCode.Jmp:
					{
						var a = op.A;
						if( a != 0 )
							CloseUpValues( stackBase + a - 1 );
						pc += op.SBx;
					}
					break;

				case OpCode.Eq:
					{
						int b = op.B;
						var bv = (b & Instruction.BitK) != 0 ?
							consts[b & ~Instruction.BitK] :
							stack[stackBase + b];

						int c = op.C;
						var cv = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						bool test =
							(bv.RefVal == cv.RefVal &&
							(bv.RefVal != Value.NumTypeTag || bv.NumVal == cv.NumVal)) ||
							Equal( ref bv, ref cv );

						if( test != (op.A != 0) )
						{
							pc++;
						}
						else
						{
							op = code[pc++];
							Debug.Assert( op.OpCode == OpCode.Jmp );
							goto case OpCode.Jmp;
						}
					}
					break;

				case OpCode.Lt:
					{
						int b = op.B;
						var bv = (b & Instruction.BitK) != 0 ?
							consts[b & ~Instruction.BitK] :
							stack[stackBase + b];

						int c = op.C;
						var cv = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						bool test =
							(bv.RefVal == Value.NumTypeTag &&
							cv.RefVal == Value.NumTypeTag &&
							bv.NumVal < cv.NumVal) ||
							Less( ref bv, ref cv );

						if( test != (op.A != 0) )
						{
							pc++;
						}
						else
						{
							op = code[pc++];
							Debug.Assert( op.OpCode == OpCode.Jmp );
							goto case OpCode.Jmp;
						}
					}
					break;

				case OpCode.Le:
					{
						int b = op.B;
						var bv = (b & Instruction.BitK) != 0 ?
							consts[b & ~Instruction.BitK] :
							stack[stackBase + b];

						int c = op.C;
						var cv = (c & Instruction.BitK) != 0 ?
							consts[c & ~Instruction.BitK] :
							stack[stackBase + c];

						bool test =
							(bv.RefVal == Value.NumTypeTag &&
							cv.RefVal == Value.NumTypeTag &&
							bv.NumVal <= cv.NumVal) ||
							LessEqual( ref bv, ref cv );

						if( test != (op.A != 0) )
						{
							pc++;
						}
						else
						{
							op = code[pc++];
							Debug.Assert( op.OpCode == OpCode.Jmp );
							goto case OpCode.Jmp;
						}
					}
					break;

				case OpCode.Test:
					{
						var a = stack[stackBase + op.A].RefVal;
						var test = a == null || a == BoolBox.False;

						if( (op.C != 0) == test )
						{
							pc++;
						}
						else
						{
							op = code[pc++];
							Debug.Assert( op.OpCode == OpCode.Jmp );
							goto case OpCode.Jmp;
						}
					}
					break;

				case OpCode.TestSet:
					{
						var b = stack[stackBase + op.B].RefVal;
						var test = b == null || b == BoolBox.False;

						if( (op.C != 0) == test )
						{
							pc++;
						}
						else
						{
							stack[stackBase + op.A] = stack[stackBase + op.B];

							op = code[pc++];
							Debug.Assert( op.OpCode == OpCode.Jmp );
							goto case OpCode.Jmp;
						}
					}
					break;

				case OpCode.Call:
					{
						int funcIdx = stackBase + op.A;
						object func = stack[funcIdx].RefVal;

						int numArgs = op.B - 1;
						if( numArgs == -1 )
							numArgs = stackTop - funcIdx - 1;

						int numRetVals = op.C - 1;

						call.PC++; //return to the next instruction
						BeginCall( funcIdx, numArgs, numRetVals ); //valid because CallReturnAll == -1

						Execute();

						PopCallInfo();
					}
					break;

				case OpCode.TailCall:
					{
						//ToDo: actually properly implement the tail call...

						if( proto.InnerProtos != null )
							CloseUpValues( stackBase );

						int funcIdx = stackBase + op.A;
						object func = stack[funcIdx].RefVal;

						int numArgs = op.B - 1;
						if( numArgs == -1 )
							numArgs = stackTop - funcIdx - 1;

						int resultIndex = call.ResultIndex;

						call.PC++; //return to the next instruction
						BeginCall( funcIdx, numArgs, call.ResultCount );
						call.ResultIndex = resultIndex;

						Execute();

						PopCallInfo();

						pc = code.Length;
					}
					break;

				case OpCode.Return:
					{
						if( proto.InnerProtos != null )
							CloseUpValues( stackBase );

						var a = stackBase + op.A;
						var b = op.B;

						int numRet =  b != 0 ? b - 1 : stackTop - a;

						var retIdx = call.ResultIndex;
						var retCount = call.ResultCount;
						
						if( retCount != CallReturnAll && numRet > retCount )
							numRet = retCount;

						if( retIdx != a )
						{
							for( int i = 0; i < numRet; i++ )
								stack[retIdx + i] = stack[a + i];
						}

						if( retCount == CallReturnAll )
						{
							stackTop = retIdx + numRet;
						}
						else
						{
							for( int i = numRet; i < retCount; i++ )
								stack[retIdx + i].RefVal = null;
						}

						pc = code.Length;
					}
					break;

				case OpCode.ForLoop:
					{
						var ai = stackBase + op.A;

						Debug.Assert( stack[ai + 0].RefVal == Value.NumTypeTag );
						Debug.Assert( stack[ai + 1].RefVal == Value.NumTypeTag );
						Debug.Assert( stack[ai + 2].RefVal == Value.NumTypeTag );

						var idx = stack[ai].NumVal;
						var limit = stack[ai + 1].NumVal;
						var step = stack[ai + 2].NumVal;

						idx += step;

						if( step < 0 ? idx >= limit : idx <= limit )
						{
							pc += op.SBx;
							stack[ai].NumVal = idx;
							stack[ai + 3].Set( idx );
						}
					}
					break;

				case OpCode.ForPrep:
					throw new NotImplementedException();

				case OpCode.TForCall:
					throw new NotImplementedException();
					goto case OpCode.TForLoop;

				case OpCode.TForLoop:
					{
						int ia = stackBase + op.A;
						if( stack[ia + 1].RefVal != null )
						{
							stack[ia] = stack[ia + 1];
							pc += op.SBx;
						}
					}
					break;

				case OpCode.SetList:
					{
						var ia = stackBase + op.A;

						var n = op.B;
						var c = op.C;

						if( n == 0 )
							n = stackTop - ia - 1;

						if( c == 0 )
						{
							var opEx = code[pc++];
							Debug.Assert( opEx.OpCode == OpCode.ExtraArg );
							c = opEx.Ax;
						}

						var table = (Table)stack[ia];

						var last = (c - 1) * Instruction.FieldsPerFlush + n;

						if( last > table.ArrayCapacity )
							table.Resize( last, table.NodeCapacity );
						var tableArray = table.array;

						for( ; n > 0; n-- )
							tableArray[--last].Set( ref stack[ia + n] );

						//stackTop = call.Top;
					}
					break;

				case OpCode.Closure:
					stack[stackBase + op.A].RefVal =
						CreateClosure( proto.InnerProtos[op.Bx], upValues );
					break;

				case OpCode.Vararg:
					{
						int srcIdx = call.VarArgsIndex;
						int destIdx = stackBase + op.A;

						int numVarArgs = call.StackBase - srcIdx;
						int numWanted = op.B - 1;

						if( numWanted == -1 )
						{
							numWanted = numVarArgs;
							stackTop = destIdx + numWanted;
						}

						int numHad = numWanted < numVarArgs ? numWanted : numVarArgs;
						for( int i = 0; i < numHad; i++ )
							stack[destIdx + i] = stack[srcIdx + i];

						for( int i = numHad; i < numWanted; i++ )
							stack[destIdx + i].RefVal = null;
					}
					break;

				default:
					throw new InvalidBytecodeException();
				}
			}
		}

		private void BeginCall( int funcIdx, int numArgs, int numResults )
		{
			var callable = stack[funcIdx].RefVal;

			if( callable == null )
				throw new ArgumentNullException( "Attempt to call a nil value." );

			var asFunc = callable as Function;
			if( asFunc != null )
			{
				var proto = asFunc as Proto;
				if( proto == null )
				{
					var asClosure = asFunc as Closure;
					if( asClosure != null )
						proto = asClosure.Proto;
				}

				if( proto == null )
					throw new ArgumentException( "Attempting to call a non-callable object." );

				int numVarArgs = 0;
				if( proto.HasVarArgs )
					numVarArgs = Math.Max( numArgs - proto.NumParams, 0 );

				int newStackBase = funcIdx + 1;

				if( numVarArgs != 0 )
					newStackBase += numArgs;

				//if( newStackBase < 0 || (call.Callable != null && newStackBase < call.StackBase) )
					//throw new ArgumentException( "Fewer args provided than expected.", "numArgs" );

				//if( numResults != CallReturnAll && stackTop - numArgs + numResults > stack.Length )
					//throw new ArgumentException( "This call would overflow the stack." );

				int newStackTop = newStackBase + proto.MaxStack;
				CheckStack( newStackTop );

				if( numVarArgs != 0 )
				{
					//got at least proto.NumParams on the stack
					//move them to the right spot

					for( int i = 0; i < proto.NumParams; i++ )
					{
						int srcIdx = funcIdx + 1 + i;

						stack[newStackBase + i] = stack[srcIdx];
						stack[srcIdx].RefVal = null;
					}
				}
				else
				{
					//complete the missing args

					for( int i = numArgs; i < proto.NumParams; i++ )
						stack[newStackBase + i].RefVal = null;
				}

				PushCallInfo();

				call.Callable = callable;
				call.StackBase = newStackBase;

				call.PC = 0;

				call.ResultIndex = funcIdx;
				call.ResultCount = numResults;

				call.VarArgsIndex = newStackBase - numVarArgs;

				return;
			}

			throw new NotImplementedException();
		}

		private void GetTable( object obj, ref Value key, out Value value )
		{
			//ToDo: this should be a full get

			var table = obj as Table;

			int loc = table.FindValue( key );
			table.ReadValue( loc, out value );
		}

		private void SetTable( object obj, ref Value key, ref Value value )
		{
			//ToDo: this should be a full set

			var table = obj as Table;

			int loc = table.FindValue( key );

			if( loc == 0 )
				loc = table.InsertNewKey( new CompactValue( key ) );

			table.WriteValue( loc, ref value );
		}

		private void DoArith( String opName, object a, object b, out Value ret )
		{
			throw new NotImplementedException();
		}

		private bool Equal( ref Value a, ref Value b )
		{
			throw new NotImplementedException();
		}

		private bool Less( ref Value a, ref Value b )
		{
			throw new NotImplementedException();
		}

		private bool LessEqual( ref Value a, ref Value b )
		{
			throw new NotImplementedException();
		}

		private void ReadUpValue( ref Value upVal, out Value ret )
		{
			if( upVal.RefVal == Value.OpenUpValueTag )
			{
				ret = stack[(int)upVal.NumVal];
				return;
			}

			var asClosed = upVal.RefVal as ValueBox;
			if( asClosed != null )
			{
				ret = asClosed.Value;
				return;
			}

			//simple value
			ret = upVal;
		}

		private void WriteUpValue( ref Value upVal, ref Value value )
		{
			if( upVal.RefVal == Value.OpenUpValueTag )
			{
				stack[(int)upVal.NumVal] = value;
				return;
			}

			var asClosed = upVal.RefVal as ValueBox;
			if( asClosed != null )
			{
				asClosed.Value = value;
				return;
			}

			//simple value
			upVal = value;
		}

		private struct OpenUpValue
		{
			public Value[] UpValueStorage;
			public int UpValueIndex;
			public int StackIndex;
		}

		private OpenUpValue[] openUpValues = new OpenUpValue[32];
		private int numOpenUpValues;

		internal void RegisterOpenUpvalue( Value[] storage, int index )
		{
			if( numOpenUpValues == openUpValues.Length )
				Array.Resize( ref openUpValues, openUpValues.Length + 32 );

			Debug.Assert( storage[index].RefVal == Value.OpenUpValueTag );

			OpenUpValue rec;
			rec.UpValueStorage = storage;
			rec.UpValueIndex = index;
			rec.StackIndex = (int)storage[index].NumVal;

			Debug.Assert( numOpenUpValues == 0 ||
				openUpValues[numOpenUpValues - 1].StackIndex <= rec.StackIndex );

			openUpValues[numOpenUpValues++] = rec;
		}

		/// <summary>
		/// Closes all open upvalues pointing to stack locations >= index.
		/// </summary>
		private void CloseUpValues( int index )
		{
			int i;
			for( i = numOpenUpValues - 1; i >= 0; i-- )
			{
				var rec = openUpValues[i];

				if( rec.StackIndex < index )
					break;

				var stk = stack[rec.StackIndex];

				var asUpValRef = stk.RefVal as Value[];
				if( asUpValRef == null )
				{
					//values initially close to simple values

					rec.UpValueStorage[rec.UpValueIndex] = stk;

					if( !(stk.RefVal is ValueBox) )
					{
						//keep track of where we closed this value to
						//if another open value closes on this slot, we
						//need to properly box it and fix both references

						stack[rec.StackIndex].RefVal = rec.UpValueStorage;
						stack[rec.StackIndex].NumVal = rec.UpValueIndex;
					}
				}
				else
				{
					//the value's already been closed to a simple value

					int upIdx = (int)stk.NumVal;
					var box = new ValueBox { Value = asUpValRef[upIdx] };
					
					asUpValRef[upIdx].RefVal = box;
					rec.UpValueStorage[rec.UpValueIndex].RefVal = box;

					//we don't want to go through this path each time
					//so we put the box on the stack, any further upvalues
					//closing on this slot will take the short path above

					stack[rec.StackIndex].RefVal = box;
				}

				openUpValues[i].UpValueStorage = null;
			}

			//restore the original stack values (ToDo: find out if this is necessary)

			for( int j = i + 1; j < numOpenUpValues; j++ )
			{
				var stkIdx = openUpValues[j].StackIndex;
				var stk = stack[stkIdx];

				var asUpValRef = stk.RefVal as Value[];
				if( asUpValRef != null )
				{
					stack[stkIdx] = asUpValRef[(int)stk.NumVal];
					continue;
				}

				var asBox = stk.RefVal as ValueBox;
				if( asBox != null )
				{
					stack[stkIdx] = asBox.Value;
					continue;
				}
			}

			//and... done!

			numOpenUpValues = i + 1;
		}

		private Function CreateClosure( Proto proto, Value[] parentUpValues )
		{
			var stackBase = call.StackBase;

			var upValDesc = proto.UpValues;
			if( upValDesc == null )
				//we don't wrap simple protos in full closures
				return proto;

			var upValues = new Value[upValDesc.Length];
			for( int i = 0; i < upValDesc.Length; i++ )
			{
				var desc = upValDesc[i];
				if( desc.InStack )
				{
					//create an open upvalue
					upValues[i].RefVal = Value.OpenUpValueTag;
					upValues[i].NumVal = stackBase + desc.Index;

					RegisterOpenUpvalue( upValues, i );
				}
				else
				{
					upValues[i] = parentUpValues[desc.Index];
					if( upValues[i].RefVal == Value.OpenUpValueTag )
						RegisterOpenUpvalue( upValues, i );
				}
			}

			return new Closure() { Proto = proto, UpValues = upValues };
		}

		public const int CallReturnAll = -1;

		public void Call( int numArgs, int numResults )
		{
			//var stk = Stack;

			throw new NotImplementedException();
		}

		public void Call( Function func, int numArgs, int numResults )
		{
			if( func == null )
				throw new ArgumentNullException( "func" );
			if( numArgs < 0 )
				throw new ArgumentOutOfRangeException( "numArgs" );
			if( numResults < 0 && numResults != CallReturnAll )
				throw new ArgumentOutOfRangeException( "numResults" );
			
			var proto = func as Proto;

			if( proto == null )
			{
				var asClosure = func as Closure;
				if( asClosure == null )
					throw new ArgumentException( "Unsupported func type." );
				proto = asClosure.Proto;
			}

			int newStackBase = stackTop - numArgs;
			if( newStackBase < 0 || (call.Callable != null && newStackBase < call.StackBase) )
				throw new ArgumentException( "Fewer args provided than expected.", "numArgs" );

			if( numResults != CallReturnAll && stackTop - numArgs + numResults > stack.Length )
				throw new ArgumentException( "This call would overflow the stack." );

			int newStackTop = newStackBase + proto.MaxStack;
			CheckStack( newStackTop );

			PushCallInfo();

			call.Callable = func;
			call.StackBase = newStackBase;

			call.ResultIndex = newStackBase;
			call.ResultCount = numResults;

			Execute();

			if( numResults != CallReturnAll )
				stackTop = call.StackBase + numResults;

			PopCallInfo();
		}
	}
}
