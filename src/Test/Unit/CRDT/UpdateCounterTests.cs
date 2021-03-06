// <copyright file="UpdateCounterTests.cs" company="Basho Technologies, Inc.">
// Copyright 2015 - Basho Technologies, Inc.
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
// </copyright>

namespace Test.Unit.CRDT
{
    using System;
    using System.Text;
    using NUnit.Framework;
    using RiakClient;
    using RiakClient.Commands.CRDT;
    using RiakClient.Messages;

    [TestFixture, UnitTest]
    public class UpdateCounterTests
    {
        private const string BucketType = "counters";
        private const string Bucket = "myBucket";
        private const string Key = "counter_1";
        private static readonly long DefaultIncrement = 10;

        [Test]
        public void Should_Build_DtUpdateReq_Correctly()
        {
            var updateCounterCommandBuilder = new UpdateCounter.Builder(DefaultIncrement);

            var q1 = new Quorum(1);
            var q2 = new Quorum(2);
            var q3 = new Quorum(3);

            updateCounterCommandBuilder
                .WithBucketType(BucketType)
                .WithBucket(Bucket)
                .WithKey(Key)
                .WithW(q3)
                .WithPW(q1)
                .WithDW(q2)
                .WithReturnBody(true)
                .WithIncludeContext(false)
                .WithTimeout(TimeSpan.FromSeconds(20));

            UpdateCounter updateCounterCommand = updateCounterCommandBuilder.Build();

            DtUpdateReq protobuf = (DtUpdateReq)updateCounterCommand.ConstructRequest(false);

            Assert.AreEqual(Encoding.UTF8.GetBytes(BucketType), protobuf.type);
            Assert.AreEqual(Encoding.UTF8.GetBytes(Bucket), protobuf.bucket);
            Assert.AreEqual(Encoding.UTF8.GetBytes(Key), protobuf.key);
            Assert.AreEqual(q3, protobuf.w);
            Assert.AreEqual(q1, protobuf.pw);
            Assert.AreEqual(q2, protobuf.dw);
            Assert.IsTrue(protobuf.return_body);
            Assert.IsFalse(protobuf.include_context);
            Assert.AreEqual(20000, protobuf.timeout);

            CounterOp counterOpMsg = protobuf.op.counter_op;

            Assert.AreEqual(DefaultIncrement, counterOpMsg.increment);
        }

        [Test]
        public void Should_Construct_CounterResponse_From_DtUpdateResp()
        {
            var key = new RiakString("riak_generated_key");
            var context = new RiakString("1234");

            var updateResp = new DtUpdateResp();
            updateResp.key = key;
            updateResp.context = context;
            updateResp.counter_value = DefaultIncrement;

            var update = new UpdateCounter.Builder(DefaultIncrement)
                .WithBucketType(BucketType)
                .WithBucket(Bucket)
                .Build();

            update.OnSuccess(updateResp);

            CounterResponse response = update.Response;

            Assert.NotNull(response);
            Assert.AreEqual(key, response.Key);
            Assert.AreEqual(RiakString.ToBytes(context), response.Context);
            Assert.AreEqual(DefaultIncrement, response.Value);
        }
    }
}