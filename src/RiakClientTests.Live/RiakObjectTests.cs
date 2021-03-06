// <copyright file="RiakObjectTests.cs" company="Basho Technologies, Inc.">
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


namespace RiakClientTests.Live
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ProtoBuf;
    using RiakClient;
    using RiakClient.Extensions;
    using RiakClient.Models;
    using RiakClient.Models.MapReduce;
    using RiakClient.Models.MapReduce.Inputs;

    public class RiakObjectTestBase : LiveRiakConnectionTestBase
    {
        protected const string Jeremiah = "jeremiah";
        protected const string OJ = "oj";
        protected const string Brent = "brent";
        protected const string Rob = "rob";

        [ProtoContract]
        protected class Person
        {
            [ProtoMember(1)]
            public string Name { get; set; }
            [ProtoMember(2)]
            public string CurrentlyDrinking { get; set; }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        protected void CreateObjects(string bucketName)
        {
            var oj = new RiakObject(bucketName, OJ, new Person() { Name = "oj" });
            var jeremiah = new RiakObject(bucketName, Jeremiah, new Person() { Name = "jeremiah" });
            var brent = new RiakObject(bucketName, Brent, new Person() { Name = "brent" });
            var rob = new RiakObject(bucketName, Rob, new Person() { Name = "rob" });

            oj.ContentType = RiakConstants.ContentTypes.ApplicationJson;
            jeremiah.ContentType = RiakConstants.ContentTypes.ApplicationJson;
            brent.ContentType = RiakConstants.ContentTypes.ApplicationJson;
            rob.ContentType = RiakConstants.ContentTypes.ApplicationJson;

            Client.Put(new[] { oj, jeremiah, brent, rob });
        }

        protected void CreateLinkedObjects(string bucketName)
        {
            var oj = new RiakObject(bucketName, OJ, new Person() { Name = "oj" });
            var jeremiah = new RiakObject(bucketName, Jeremiah, new Person() { Name = "jeremiah" });
            var brent = new RiakObject(bucketName, Brent, new Person() { Name = "brent" });
            var rob = new RiakObject(bucketName, Rob, new Person() { Name = "rob" });

            oj.ContentType = RiakConstants.ContentTypes.ApplicationJson;
            jeremiah.ContentType = RiakConstants.ContentTypes.ApplicationJson;
            brent.ContentType = RiakConstants.ContentTypes.ApplicationJson;
            rob.ContentType = RiakConstants.ContentTypes.ApplicationJson;

#pragma warning disable 618
            oj.LinkTo(jeremiah, "friends");
            oj.LinkTo(jeremiah, "coworkers");

            jeremiah.LinkTo(oj, "friends");
            jeremiah.LinkTo(oj, "coworkers");
            jeremiah.LinkTo(oj, "ozzies");
            jeremiah.LinkTo(brent, "friends");
            jeremiah.LinkTo(brent, "coworkers");
            jeremiah.LinkTo(rob, "ozzies");

            brent.LinkTo(jeremiah, "coworkers");
            brent.LinkTo(jeremiah, "friends");
#pragma warning restore 618
            Client.Put(new[] { oj, jeremiah, brent, rob });
        }
    }

    [TestFixture, IntegrationTest]
    public class WhenSavingObjects : RiakObjectTestBase
    {
        private string Bucket;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            Bucket = System.Guid.NewGuid().ToString();

            CreateObjects(Bucket);

            var props = new RiakBucketProperties().SetAllowMultiple(true);
            Client.SetBucketProperties(Bucket, props);
        }

        [Test]
        public void WriteableVectorClocksCanBeUsedToForceSiblings()
        {
            var oj = Client.Get(Bucket, OJ).Value;
            var vclock = oj.VectorClock;

            var ojTea = oj.GetObject<Person>();
            var ojCoffee = oj.GetObject<Person>();

            ojTea.CurrentlyDrinking = "tea";
            ojCoffee.CurrentlyDrinking = "coffee";

            oj.SetObject(ojTea);
            Client.Put(oj);

            oj.SetObject(ojCoffee);
            Client.Put(oj);

            var multiOj = Client.Get(Bucket, OJ).Value;

            oj.VectorClock.ShouldEqual(vclock);
            oj.VectorClock.ShouldNotEqual(multiOj.VectorClock);

            multiOj.Siblings.Count.ShouldBeGreaterThan(0);
        }
    }

    [TestFixture, IntegrationTest]
    public class WhenCreatingLinks : RiakObjectTestBase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            CreateLinkedObjects(TestBucket);

            var props = new RiakBucketProperties().SetAllowMultiple(false);
            Client.SetBucketProperties(TestBucket, props);
        }

        [Test]
        public void LinkMetadataIsRetrievedWithAnObject()
        {
            var jeremiah = Client.Get(TestBucket, Jeremiah).Value;

            jeremiah.Links.Count.IsAtLeast(4);
        }

        [Test, Ignore("Link walking is deprecated in Riak 2.0 and incompatible with Security")]
        public void RiakObjectLinksAreTheSameAsLinksRetrievedViaMapReduce()
        {
            var jeremiah = Client.Get(TestBucket, Jeremiah).Value;
            var jLinks = jeremiah.Links;

            var input = new RiakBucketKeyInput()
                .Add(new RiakObjectId(TestBucket, Jeremiah));

#pragma warning disable 618
            var query = new RiakMapReduceQuery().Inputs(input).Link(l => l.AllLinks().Keep(true));
#pragma warning restore 618

            var mrResult = Client.MapReduce(query);
            mrResult.IsSuccess.ShouldBeTrue();

            // TODO: FUTURE - Is *this* chunk of code acceptable?
            // This should probably be taken care of in the RiakClient.WalkLinks
            var listOfLinks = mrResult.Value.PhaseResults.OrderBy(pr => pr.Phase)
                .ElementAt(0).Values
                    .Select(v => RiakLink.ParseArrayFromJsonString(v.FromRiakString()));

            var mrLinks = listOfLinks.SelectMany(l => l).ToList();

            mrLinks.Count().ShouldEqual(jLinks.Count);
            foreach (var link in jLinks)
            {
                mrLinks.ShouldContain(link);
            }
        }

        [Test, Ignore("Link walking is deprecated in Riak 2.0 and incompatible with Security")]
        public void LinksAreRetrievedWithAMapReducePhase()
        {
#pragma warning disable 618
            var query = new RiakMapReduceQuery()
                    .Inputs(TestBucket)
                    .Filter(f => f.Matches(Jeremiah))
                    .Link(l => l.Tag("friends").Bucket(TestBucket).Keep(false))
                    .ReduceErlang(r => r.ModFun("riak_kv_mapreduce", "reduce_set_union").Keep(true));
#pragma warning restore 618

            var result = Client.MapReduce(query);
            result.IsSuccess.ShouldBeTrue();

            var mrResult = result.Value;
            mrResult.PhaseResults.ShouldNotBeNull();
            mrResult.PhaseResults.Count().ShouldEqual(2);
        }

        [Test]
        public void LinksAreRemovedSuccessfullyInMemory()
        {
            var jeremiah = Client.Get(TestBucket, Jeremiah).Value;
            var linkToRemove = new RiakLink(TestBucket, OJ, "ozzies");

#pragma warning disable 618
            jeremiah.RemoveLink(linkToRemove);
#pragma warning restore 618

            var ojLinks = new List<RiakLink>
            {
                new RiakLink(TestBucket, OJ, "friends"),
                new RiakLink(TestBucket, OJ, "coworkers")
            };

            jeremiah.Links.ShouldNotContain(linkToRemove);

            ojLinks.ForEach(l => jeremiah.Links.ShouldContain(l));
        }

        [Test]
        public void LinksAreRemovedAfterSaving()
        {
            var jeremiah = Client.Get(TestBucket, Jeremiah).Value;
            var linkToRemove = new RiakLink(TestBucket, OJ, "ozzies");

#pragma warning disable 618
            jeremiah.RemoveLink(linkToRemove);
#pragma warning restore 618

            var result = Client.Put(jeremiah, new RiakPutOptions { ReturnBody = true });
            result.IsSuccess.ShouldBeTrue();

            jeremiah = result.Value;

            var ojLinks = new List<RiakLink>
            {
                new RiakLink(TestBucket, OJ, "friends"),
                new RiakLink(TestBucket, OJ, "coworkers")
            };

            jeremiah.Links.ShouldNotContain(linkToRemove);

            ojLinks.ForEach(l => jeremiah.Links.ShouldContain(l));
        }

        [Test, Ignore("Link walking is deprecated in Riak 2.0 and incompatible with Security")]
        public void LinkWalkingSuccessfullyRetrievesNLevels()
        {
            var oj = Client.Get(TestBucket, OJ).Value;
            var linkPhases = new List<RiakLink>
            {
                RiakLink.AllLinks,
                RiakLink.AllLinks
            };

#pragma warning disable 618
            var linkPeople = Client.WalkLinks(oj, linkPhases);
#pragma warning restore 618
            linkPeople.IsSuccess.ShouldBeTrue();
            linkPeople.Value.Count.ShouldEqual(6);
        }
    }

    [TestFixture, IntegrationTest]
    public class WhenSerializingObjects : RiakObjectTestBase
    {
        [ProtoContract]
        class ProtoBufPerson
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string Name { get; set; }
            [ProtoMember(3)]
            public Address Address { get; set; }
        }
        [ProtoContract]
        class Address
        {
            [ProtoMember(1)]
            public string Line1 { get; set; }
            [ProtoMember(2)]
            public string Line2 { get; set; }
        }

        [Test]
        public void SavedObjectsShouldBeIdentical()
        {
            // create a proto-contract class (https://code.google.com/p/protobuf-net/wiki/GettingStarted)
            //var testPerson = new ProtoBufPerson()
            //{
            //    Id = 42,
            //    Name = "alex",
            //    Address = new Address()
            //    {
            //        Line1 = "16 dusty road",
            //        Line2 = "santa fe, nm 87508"
            //    }
            //};
            const string bucketName = "test";
            var ojPerson = new Person() { Name = "oj", CurrentlyDrinking = "tea" };

            var oj = new RiakObject(bucketName, OJ) { ContentType = RiakConstants.ContentTypes.ProtocolBuffers };
            oj.SetObject(ojPerson);

            // Don't capture result to avoid compiler warning
            Client.Put(oj);

            var getResult = Client.Get(bucketName, OJ);
            var newPerson = getResult.Value.GetObject<Person>();

            ojPerson.Name.ShouldEqual(newPerson.Name);


            var testPerson = new ProtoBufPerson()
            {
                Id = 42,
                Name = "alex",
                Address = new Address()
                {
                    Line1 = "16 dusty road",
                    Line2 = "santa fe, nm 87508"
                }
            };

            var ro = new RiakObject(bucketName, testPerson.Id.ToString());
            ro.ContentType = RiakConstants.ContentTypes.ProtocolBuffers;
            ro.SetObject(testPerson);

            // Don't capture result to avoid compiler warning
            Client.Put(ro);

            var getResult2 = Client.Get(bucketName, testPerson.Id.ToString());
            var testPerson2 = getResult2.Value.GetObject<ProtoBufPerson>();

            testPerson.Id.ShouldEqual(testPerson2.Id);
            testPerson.Name.ShouldEqual(testPerson2.Name);
            testPerson.Address.Line1.ShouldEqual(testPerson2.Address.Line1);
            testPerson.Address.Line2.ShouldEqual(testPerson2.Address.Line2);
        }
    }
}
