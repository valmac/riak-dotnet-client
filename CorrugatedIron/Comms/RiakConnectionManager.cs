﻿// Copyright (c) 2010 - OJ Reeves & Jeremiah Peschka
//
// This file is provided to you under the Apache License,
// Version 2.0 (the "License"); you may not use this file
// except in compliance with the License.  You may obtain
// a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

using System;
using CorrugatedIron.Collections;
using CorrugatedIron.Config;

namespace CorrugatedIron.Comms
{
    public interface IRiakConnectionManager : IDisposable
    {
        RiakResult<TResult> UseConnection<TResult>(Func<IRiakConnection, RiakResult<TResult>> useFun);
        RiakResult UseConnection(Func<IRiakConnection, RiakResult> useFun);
    }

    public class RiakConnectionManager : IRiakConnectionManager
    {
        private readonly IRiakConnectionConfiguration _connectionConfiguration;
        private readonly ResourcePool<IRiakConnection> _connections;
        private bool _disposing;

        public RiakConnectionManager(IRiakConnectionConfiguration connectionConfiguration)
        {
            _connectionConfiguration = connectionConfiguration;
            _connections = new ResourcePool<IRiakConnection>(connectionConfiguration.PoolSize,
                () => new RiakConnection(connectionConfiguration),
                conn => conn.Dispose());
        }

        public RiakResult UseConnection(Func<IRiakConnection, RiakResult> useFun)
        {
            if (_disposing) return RiakResult.Error("Shutting down");

            var response = _connections.Consume(useFun);
            if (response.Item1)
            {
                return response.Item2;
            }
            return RiakResult.Error();
        }

        public RiakResult<TResult> UseConnection<TResult>(Func<IRiakConnection, RiakResult<TResult>> useFun)
        {
            if (_disposing) return RiakResult<TResult>.Error("Shutting down");

            var response = _connections.Consume(useFun);
            if (response.Item1)
            {
                return response.Item2;
            }
            return RiakResult<TResult>.Error();
        }

        public void Dispose()
        {
            _disposing = true;

            _connections.Dispose();
        }
    }
}
