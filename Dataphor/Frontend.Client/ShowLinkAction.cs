﻿/*
	Alphora Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.ComponentModel;

namespace Alphora.Dataphor.Frontend.Client
{
	public abstract class ShowLinkAction : Action, IShowLinkAction
	{
		// URL

		string FURL = String.Empty;
		
		[DefaultValue("")]
		[Description("A URL pointing to the web page to be loaded.")]
		public string URL
		{
			get	{ return FURL; }
			set	{ FURL = (new Uri(value)).AbsoluteUri;	}
		}
	}
}
