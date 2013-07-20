﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuaSharp
{
	internal sealed class BoolBox
	{
		public readonly bool Value;
		private BoolBox( bool value )
		{
			Value = value;
		}

		public static readonly BoolBox True = new BoolBox( true );
		public static readonly BoolBox False = new BoolBox( false );

		public override string ToString()
		{
			return Value.ToString();
		}
	}

	internal sealed class NumBox
	{
		public double Value;
		public NumBox( double value )
		{
			this.Value = value;
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}
}
