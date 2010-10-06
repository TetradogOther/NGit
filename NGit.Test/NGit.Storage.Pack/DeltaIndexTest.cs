using NGit;
using NGit.Junit;
using NGit.Storage.Pack;
using NUnit.Framework;
using Sharpen;

namespace NGit.Storage.Pack
{
	public class DeltaIndexTest : TestCase
	{
		private TestRng rng;

		private ByteArrayOutputStream actDeltaBuf;

		private ByteArrayOutputStream expDeltaBuf;

		private DeltaEncoder expDeltaEnc;

		private byte[] src;

		private byte[] dst;

		private ByteArrayOutputStream dstBuf;

		/// <exception cref="System.Exception"></exception>
		protected override void SetUp()
		{
			base.SetUp();
			rng = new TestRng(Sharpen.Extensions.GetTestName(this));
			actDeltaBuf = new ByteArrayOutputStream();
			expDeltaBuf = new ByteArrayOutputStream();
			expDeltaEnc = new DeltaEncoder(expDeltaBuf, 0, 0);
			dstBuf = new ByteArrayOutputStream();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestInsertWholeObject_Length12()
		{
			src = rng.NextBytes(12);
			Insert(src);
			DoTest();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestCopyWholeObject_Length128()
		{
			src = rng.NextBytes(128);
			Copy(0, 128);
			DoTest();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestCopyWholeObject_Length123()
		{
			src = rng.NextBytes(123);
			Copy(0, 123);
			DoTest();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestCopyZeros_Length128()
		{
			src = new byte[2048];
			Copy(0, src.Length);
			DoTest();
			// The index should be smaller than expected due to the chain
			// being truncated. Without truncation we would expect to have
			// more than 3584 bytes used.
			//
			NUnit.Framework.Assert.AreEqual(2636, new DeltaIndex(src).GetIndexSize());
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestShuffleSegments()
		{
			src = rng.NextBytes(128);
			Copy(64, 64);
			Copy(0, 64);
			DoTest();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestInsertHeadMiddle()
		{
			src = rng.NextBytes(1024);
			Insert("foo");
			Copy(0, 512);
			Insert("yet more fooery");
			Copy(0, 512);
			DoTest();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestInsertTail()
		{
			src = rng.NextBytes(1024);
			Copy(0, 512);
			Insert("bar");
			DoTest();
		}

		public virtual void TestIndexSize()
		{
			src = rng.NextBytes(1024);
			DeltaIndex di = new DeltaIndex(src);
			NUnit.Framework.Assert.AreEqual(1860, di.GetIndexSize());
			NUnit.Framework.Assert.AreEqual("DeltaIndex[2 KiB]", di.ToString());
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestLimitObjectSize_Length12InsertFails()
		{
			src = rng.NextBytes(12);
			dst = src;
			DeltaIndex di = new DeltaIndex(src);
			NUnit.Framework.Assert.IsFalse(di.Encode(actDeltaBuf, dst, src.Length));
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestLimitObjectSize_Length130InsertFails()
		{
			src = rng.NextBytes(130);
			dst = rng.NextBytes(130);
			DeltaIndex di = new DeltaIndex(src);
			NUnit.Framework.Assert.IsFalse(di.Encode(actDeltaBuf, dst, src.Length));
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestLimitObjectSize_Length130CopyOk()
		{
			src = rng.NextBytes(130);
			Copy(0, 130);
			dst = dstBuf.ToByteArray();
			DeltaIndex di = new DeltaIndex(src);
			NUnit.Framework.Assert.IsTrue(di.Encode(actDeltaBuf, dst, dst.Length));
			byte[] actDelta = actDeltaBuf.ToByteArray();
			byte[] expDelta = expDeltaBuf.ToByteArray();
			NUnit.Framework.Assert.AreEqual(BinaryDelta.Format(expDelta, false), BinaryDelta.
				Format(actDelta, false));
		}

		//
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestLimitObjectSize_Length130CopyFails()
		{
			src = rng.NextBytes(130);
			Copy(0, 130);
			dst = dstBuf.ToByteArray();
			// The header requires 4 bytes for these objects, so a target length
			// of 5 is bigger than the copy instruction and should cause an abort.
			//
			DeltaIndex di = new DeltaIndex(src);
			NUnit.Framework.Assert.IsFalse(di.Encode(actDeltaBuf, dst, 5));
			NUnit.Framework.Assert.AreEqual(4, actDeltaBuf.Size());
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestLimitObjectSize_InsertFrontFails()
		{
			src = rng.NextBytes(130);
			Insert("eight");
			Copy(0, 130);
			dst = dstBuf.ToByteArray();
			// The header requires 4 bytes for these objects, so a target length
			// of 5 is bigger than the copy instruction and should cause an abort.
			//
			DeltaIndex di = new DeltaIndex(src);
			NUnit.Framework.Assert.IsFalse(di.Encode(actDeltaBuf, dst, 5));
			NUnit.Framework.Assert.AreEqual(4, actDeltaBuf.Size());
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void Copy(int offset, int len)
		{
			dstBuf.Write(src, offset, len);
			expDeltaEnc.Copy(offset, len);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void Insert(string text)
		{
			Insert(Constants.Encode(text));
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void Insert(byte[] text)
		{
			dstBuf.Write(text);
			expDeltaEnc.Insert(text);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void DoTest()
		{
			dst = dstBuf.ToByteArray();
			DeltaIndex di = new DeltaIndex(src);
			di.Encode(actDeltaBuf, dst);
			byte[] actDelta = actDeltaBuf.ToByteArray();
			byte[] expDelta = expDeltaBuf.ToByteArray();
			NUnit.Framework.Assert.AreEqual(BinaryDelta.Format(expDelta, false), BinaryDelta.
				Format(actDelta, false));
			//
			NUnit.Framework.Assert.IsTrue("delta is not empty", actDelta.Length > 0);
			NUnit.Framework.Assert.AreEqual(src.Length, BinaryDelta.GetBaseSize(actDelta));
			NUnit.Framework.Assert.AreEqual(dst.Length, BinaryDelta.GetResultSize(actDelta));
			NUnit.Framework.Assert.IsTrue(Arrays.Equals(dst, BinaryDelta.Apply(src, actDelta)
				));
		}
	}
}