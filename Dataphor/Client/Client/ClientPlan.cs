﻿/*
	Alphora Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.ServiceModel;
using System.Collections.Generic;

using Alphora.Dataphor.DAE;
using Alphora.Dataphor.DAE.Contracts;

namespace Alphora.Dataphor.DAE.Client
{
	public abstract class ClientPlan : ClientObject, IRemoteServerPlan
	{
		public ClientPlan(ClientProcess clientProcess, PlanDescriptor planDescriptor)
		{
			_clientProcess = clientProcess;
			_planDescriptor = planDescriptor;
		}
		
		private ClientProcess _clientProcess;
		public ClientProcess ClientProcess { get { return _clientProcess; } }
		
		protected IClientDataphorService GetServiceInterface()
		{
			return _clientProcess.ClientSession.ClientConnection.ClientServer.GetServiceInterface();
		}
		
		protected PlanDescriptor _planDescriptor;
		public PlanDescriptor PlanDescriptor { get { return _planDescriptor; } }
		
		public int PlanHandle { get { return _planDescriptor.Handle; } }
		
		#region IRemoteServerPlan Members

		public IRemoteServerProcess Process
		{
			get { return _clientProcess; }
		}

		private Exception[] _messages;
		public Exception[] Messages
		{
			get 
			{ 
				if (_messages == null)
					_messages = DataphorFaultUtility.FaultsToExceptions(_planDescriptor.Messages).ToArray();
				return _messages;
			}
		}

		#endregion

		#region IServerPlanBase Members

		public Guid ID
		{
			get { return _planDescriptor.ID; }
		}

		public void CheckCompiled()
		{
			throw new NotImplementedException();
		}

		public PlanStatistics PlanStatistics
		{
			get { return _planDescriptor.Statistics; }
		}

		protected ProgramStatistics _programStatistics = new ProgramStatistics();
		public ProgramStatistics ProgramStatistics { get { return _programStatistics; } }

		#endregion
	}

	public class ClientStatementPlan : ClientPlan, IRemoteServerStatementPlan
	{
		public ClientStatementPlan(ClientProcess clientProcess, PlanDescriptor planDescriptor) : base(clientProcess, planDescriptor) { }
		
		#region IRemoteServerStatementPlan Members

		public void Execute(ref RemoteParamData paramsValue, out ProgramStatistics executeTime, ProcessCallInfo callInfo)
		{
			try
			{
				IAsyncResult result = GetServiceInterface().BeginExecutePlan(PlanHandle, callInfo, paramsValue, null, null);
				result.AsyncWaitHandle.WaitOne();
				ExecuteResult executeResult = GetServiceInterface().EndExecutePlan(result);
				executeTime = executeResult.ExecuteTime;
				paramsValue.Data = executeResult.ParamData;
			}
			catch (FaultException<DataphorFault> fault)
			{
				throw DataphorFaultUtility.FaultToException(fault.Detail);
			}
		}

		#endregion
	}

	public class ClientExpressionPlan : ClientPlan, IRemoteServerExpressionPlan
	{
		public ClientExpressionPlan(ClientProcess clientProcess, PlanDescriptor planDescriptor) : base(clientProcess, planDescriptor) { }
		
		#region IRemoteServerExpressionPlan Members

		public byte[] Evaluate(ref RemoteParamData paramsValue, out ProgramStatistics executeTime, ProcessCallInfo callInfo)
		{
			try
			{
				IAsyncResult result = GetServiceInterface().BeginEvaluatePlan(PlanHandle, callInfo, paramsValue, null, null);
				result.AsyncWaitHandle.WaitOne();
				EvaluateResult evaluateResult = GetServiceInterface().EndEvaluatePlan(result);
				executeTime = evaluateResult.ExecuteTime;
				_programStatistics = executeTime;
				paramsValue.Data = evaluateResult.ParamData;
				return evaluateResult.Result;
			}
			catch (FaultException<DataphorFault> fault)
			{
				throw DataphorFaultUtility.FaultToException(fault.Detail);
			}
		}

		public IRemoteServerCursor Open(ref RemoteParamData paramsValue, out ProgramStatistics executeTime, ProcessCallInfo callInfo)
		{
			try
			{
				IAsyncResult result = GetServiceInterface().BeginOpenPlanCursor(PlanHandle, callInfo, paramsValue, null, null);
				result.AsyncWaitHandle.WaitOne();
				CursorResult cursorResult = GetServiceInterface().EndOpenPlanCursor(result);
				executeTime = cursorResult.ExecuteTime;
				_programStatistics = executeTime;
				paramsValue.Data = cursorResult.ParamData;
				return new ClientCursor(this, cursorResult.CursorDescriptor);
			}
			catch (FaultException<DataphorFault> fault)
			{
				throw DataphorFaultUtility.FaultToException(fault.Detail);
			}
		}

		public IRemoteServerCursor Open(ref RemoteParamData paramsValue, out ProgramStatistics executeTime, out Guid[] bookmarks, int count, out RemoteFetchData fetchData, ProcessCallInfo callInfo)
		{
			try
			{
				IAsyncResult result = GetServiceInterface().BeginOpenPlanCursorWithFetch(PlanHandle, callInfo, paramsValue, count, null, null);
				result.AsyncWaitHandle.WaitOne();
				CursorWithFetchResult cursorResult = GetServiceInterface().EndOpenPlanCursorWithFetch(result);
				executeTime = cursorResult.ExecuteTime;
				_programStatistics = executeTime;
				paramsValue.Data = cursorResult.ParamData;
				bookmarks = cursorResult.Bookmarks;
				fetchData = cursorResult.FetchData;
				return new ClientCursor(this, cursorResult.CursorDescriptor);
			}
			catch (FaultException<DataphorFault> fault)
			{
				throw DataphorFaultUtility.FaultToException(fault.Detail);
			}
		}

		public void Close(IRemoteServerCursor cursor, ProcessCallInfo callInfo)
		{
			try
			{
				IAsyncResult result = GetServiceInterface().BeginCloseCursor(((ClientCursor)cursor).CursorHandle, callInfo, null, null);
				result.AsyncWaitHandle.WaitOne();
				GetServiceInterface().EndCloseCursor(result);
			}
			catch (FaultException<DataphorFault> fault)
			{
				throw DataphorFaultUtility.FaultToException(fault.Detail);
			}
		}

		#endregion

		#region IServerCursorBehavior Members

		public CursorCapability Capabilities
		{
			get { return _planDescriptor.Capabilities; }
		}

		public CursorType CursorType
		{
			get { return _planDescriptor.CursorType; }
		}

		public bool Supports(CursorCapability capability)
		{
			return (Capabilities & capability) != 0;
		}

		public CursorIsolation Isolation
		{
			get { return _planDescriptor.CursorIsolation; }
		}

		#endregion
	}
}
