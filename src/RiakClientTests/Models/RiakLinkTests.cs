// <copyright file="RiakLinkTests.cs" company="Basho Technologies, Inc.">
// Copyright 2011 - OJ Reeves & Jeremiah Peschka
// Copyright 2014 - Basho Technologies, Inc.
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

using NUnit.Framework;
using RiakClient.Models;

namespace RiakClientTests.Models
{
    [TestFixture, UnitTest]
    public class RiakLinkTests
    {
        private static readonly RiakLink Link = new RiakLink("bucket", "key", "tag");

        [Test]
        public void RiakLinkCanBeCreatedFromJsonString()
        {
            const string jsonString = @"[""bucket"", ""key"", ""tag""]";
            var jsonLink = RiakLink.FromJsonString(jsonString);

            Assert.AreEqual(Link, jsonLink);
        }
    }
}
