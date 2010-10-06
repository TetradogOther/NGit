using System;
using NGit;
using NGit.Transport;
using Sharpen;

namespace NGit.Transport
{
	/// <summary>Multiplexes data and progress messages.</summary>
	/// <remarks>
	/// Multiplexes data and progress messages.
	/// <p>
	/// This stream is buffered at packet sizes, so the caller doesn't need to wrap
	/// it in yet another buffered stream.
	/// </remarks>
	internal class SideBandOutputStream : OutputStream
	{
		internal const int CH_DATA = SideBandInputStream.CH_DATA;

		internal const int CH_PROGRESS = SideBandInputStream.CH_PROGRESS;

		internal const int CH_ERROR = SideBandInputStream.CH_ERROR;

		internal const int SMALL_BUF = 1000;

		internal const int MAX_BUF = 65520;

		internal const int HDR_SIZE = 5;

		private readonly OutputStream @out;

		private readonly byte[] buffer;

		/// <summary>
		/// Number of bytes in
		/// <see cref="buffer">buffer</see>
		/// that are valid data.
		/// <p>
		/// Initialized to
		/// <see cref="HDR_SIZE">HDR_SIZE</see>
		/// if there is no application data in the
		/// buffer, as the packet header always appears at the start of the buffer.
		/// </summary>
		private int cnt;

		/// <summary>Create a new stream to write side band packets.</summary>
		/// <remarks>Create a new stream to write side band packets.</remarks>
		/// <param name="chan">
		/// channel number to prefix all packets with, so the remote side
		/// can demultiplex the stream and get back the original data.
		/// Must be in the range [0, 255].
		/// </param>
		/// <param name="sz">
		/// maximum size of a data packet within the stream. The remote
		/// side needs to agree to the packet size to prevent buffer
		/// overflows. Must be in the range [HDR_SIZE + 1, MAX_BUF).
		/// </param>
		/// <param name="os">
		/// stream that the packets are written onto. This stream should
		/// be attached to a SideBandInputStream on the remote side.
		/// </param>
		internal SideBandOutputStream(int chan, int sz, OutputStream os)
		{
			if (chan <= 0 || chan > 255)
			{
				throw new ArgumentException(MessageFormat.Format(JGitText.Get().channelMustBeInRange0_255
					, chan));
			}
			if (sz <= HDR_SIZE)
			{
				throw new ArgumentException(MessageFormat.Format(JGitText.Get().packetSizeMustBeAtLeast
					, sz, HDR_SIZE));
			}
			else
			{
				if (MAX_BUF < sz)
				{
					throw new ArgumentException(MessageFormat.Format(JGitText.Get().packetSizeMustBeAtMost
						, sz, MAX_BUF));
				}
			}
			@out = os;
			buffer = new byte[sz];
			buffer[4] = unchecked((byte)chan);
			cnt = HDR_SIZE;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Flush()
		{
			if (HDR_SIZE < cnt)
			{
				WriteBuffer();
			}
			@out.Flush();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] b, int off, int len)
		{
			while (0 < len)
			{
				int capacity = buffer.Length - cnt;
				if (cnt == HDR_SIZE && capacity < len)
				{
					// Our block to write is bigger than the packet size,
					// stream it out as-is to avoid unnecessary copies.
					PacketLineOut.FormatLength(buffer, buffer.Length);
					@out.Write(buffer, 0, HDR_SIZE);
					@out.Write(b, off, capacity);
					off += capacity;
					len -= capacity;
				}
				else
				{
					if (capacity == 0)
					{
						WriteBuffer();
					}
					int n = Math.Min(len, capacity);
					System.Array.Copy(b, off, buffer, cnt, n);
					cnt += n;
					off += n;
					len -= n;
				}
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(int b)
		{
			if (cnt == buffer.Length)
			{
				WriteBuffer();
			}
			buffer[cnt++] = unchecked((byte)b);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void WriteBuffer()
		{
			PacketLineOut.FormatLength(buffer, cnt);
			@out.Write(buffer, 0, cnt);
			cnt = HDR_SIZE;
		}
	}
}