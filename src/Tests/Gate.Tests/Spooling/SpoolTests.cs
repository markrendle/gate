﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gate.Spooling;
using NUnit.Framework;

// ReSharper disable InconsistentNaming

namespace Gate.Tests.Spooling
{
    [TestFixture]
    public class SpoolTests
    {
        static ArraySegment<byte> Data(int count)
        {
            return new ArraySegment<byte>(new byte[count], 0, count);
        }

        static ArraySegment<byte> Data(int count, string fill)
        {
            var data = Data(count);
            var fillBytes = Encoding.UTF8.GetBytes(fill);
            var offset = data.Offset;
            while (offset < data.Count)
            {
                var copy = Math.Min(data.Count - offset, fillBytes.Length);
                Array.Copy(fillBytes, 0, data.Array, offset, copy);
                offset += copy;
            }
            return data;
        }

        void AssertDataEqual(ArraySegment<byte> data, ArraySegment<byte> equals)
        {
            Assert.That(data.Offset, Is.EqualTo(equals.Offset));
            Assert.That(data.Count, Is.EqualTo(equals.Count));

            var d1 = data.Array.Skip(data.Offset).Take(data.Count);
            var d2 = equals.Array.Skip(equals.Offset).Take(equals.Count);
            var diffs = d1.Zip(d2, (b1, b2) => new {b1, b2})
                .Select((bb, i) => new {bb.b1, bb.b2, i})
                .Where(bb => bb.b1 != bb.b2);
            Assert.That(!diffs.Any(), diffs.Count() + " differences " + diffs.Take(10).Aggregate("", (str, bb) => str + "\r\n[" + bb.i + "] actual " + bb.b1 + " != expected " + bb.b2));
        }

        [Test]
        public void Pushing_data_with_no_continuation_is_synchronous()
        {
            var spool = new Spool();
            var async = spool.Push(Data(100), null);
            Assert.That(async, Is.False);
        }

        [Test]
        public void Pushing_data_with_continuation_is_asynchronous()
        {
            var spool = new Spool();
            var async = spool.Push(Data(100), () => { });
            Assert.That(async, Is.True);
        }

        [Test]
        public void Pulling_data_when_spooled_is_synchronous()
        {
            var spool = new Spool();
            var asyncPush = spool.Push(Data(100, "hello"), null);
            Assert.That(asyncPush, Is.False);

            var data = Data(100);
            var asyncPull = spool.Pull(data, () => { });
            Assert.That(asyncPull, Is.False);

            AssertDataEqual(data, Data(100, "hello"));
        }

        [Test]
        public void Pulling_before_pushing_is_asynchronous()
        {
            var spool = new Spool();
            var data = Data(100);
            var callbackPull = false;
            var asyncPull = spool.Pull(data, () => callbackPull = true);
            Assert.That(asyncPull, Is.True);
            Assert.That(callbackPull, Is.False);

            var asyncPush = spool.Push(Data(100, "hello"), null);
            Assert.That(asyncPush, Is.False);
            Assert.That(callbackPull, Is.True);

            AssertDataEqual(data, Data(100, "hello"));
        }

        [Test]
        public void Pushing_async_before_pulling_async()
        {
            var spool = new Spool();
            var data = Data(100);

            var callbackPush = false;
            var asyncPush = spool.Push(Data(100, "hello"), () => callbackPush = true);
            Assert.That(asyncPush, Is.True);
            Assert.That(callbackPush, Is.False);

            var callbackPull = false;
            var asyncPull = spool.Pull(data, () => callbackPull = true);
            Assert.That(asyncPull, Is.False);
            Assert.That(callbackPull, Is.False);
            Assert.That(callbackPush, Is.True);

            AssertDataEqual(data, Data(100, "hello"));
        }

        [Test]
        public void Pushing_odd_sizes_completes_partially()
        {
            var spool = new Spool();            

            var callbackPushOne = false;
            var asyncPushOne = spool.Push(Data(100, "hello"), () => callbackPushOne = true);
            Assert.That(asyncPushOne, Is.True);
            Assert.That(callbackPushOne, Is.False);

            var dataOne = Data(50);
            var callbackPullOne = false;
            var asyncPullOne = spool.Pull(dataOne, () => callbackPullOne = true);
            Assert.That(asyncPullOne, Is.False);
            Assert.That(callbackPullOne, Is.False);
            Assert.That(callbackPushOne, Is.False);

            AssertDataEqual(dataOne, Data(50, "hello"));

            var dataTwo = Data(100);
            var callbackPullTwo = false;
            var asyncPullTwo = spool.Pull(dataTwo, () => callbackPullTwo = true);
            Assert.That(asyncPullTwo, Is.True);
            Assert.That(callbackPullTwo, Is.False);
            Assert.That(callbackPushOne, Is.True);

            var callbackPushTwo = false;
            var asyncPushTwo = spool.Push(Data(50, "hello"), () => callbackPushTwo = true);
            Assert.That(asyncPushTwo, Is.False);
            Assert.That(callbackPushTwo, Is.False);
            Assert.That(callbackPullTwo, Is.True);
            
            AssertDataEqual(dataTwo, Data(100, "hello"));

            //final state
            Assert.That(asyncPushOne, Is.True);
            Assert.That(callbackPushOne, Is.True);
            Assert.That(asyncPullOne, Is.False);
            Assert.That(callbackPullOne, Is.False);
            Assert.That(asyncPullTwo, Is.True);
            Assert.That(callbackPullTwo, Is.True);
            Assert.That(asyncPushTwo, Is.False);
            Assert.That(callbackPushTwo, Is.False);
        }
    }
}