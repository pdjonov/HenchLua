﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuaSharp
{
	/// <summary>
	/// Nicely wraps up both closures and callbacks.
	/// </summary>
	public struct Callable
	{
		internal object Val;

		internal static bool IsCallable( object obj )
		{
			return false;
		}
	}
}