/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
#define UseReferenceDerivation
#define NILPROPOGATION
	
using System;
using System.Text;
using System.Threading;
using System.Collections;

using Alphora.Dataphor.DAE.Language;
using Alphora.Dataphor.DAE.Language.D4;
using Alphora.Dataphor.DAE.Compiling;
using Alphora.Dataphor.DAE.Server;	
using Alphora.Dataphor.DAE.Runtime;
using Alphora.Dataphor.DAE.Runtime.Data;
using Alphora.Dataphor.DAE.Runtime.Instructions;
using Alphora.Dataphor.DAE.Device.ApplicationTransaction;
using Schema = Alphora.Dataphor.DAE.Schema;

namespace Alphora.Dataphor.DAE.Runtime.Instructions
{
	/*
		Extraction Operators
		    Extract(table{}): row{}
			Extract(table{}, ColumnName): scalar
			Extract(row{}, ColumnName): scalar
			
		// These operators cannot be overloaded, nor can they be invoked by call
	*/
	
	// operator Extract(table{}): row{}
	public class ExtractRowNode : PlanNode
	{		
		public ExtractRowNode() : base()
		{
		}
		
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			TableNode sourceNode = (TableNode)Nodes[0];
			bool hasEmptyKey = false;
			foreach (Schema.Key key in sourceNode.TableVar.Keys)
				if (key.Columns.Count == 0)
				{
					hasEmptyKey = true;
					break;
				}
			
			if (!hasEmptyKey && !plan.SuppressWarnings && !plan.InTypeOfContext)
				plan.Messages.Add(new CompilerException(CompilerException.Codes.InvalidRowExtractorExpression, CompilerErrorLevel.Warning, plan.CurrentStatement()));
			_dataType = sourceNode.TableVar.DataType.RowType;
		}
		
		public override Schema.IDataType DataType { get { return base.DataType; } set { base.DataType = value; } }
		
		public override void DetermineCharacteristics(Plan plan)
		{
			_isLiteral = true;
			_isFunctional = true;
			_isDeterministic = true;
			_isRepeatable = true;
			_isNilable = true;
			for (int index = 0; index < NodeCount; index++)
			{
				_isLiteral = _isLiteral && Nodes[index].IsLiteral;
				_isFunctional = _isFunctional && Nodes[index].IsFunctional;
				_isDeterministic = _isDeterministic && Nodes[index].IsDeterministic;
				_isRepeatable = _isRepeatable && Nodes[index].IsRepeatable;
			} 
		}
		
		public override object InternalExecute(Program program)
		{
			Table table = Nodes[0].Execute(program) as Table;
			#if NILPROPOGATION
			if ((table == null))
				return null;
			#endif
			try
			{
				table.Open();
				if (table.Next())
				{
					Row row = table.Select();
					try
					{
						if (table.Next())
							throw new CompilerException(CompilerException.Codes.InvalidRowExtractorExpression);
						return row;
					}
					catch
					{
						row.Dispose();
						throw;
					}
				}
				else
					#if NILPROPOGATION
					return null;
					#else
					throw new RuntimeException(RuntimeException.Codes.RowTableEmpty);
					#endif
			}
			finally
			{
				table.Dispose();
			}
		}
		
		private D4IndexerExpression _indexerExpression;
		public D4IndexerExpression IndexerExpression
		{
			get { return _indexerExpression; }
			set { _indexerExpression = value; }
		}

		public override Statement EmitStatement(EmitMode mode)
		{
			if (_indexerExpression != null)
				return _indexerExpression;
			else
			{
				D4IndexerExpression indexerExpression = new D4IndexerExpression();
				indexerExpression.Expression = (Expression)Nodes[0].EmitStatement(mode);
				return indexerExpression;
			}
		}
	}

	//	Extract(row{}, ColumnName): scalar
	//  Extract(table{}, ColumnName): scalar
	public class ExtractColumnNode : PlanNode
	{		
		public override void DetermineCharacteristics(Plan plan)
		{
			_isLiteral = true;
			_isFunctional = true;
			_isDeterministic = true;
			_isRepeatable = true;
			_isNilable = true;
			for (int index = 0; index < NodeCount; index++)
			{
				_isLiteral = _isLiteral && Nodes[index].IsLiteral;
				_isFunctional = _isFunctional && Nodes[index].IsFunctional;
				_isDeterministic = _isDeterministic && Nodes[index].IsDeterministic;
				_isRepeatable = _isRepeatable && Nodes[index].IsRepeatable;
			} 
		}
		
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			#if USECOLUMNLOCATIONBINDING
			if (Nodes[0].DataType is Schema.RowType)
				FDataType = ((Schema.RowType)Nodes[0].DataType).Columns[Location].DataType;
			else
				FDataType = ((Schema.TableType)Nodes[0].DataType).Columns[Location].DataType;
			#else
			if (Nodes[0].DataType is Schema.RowType)
			{
				_dataType = ((Schema.RowType)Nodes[0].DataType).Columns[Identifier].DataType;
				if (Nodes[0] is StackReferenceNode)
				{
					_shouldDisposeSource = false;
					((StackReferenceNode)Nodes[0]).ByReference = true;
				}
			}
			else
				_dataType = ((Schema.TableType)Nodes[0].DataType).Columns[Identifier].DataType;
			#endif
		}
		
		#if USECOLUMNLOCATIONBINDING
		public int Location = -1;
		#endif

		public string Identifier = String.Empty;		
		
		private bool _shouldDisposeSource = true;
		
		public override object InternalExecute(Program program)
		{
			object objectValue = Nodes[0].Execute(program);
			#if NILPROPOGATION
			if (objectValue == null)
				return null;
			#endif

			Table table = objectValue as Table;
			if (table != null)
			{
				try
				{
					table.Open();
					if (table.Next())
					{
						Row row = table.Select();
						try
						{
							if (table.Next())
								throw new CompilerException(CompilerException.Codes.InvalidRowExtractorExpression);
							#if USECOLUMNLOCATIONBINDING
							if (row.HasValue(Location))
								return row[Location].AsNative;
							else
								return null;
							#else
							int columnIndex = row.DataType.Columns.IndexOf(Identifier);
							if (row.HasValue(columnIndex))
								return row[columnIndex];
							else
								return null;
							#endif
						}
						finally
						{
							row.Dispose();
						}
					}
					else
						#if NILPROPOGATION
						return null;
						#else
						throw new RuntimeException(RuntimeException.Codes.ColumnTableEmpty);
						#endif
				}
				finally
				{
					if (_shouldDisposeSource)
						table.Dispose();
				}
			}
			else
			{
				Row row = (Row)objectValue;
				try
				{
					#if USECOLUMNLOCATIONBINDING
					if (row.HasValue(Location))
						return row[Location].AsNative;
					else
						return null;
					#else
					int columnIndex = row.DataType.Columns.IndexOf(Identifier);
					if (!row.IsNil && row.HasValue(columnIndex))
						return row[columnIndex];
					else
						return null;
					#endif
				}
				finally
				{
					if (_shouldDisposeSource)
						row.Dispose();
				}
			}
		}
		
		public override Statement EmitStatement(EmitMode mode)
		{
			return new QualifierExpression((Expression)Nodes[0].EmitStatement(mode), new IdentifierExpression(Schema.Object.EnsureUnrooted(Identifier)));
		}
	}
	
    // operator iExists(table) : bool
    public class ExistsNode : InstructionNodeBase
    {
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			_dataType = plan.DataTypes.SystemBoolean;
		}
		
		public override object InternalExecute(Program program)
		{
			Table table = Nodes[0].Execute(program) as Table;
			#if NILPROPOGATION
			if (table == null)
				return null;
			#endif
			try
			{
				table.Open();
				return !table.IsEmpty();
			}
			finally
			{
				table.Dispose();
			}
		}
    }
}
