/*
	Dataphor
	© Copyright 2000-2015 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
using System;
using System.Reflection;
	
using Alphora.Dataphor;
using Alphora.Dataphor.BOP;
using Alphora.Dataphor.DAE.Streams;
using Alphora.Dataphor.DAE.Runtime.Instructions;
using Schema = Alphora.Dataphor.DAE.Schema;

namespace Alphora.Dataphor.FHIR.Core
{
	class DAERegister
	{
		protected const string D4ClassDefinitionNameSpace = "FHIRCore";

		public static SettingsList GetClasses()
		{
			SettingsList classes = new SettingsList();
			
			Type[] types = typeof(DAERegister).Assembly.GetTypes();

			foreach (Type type in types)
			{
				// Node
				if (type.IsSubclassOf(typeof(InstructionNodeBase)))
					classes.Add(new SettingsItem(String.Format("{0}.{1}", D4ClassDefinitionNameSpace, Schema.Object.Unqualify(type.Name)), type.AssemblyQualifiedName));
				
				// Conveyor
				if (type.IsSubclassOf(typeof(Conveyor)))
					classes.Add(new SettingsItem(String.Format("{0}.{1}", D4ClassDefinitionNameSpace, Schema.Object.Unqualify(type.Name)), type.AssemblyQualifiedName));
				
				// DeviceOperator
				if (type.IsSubclassOf(typeof(Schema.DeviceOperator)))
					classes.Add(new SettingsItem(String.Format("{0}.{1}", D4ClassDefinitionNameSpace, Schema.Object.Unqualify(type.Name)), type.AssemblyQualifiedName));
					
				// DeviceScalarType
				if (type.IsSubclassOf(typeof(Schema.DeviceScalarType)))
					classes.Add(new SettingsItem(String.Format("{0}.{1}", D4ClassDefinitionNameSpace, Schema.Object.Unqualify(type.Name)), type.AssemblyQualifiedName));
			}
			
			return classes;
		}
	}
}