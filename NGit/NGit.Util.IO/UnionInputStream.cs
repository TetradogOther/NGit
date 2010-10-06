using System.Collections.Generic;
using System.IO;
using Sharpen;

namespace NGit.Util.IO
{
	/// <summary>An InputStream which reads from one or more InputStreams.</summary>
	/// <remarks>
	/// An InputStream which reads from one or more InputStreams.
	/// <p>
	/// This stream may enter into an EOF state, returning -1 from any of the read
	/// methods, and then later successfully read additional bytes if a new
	/// InputStream is added after reaching EOF.
	/// <p>
	/// Currently this stream does not support the mark/reset APIs. If mark and later
	/// reset functionality is needed the caller should wrap this stream with a
	/// <see cref="Sharpen.BufferedInputStream">Sharpen.BufferedInputStream</see>
	/// .
	/// </remarks>
	public class UnionInputStream : InputStream
	{
		private sealed class _InputStream_63 : InputStream
		{
			public _InputStream_63()
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override int Read()
			{
				return -1;
			}
		}

		private static readonly InputStream EOF = new _InputStream_63();

		private readonly List<InputStream> streams = new List<InputStream>();

		/// <summary>Create an empty InputStream that is currently at EOF state.</summary>
		/// <remarks>Create an empty InputStream that is currently at EOF state.</remarks>
		public UnionInputStream()
		{
		}

		/// <summary>Create an InputStream that is a union of the individual streams.</summary>
		/// <remarks>
		/// Create an InputStream that is a union of the individual streams.
		/// <p>
		/// As each stream reaches EOF, it will be automatically closed before bytes
		/// from the next stream are read.
		/// </remarks>
		/// <param name="inputStreams">streams to be pushed onto this stream.</param>
		public UnionInputStream(params InputStream[] inputStreams)
		{
			// Do nothing.
			foreach (InputStream i in inputStreams)
			{
				Add(i);
			}
		}

		private InputStream Head()
		{
			return streams.IsEmpty() ? EOF : streams.GetFirst();
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void Pop()
		{
			if (!streams.IsEmpty())
			{
				streams.RemoveFirst().Close();
			}
		}

		/// <summary>Add the given InputStream onto the end of the stream queue.</summary>
		/// <remarks>
		/// Add the given InputStream onto the end of the stream queue.
		/// <p>
		/// When the stream reaches EOF it will be automatically closed.
		/// </remarks>
		/// <param name="in">the stream to add; must not be null.</param>
		public virtual void Add(InputStream @in)
		{
			streams.AddItem(@in);
		}

		/// <summary>Returns true if there are no more InputStreams in the stream queue.</summary>
		/// <remarks>
		/// Returns true if there are no more InputStreams in the stream queue.
		/// <p>
		/// If this method returns
		/// <code>true</code>
		/// then all read methods will signal EOF
		/// by returning -1, until another InputStream has been pushed into the queue
		/// with
		/// <see cref="Add(Sharpen.InputStream)">Add(Sharpen.InputStream)</see>
		/// .
		/// </remarks>
		/// <returns>true if there are no more streams to read from.</returns>
		public virtual bool IsEmpty()
		{
			return streams.IsEmpty();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Read()
		{
			for (; ; )
			{
				InputStream @in = Head();
				int r = @in.Read();
				if (0 <= r)
				{
					return r;
				}
				else
				{
					if (@in == EOF)
					{
						return -1;
					}
					else
					{
						Pop();
					}
				}
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Read(byte[] b, int off, int len)
		{
			int cnt = 0;
			while (0 < len)
			{
				InputStream @in = Head();
				int n = @in.Read(b, off, len);
				if (0 < n)
				{
					cnt += n;
					off += n;
					len -= n;
				}
				else
				{
					if (@in == EOF)
					{
						return 0 < cnt ? cnt : -1;
					}
					else
					{
						Pop();
					}
				}
			}
			return cnt;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Available()
		{
			return Head().Available();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override long Skip(long len)
		{
			long cnt = 0;
			while (0 < len)
			{
				InputStream @in = Head();
				long n = @in.Skip(len);
				if (0 < n)
				{
					cnt += n;
					len -= n;
				}
				else
				{
					if (@in == EOF)
					{
						return cnt;
					}
					else
					{
						// Is this stream at EOF? We can't tell from skip alone.
						// Read one byte to test for EOF, discard it if we aren't
						// yet at EOF.
						//
						int r = @in.Read();
						if (r < 0)
						{
							Pop();
						}
						else
						{
							cnt += 1;
							len -= 1;
						}
					}
				}
			}
			return cnt;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Close()
		{
			IOException err = null;
			for (Iterator<InputStream> i = streams.Iterator(); i.HasNext(); )
			{
				try
				{
					i.Next().Close();
				}
				catch (IOException closeError)
				{
					err = closeError;
				}
				i.Remove();
			}
			if (err != null)
			{
				throw err;
			}
		}
	}
}