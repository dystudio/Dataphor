﻿/*
	Alphora Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.Collections.Generic;

using Alphora.Dataphor.DAE;
using Alphora.Dataphor.DAE.Contracts;
using Alphora.Dataphor.DAE.Debug;
using Alphora.Dataphor.DAE.Streams;
using Alphora.Dataphor.DAE.Runtime;
using System.IO;

namespace Alphora.Dataphor.DAE.Client
{
	public class ClientStream : StreamBase, IRemoteStream
	{
		public ClientStream(ClientProcess AClientProcess, int AStreamHandle)
		{
			FClientProcess = AClientProcess;
			FStreamHandle = AStreamHandle;
		}
		
		private ClientProcess FClientProcess;
		public ClientProcess ClientProcess { get { return FClientProcess; } }
		
		private IClientDataphorService GetServiceInterface()
		{
			return FClientProcess.ClientSession.ClientConnection.ClientServer.GetServiceInterface();
		}
		
		private int FStreamHandle;
		public int StreamHandle { get { return FStreamHandle; } }
		
		public override long Length
		{
			get
			{
				IAsyncResult LResult = GetServiceInterface().BeginGetStreamLength(FStreamHandle, null, null);
				LResult.AsyncWaitHandle.WaitOne();
				return GetServiceInterface().EndGetStreamLength(LResult);
			}
		}
		
		public override void SetLength(long AValue)
		{
			IAsyncResult LResult = GetServiceInterface().BeginSetStreamLength(FStreamHandle, AValue, null, null);
			LResult.AsyncWaitHandle.WaitOne();
			GetServiceInterface().EndSetStreamLength(LResult);
		}
		
		public override long Position
		{
			get
			{
				IAsyncResult LResult = GetServiceInterface().BeginGetStreamPosition(FStreamHandle, null, null);
				LResult.AsyncWaitHandle.WaitOne();
				return GetServiceInterface().EndGetStreamPosition(LResult);
			}
			set
			{
				IAsyncResult LResult = GetServiceInterface().BeginSetStreamPosition(FStreamHandle, value, null, null);
				LResult.AsyncWaitHandle.WaitOne();
				GetServiceInterface().EndSetStreamPosition(LResult);
			}
		}
		
		public override int Read(byte[] ABuffer, int AOffset, int ACount)
		{
			IAsyncResult LResult = GetServiceInterface().BeginReadStream(FStreamHandle, ACount, null, null);
			LResult.AsyncWaitHandle.WaitOne();
			byte[] LBytes = GetServiceInterface().EndReadStream(LResult);
			Array.Copy(LBytes, 0, ABuffer, AOffset, LBytes.Length); // ?? Should the buffer bytes beyond the read bytes be cleared?
			return LBytes.Length;
		}
		
		public override void Write(byte[] ABuffer, int AOffset, int ACount)
		{
			byte[] LBytes = new byte[ACount];
			Array.Copy(ABuffer, AOffset, LBytes, 0, ACount);
			IAsyncResult LResult = GetServiceInterface().BeginWriteStream(FStreamHandle, LBytes, null, null);
			LResult.AsyncWaitHandle.WaitOne();
			GetServiceInterface().EndWriteStream(LResult);
		}

		#region IRemoteStream Members

		long IRemoteStream.Length
		{
			get { return Length; }
		}

		void IRemoteStream.SetLength(long AValue)
		{
			SetLength(AValue);
		}

		long IRemoteStream.Position
		{
			get { return Position; }
			set { Position = value; }
		}

		void IRemoteStream.Flush()
		{
			Flush();
		}

		bool IRemoteStream.CanRead
		{
			get { return CanRead; }
		}

		bool IRemoteStream.CanSeek
		{
			get { return CanSeek; }
		}

		bool IRemoteStream.CanWrite
		{
			get { return CanWrite; }
		}

		int IRemoteStream.Read(byte[] ABuffer, int AOffset, int ACount)
		{
			return Read(ABuffer, AOffset, ACount);
		}

		int IRemoteStream.ReadByte()
		{
			return ReadByte();
		}

		long IRemoteStream.Seek(long AOffset, SeekOrigin AOrigin)
		{
			return Seek(AOffset, AOrigin);
		}

		void IRemoteStream.Write(byte[] ABuffer, int AOffset, int ACount)
		{
			Write(ABuffer, AOffset, ACount);
		}

		void IRemoteStream.WriteByte(byte AByte)
		{
			WriteByte(AByte);
		}

		void IRemoteStream.Close()
		{
			Close();
		}

		#endregion

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			Close();
		}

		#endregion
	}
}