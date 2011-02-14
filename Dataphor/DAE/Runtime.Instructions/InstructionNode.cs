/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

//#define USECLEANUPNODES // Part of cleanup processing, currently disabled because cleanup processing is done with InstructionNodeBase.CleanupOperand

using System;
using System.Reflection;
using System.Reflection.Emit;

using Alphora.Dataphor.DAE.Language;
using Alphora.Dataphor.DAE.Language.D4;
using Alphora.Dataphor.DAE.Compiling;
using Alphora.Dataphor.DAE.Server;
using Alphora.Dataphor.DAE.Streams;
using Alphora.Dataphor.DAE.Runtime;
using Alphora.Dataphor.DAE.Runtime.Data;
using Schema = Alphora.Dataphor.DAE.Schema;

namespace Alphora.Dataphor.DAE.Runtime.Instructions
{
	/*
		InstructionNode prepares the AArguments set for use in virtual resolution.

		Passing an argument by value (in or no modifier) is a copy.  For local table
		variables this is accomplished as it is for all other variable types, by
		executing the argument node. For non-local table variables, this is accomplished
		by converting the result to a local table variable for passing into the operator.

		Passing an argument by reference (var, out or const) is not a copy.  For local
		table variables this is accomplished as it is for all other variable types, by 
		referencing the object from the current stack frame in the argument set.
		Non-local table variables cannot be passed by reference.
	*/
	public abstract class InstructionNodeBase : PlanNode
	{
        // constructor
        public InstructionNodeBase() : base() 
        {
			// InstructionNodes are by default not breakable (because for 99% of instructions this would be the
			// equivalent of breaking into the multiplication instruction in the processor, for example.
			//IsBreakable = true;
        }
        
		// Operator
		// The operator this node is implementing
		[Reference]
		private Schema.Operator _operator;
		public Schema.Operator Operator
		{
			get { return _operator; }
			set { _operator = value; }
		}
		
		public override Statement EmitStatement(EmitMode mode)
		{		
			if (_operator.IsBuiltin && (NodeCount > 0) && (NodeCount <= 2))
			{
				Expression expression;
				if (Nodes.Count == 1)
					expression =
						new UnaryExpression
						(
							_operator.OperatorName, 
							_operator.Operands[0].Modifier == Modifier.Var ? 
								new ParameterExpression(Modifier.Var, (Expression)Nodes[0].EmitStatement(mode)) : 
								(Expression)Nodes[0].EmitStatement(mode)
						);
				else
					expression =
						new BinaryExpression
						(
							_operator.Operands[0].Modifier == Modifier.Var ?
								new ParameterExpression(Modifier.Var, (Expression)Nodes[0].EmitStatement(mode)) :
								(Expression)Nodes[0].EmitStatement(mode), 
							_operator.OperatorName, 
							_operator.Operands[1].Modifier == Modifier.Var ?
								new ParameterExpression(Modifier.Var, (Expression)Nodes[1].EmitStatement(mode)) :
								(Expression)Nodes[1].EmitStatement(mode)
						);
				expression.Modifiers = Modifiers;
				return expression;
			}
			else
			{
				CallExpression expression = new CallExpression();
				expression.Identifier = Schema.Object.EnsureRooted(_operator.OperatorName);
				for (int index = 0; index < NodeCount; index++)
					if (_operator.Operands[index].Modifier == Modifier.Var)
						expression.Expressions.Add(new ParameterExpression(Modifier.Var, (Expression)Nodes[index].EmitStatement(mode)));
					else
						expression.Expressions.Add((Expression)Nodes[index].EmitStatement(mode));
				expression.Modifiers = Modifiers;
				return expression;
			}
		}
		
		#if USECLEANUPNODES
		protected PlanNodes FCleanupNodes;
		public PlanNodes CleanupNodes { get { return FCleanupNodes; } }
		#endif
		
		public override void DetermineCharacteristics(Plan plan)
		{
			if (Modifiers != null)
			{
				_isLiteral = Convert.ToBoolean(LanguageModifiers.GetModifier(Modifiers, "IsLiteral", Operator.IsLiteral.ToString()));
				_isFunctional = Convert.ToBoolean(LanguageModifiers.GetModifier(Modifiers, "IsFunctional", Operator.IsFunctional.ToString()));
				_isDeterministic = Convert.ToBoolean(LanguageModifiers.GetModifier(Modifiers, "IsDeterministic", Operator.IsDeterministic.ToString()));
				_isRepeatable = Convert.ToBoolean(LanguageModifiers.GetModifier(Modifiers, "IsRepeatable", Operator.IsRepeatable.ToString()));
				_isNilable = Convert.ToBoolean(LanguageModifiers.GetModifier(Modifiers, "IsNilable", Operator.IsNilable.ToString()));
			}
			else
			{
				_isLiteral = Operator.IsLiteral;
				_isFunctional = Operator.IsFunctional;
				_isDeterministic = Operator.IsDeterministic;
				_isRepeatable = Operator.IsRepeatable;
				_isNilable = Operator.IsNilable;
			}

			for (int index = 0; index < Operator.Operands.Count; index++)
			{
				_isLiteral = _isLiteral && Nodes[index].IsLiteral;
				_isFunctional = _isFunctional && Nodes[index].IsFunctional;
				_isDeterministic = _isDeterministic && Nodes[index].IsDeterministic;
				_isRepeatable = _isRepeatable && Nodes[index].IsRepeatable;
				_isNilable = _isNilable || Nodes[index].IsNilable;
			} 
			
			_isOrderPreserving = Convert.ToBoolean(MetaData.GetTag(Operator.MetaData, "DAE.IsOrderPreserving", _isOrderPreserving.ToString()));
		}
		
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			_dataType = Operator.ReturnDataType;
			
			for (int index = 0; index < Operator.Operands.Count; index++)
			{
				switch (Operator.Operands[index].Modifier)
				{
					case Modifier.In : break;
					
					case Modifier.Var :
						if (Nodes[index] is ParameterNode)
							Nodes[index] = Nodes[index].Nodes[0];
							
						if (!(Nodes[index] is StackReferenceNode) || plan.Symbols[((StackReferenceNode)Nodes[index]).Location].IsConstant)
							throw new CompilerException(CompilerException.Codes.ConstantObjectCannotBePassedByReference, plan.CurrentStatement(), Operator.Operands[index].Name);

						((StackReferenceNode)Nodes[index]).ByReference = true;
					break;

					case Modifier.Const :
						if (Nodes[index] is ParameterNode)
							Nodes[index] = Nodes[index].Nodes[0];
							
						if (Nodes[index] is StackReferenceNode)
							((StackReferenceNode)Nodes[index]).ByReference = true;
						else if (Nodes[index] is StackColumnReferenceNode)
							((StackColumnReferenceNode)Nodes[index]).ByReference = true;
					break;
				}
			}
		}
		
		public override void InternalDetermineBinding(Plan plan)
		{
			base.InternalDetermineBinding(plan);
			
			#if USECLEANUPNODES
			if (CleanupNodes != null)
				foreach (PlanNode node in CleanupNodes)
					node.DetermineBinding(APlan);
			#endif
		}
		
		public override void BindToProcess(Plan plan)
		{
			if (Operator != null)
			{
				plan.CheckRight(Operator.GetRight(Schema.RightNames.Execute));
				plan.EnsureApplicationTransactionOperator(Operator);
			}
			base.BindToProcess(plan);
		}
		
		protected virtual void EmitInstructionIL(Plan plan, ILGenerator generator, int[] executePath, LocalBuilder arguments)
		{
		}

		public override void EmitIL(Plan plan, ILGenerator generator, int[] executePath)
		{
			if (ShouldEmitIL)
			{
				int[] localExecutePath = PrepareExecutePath(plan, executePath);
				
				LocalBuilder arguments = generator.DeclareLocal(typeof(object[]));
				
				generator.Emit(OpCodes.Ldc_I4, NodeCount);
				generator.Emit(OpCodes.Newarr, typeof(object));
				generator.Emit(OpCodes.Stloc, arguments);
				
				for (int index = 0; index < Nodes.Count; index++)
				{
					generator.Emit(OpCodes.Ldloc, arguments);
					generator.Emit(OpCodes.Ldc_I4, index);

					EmitEvaluate(plan, generator, localExecutePath, index);
					
					generator.Emit(OpCodes.Stelem_Ref);
				}
				
				#if USECLEANUPNODES
				if (FCleanupNodes != null)
					AGenerator.BeginExceptionBlock();
				#endif
				
				EmitInstructionIL(plan, generator, executePath, arguments);
				
				#if USECLEANUPNODES
				if (FCleanupNodes != null)
				{
					AGenerator.BeginFinallyBlock();

					for (int index = 0; index < FCleanupNodes.Count; index++)
					{
						FCleanupNodes[index].EmitIL(APlan, AGenerator, AExecutePath);
						
						// Pop the return value if necessary
						if (FCleanupNodes[index].DataType != null)
							AGenerator.Emit(OpCodes.Pop);
					}

					AGenerator.EndExceptionBlock();
				}
				#endif
			}
			else
				base.EmitIL(plan, generator, executePath);
		}
		
		// TODO: This should be compiled and added as cleanup nodes
		// TODO: Handle var arguments for more than just stack references
		protected void CleanupOperand(Program program, Schema.SignatureElement signatureElement, PlanNode argumentNode, object argumentValue)
		{
			switch (signatureElement.Modifier)
			{
				case Modifier.In :
					DataValue.DisposeValue(program.ValueManager, argumentValue);
				break;
					
				case Modifier.Var :
					if (argumentNode.DataType is Schema.ScalarType)
						argumentValue = ValueUtility.ValidateValue(program, (Schema.ScalarType)argumentNode.DataType, argumentValue, _operator);
					program.Stack.Poke(((StackReferenceNode)argumentNode).Location, argumentValue);
				break;
			}
		}
		
		public override string Category
		{
			get { return "Instruction"; }
		}
	}
	
	public abstract class NilaryInstructionNode : InstructionNodeBase
	{
		public abstract object NilaryInternalExecute(Program program);

		public override object InternalExecute(Program program)
		{
			#if USECLEANUPNODES
			try
			{
			#endif
				
				if (IsBreakable)
				{
				    program.Stack.PushWindow(0, this, Operator.Locator);
				    try
				    {
				        return NilaryInternalExecute(program);
				    }
				    finally
				    {
				        program.Stack.PopWindow();
				    }
				}
				else
				{
					return NilaryInternalExecute(program);
				}

			#if USECLEANUPNODES
			}
			finally
			{
				if (FCleanupNodes != null)
					for (int index = 0; index < FCleanupNodes.Count; index++)
						FCleanupNodes[index].Execute(AProgram);
			}
			#endif
		}
	}
	
	public abstract class UnaryInstructionNode : InstructionNodeBase
	{
		public abstract object InternalExecute(Program program, object argument1);
		
		public override object InternalExecute(Program program)
		{
			object argument = Nodes[0].Execute(program);
			try
			{
				if (IsBreakable)
				{
				    program.Stack.PushWindow(0, this, Operator.Locator);
				    try
				    {
				        return InternalExecute(program, argument);
				    }
				    finally
				    {
				        program.Stack.PopWindow();
				    }
				}
				else
				{
					return InternalExecute(program, argument);
				}
			}
			finally
			{
				if (Operator != null)
					CleanupOperand(program, Operator.Signature[0], Nodes[0], argument);
				
				#if USECLEANUPNODES
				if (FCleanupNodes != null)
					for (int index = 0; index < FCleanupNodes.Count; index++)
						FCleanupNodes[index].Execute(AProgram);
				#endif
			}
		}
	}
	
	public abstract class BinaryInstructionNode : InstructionNodeBase
	{
		public abstract object InternalExecute(Program program, object argument1, object argument2);
		
		public override object InternalExecute(Program program)
		{
			object argument1 = Nodes[0].Execute(program);
			object argument2 = Nodes[1].Execute(program);
			try
			{
				if (IsBreakable)
				{
				    program.Stack.PushWindow(0, this, Operator.Locator);
				    try
				    {
				        return InternalExecute(program, argument1, argument2);
				    }
				    finally
				    {
				        program.Stack.PopWindow();
				    }
				}
				else
				{
					return InternalExecute(program, argument1, argument2);
				}
			}
			finally
			{
				if (Operator != null) 
				{
					CleanupOperand(program, Operator.Signature[0], Nodes[0], argument1);
					CleanupOperand(program, Operator.Signature[1], Nodes[1], argument2);
				}
					
				#if USECLEANUPNODES
				if (FCleanupNodes != null)
					for (int index = 0; index < FCleanupNodes.Count; index++)
						FCleanupNodes[index].Execute(AProgram);
				#endif
			}
		}
	}
	
	public abstract class TernaryInstructionNode : InstructionNodeBase
	{
		public abstract object InternalExecute(Program program, object argument1, object argument2, object argument3);
		
		public override object InternalExecute(Program program)
		{
			object argument1 = Nodes[0].Execute(program);
			object argument2 = Nodes[1].Execute(program);
			object argument3 = Nodes[2].Execute(program);
			try
			{
				if (IsBreakable)
				{
				    program.Stack.PushWindow(0, this, Operator.Locator);
				    try
				    {
				        return InternalExecute(program, argument1, argument2, argument3);
				    }
				    finally
				    {
				        program.Stack.PopWindow();
				    }
				}
				else
				{
					return InternalExecute(program, argument1, argument2, argument3);
				}
			}
			finally
			{
				if (Operator != null) 
				{
					CleanupOperand(program, Operator.Signature[0], Nodes[0], argument1);
					CleanupOperand(program, Operator.Signature[1], Nodes[1], argument2);
					CleanupOperand(program, Operator.Signature[2], Nodes[2], argument3);
				}
					
				#if USECLEANUPNODES
				if (FCleanupNodes != null)
					for (int index = 0; index < FCleanupNodes.Count; index++)
						FCleanupNodes[index].Execute(AProgram);
				#endif
			}
		}
	}
	
	public abstract class InstructionNode : InstructionNodeBase
	{
		public abstract object InternalExecute(Program program, object[] arguments);

		public override object InternalExecute(Program program)
		{
			object[] arguments = new object[NodeCount];
			for (int index = 0; index < arguments.Length; index++)
				arguments[index] = Nodes[index].Execute(program);
			try
			{
				if (IsBreakable)
				{
				    program.Stack.PushWindow(0, this, Operator.Locator);
				    try
				    {
				        return InternalExecute(program, arguments);
				    }
				    finally
				    {
				        program.Stack.PopWindow();
				    }
				}
				else
				{
					return InternalExecute(program, arguments);
				}
			}
			finally
			{
				// TODO: Compile the dispose calls and var argument assignment calls
				if (Operator != null)
					for (int index = 0; index < Operator.Operands.Count; index++)
						CleanupOperand(program, Operator.Signature[index], Nodes[index], arguments[index]);

				#if USECLEANUPNODES
				if (FCleanupNodes != null)
					for (int index = 0; index < FCleanupNodes.Count; index++)
						FCleanupNodes[index].Execute(AProgram);
				#endif
			}
		}
	}
}

