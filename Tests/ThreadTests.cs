﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaSharp.Tests
{
	[TestClass]
	public class ThreadTests
	{
		[TestMethod]
		public void Ctor()
		{
			var thread = new Thread();
			Assert.AreEqual( 0, thread.Stack.Top );
		}

		[TestMethod]
		public void MathChunk()
		{
			RunTestScript( "MathChunk.lua", 42 );
		}

		[TestMethod]
		public void SimpleCall()
		{
			RunTestScript( "Call.lua", 42 );
		}

		[TestMethod]
		public void TwoSimpleCalls()
		{
			var thread = new Thread();
			var func = Helpers.LoadFunc( "Call.lua", new Table() );

			thread.Call( func, 0, 1 );

			var stk = thread.Stack;

			Assert.AreEqual( 1, stk.Top );
			Assert.AreEqual( 42, stk[1] );

			stk.Pop();
			Assert.AreEqual( 0, stk.Top );

			thread.Call( func, 0, 1 );

			Assert.AreEqual( 1, stk.Top );
			Assert.AreEqual( 42, stk[1] );
		}

		[TestMethod]
		public void TailCall()
		{
			RunTestScript( "TailCall.lua", 42 );
		}

		[TestMethod]
		public void Vararg()
		{
			RunTestScript( "Vararg.lua", 42, true, false );
		}

		[TestMethod]
		public void SimpleUpValue()
		{
			RunTestScript( "SimpleUpValue.lua", 42 );
		}

		[TestMethod]
		public void ClosedUpValue()
		{
			RunTestScript( "ClosedUpValue.lua", 42 );
		}

		[TestMethod]
		public void ClosedUpValue2()
		{
			RunTestScript( "ClosedUpValue2.lua", 42 );
		}

		[TestMethod]
		public void ClosedUpValue3()
		{
			RunTestScript( "ClosedUpValue3.lua", 42 );
		}

		private static void RunTestScript( string script, params Value[] expectedResults )
		{
			var thread = new Thread();
			var func = Helpers.LoadFunc( script, new Table() );

			thread.Call( func, 0, Thread.CallReturnAll );

			var stk = thread.Stack;

			Assert.AreEqual( expectedResults.Length, stk.Top );
			for( int i = 0; i < expectedResults.Length; i++ )
				Assert.AreEqual( expectedResults[i], stk[i + 1] );
		}
	}
}
