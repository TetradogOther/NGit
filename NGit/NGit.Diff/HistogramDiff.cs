using System;
using NGit.Diff;
using Sharpen;

namespace NGit.Diff
{
	/// <summary>An extended form of Bram Cohen's patience diff algorithm.</summary>
	/// <remarks>
	/// An extended form of Bram Cohen's patience diff algorithm.
	/// This implementation was derived by using the 4 rules that are outlined in
	/// Bram Cohen's <a href="http://bramcohen.livejournal.com/73318.html">blog</a>,
	/// and then was further extended to support low-occurrence common elements.
	/// The basic idea of the algorithm is to create a histogram of occurrences for
	/// each element of sequence A. Each element of sequence B is then considered in
	/// turn. If the element also exists in sequence A, and has a lower occurrence
	/// count, the positions are considered as a candidate for the longest common
	/// subsequence (LCS). After scanning of B is complete the LCS that has the
	/// lowest number of occurrences is chosen as a split point. The region is split
	/// around the LCS, and the algorithm is recursively applied to the sections
	/// before and after the LCS.
	/// By always selecting a LCS position with the lowest occurrence count, this
	/// algorithm behaves exactly like Bram Cohen's patience diff whenever there is a
	/// unique common element available between the two sequences. When no unique
	/// elements exist, the lowest occurrence element is chosen instead. This offers
	/// more readable diffs than simply falling back on the standard Myers' O(ND)
	/// algorithm would produce.
	/// To prevent the algorithm from having an O(N^2) running time, an upper limit
	/// on the number of unique elements in a histogram bucket is configured by
	/// <see cref="SetMaxChainLength(int)">SetMaxChainLength(int)</see>
	/// . If sequence A has more than this many
	/// elements that hash into the same hash bucket, the algorithm passes the region
	/// to
	/// <see cref="SetFallbackAlgorithm(DiffAlgorithm)">SetFallbackAlgorithm(DiffAlgorithm)
	/// 	</see>
	/// . If no fallback algorithm is
	/// configured, the region is emitted as a replace edit.
	/// During scanning of sequence B, any element of A that occurs more than
	/// <see cref="SetMaxChainLength(int)">SetMaxChainLength(int)</see>
	/// times is never considered for an LCS match
	/// position, even if it is common between the two sequences. This limits the
	/// number of locations in sequence A that must be considered to find the LCS,
	/// and helps maintain a lower running time bound.
	/// So long as
	/// <see cref="SetMaxChainLength(int)">SetMaxChainLength(int)</see>
	/// is a small constant (such as 64),
	/// the algorithm runs in O(N * D) time, where N is the sum of the input lengths
	/// and D is the number of edits in the resulting EditList. If the supplied
	/// <see cref="SequenceComparator{S}">SequenceComparator&lt;S&gt;</see>
	/// has a good hash function, this implementation
	/// typically out-performs
	/// <see cref="MyersDiff{S}">MyersDiff&lt;S&gt;</see>
	/// , even though its theoretical running
	/// time is the same.
	/// This implementation has an internal limitation that prevents it from handling
	/// sequences with more than 268,435,456 (2^28) elements.
	/// </remarks>
	public class HistogramDiff : DiffAlgorithm
	{
		/// <summary>Algorithm to use when there are too many element occurrences.</summary>
		/// <remarks>Algorithm to use when there are too many element occurrences.</remarks>
		private DiffAlgorithm fallback = MyersDiff<Sequence>.INSTANCE;

		/// <summary>Maximum number of positions to consider for a given element hash.</summary>
		/// <remarks>
		/// Maximum number of positions to consider for a given element hash.
		/// All elements with the same hash are stored into a single chain. The chain
		/// size is capped to ensure search is linear time at O(len_A + len_B) rather
		/// than quadratic at O(len_A * len_B).
		/// </remarks>
		private int maxChainLength = 64;

		/// <summary>Set the algorithm used when there are too many element occurrences.</summary>
		/// <remarks>Set the algorithm used when there are too many element occurrences.</remarks>
		/// <param name="alg">
		/// the secondary algorithm. If null the region will be denoted as
		/// a single REPLACE block.
		/// </param>
		public virtual void SetFallbackAlgorithm(DiffAlgorithm alg)
		{
			fallback = alg;
		}

		/// <summary>Maximum number of positions to consider for a given element hash.</summary>
		/// <remarks>
		/// Maximum number of positions to consider for a given element hash.
		/// All elements with the same hash are stored into a single chain. The chain
		/// size is capped to ensure search is linear time at O(len_A + len_B) rather
		/// than quadratic at O(len_A * len_B).
		/// </remarks>
		/// <param name="maxLen">new maximum length.</param>
		public virtual void SetMaxChainLength(int maxLen)
		{
			maxChainLength = maxLen;
		}

		public override EditList DiffNonCommon<S>(SequenceComparator<S> cmp, S a, 
			S b)
		{
			HistogramDiff.State<S> s = new HistogramDiff.State<S>(this, new HashedSequencePair
				<S>(cmp, a, b));
			s.DiffReplace(new Edit(0, s.a.Size(), 0, s.b.Size()));
			return s.edits;
		}

		private class State<S> where S:Sequence
		{
			private readonly HashedSequenceComparator<S> cmp;

			internal readonly HashedSequence<S> a;

			internal readonly HashedSequence<S> b;

			/// <summary>Result edits we have determined that must be made to convert a to b.</summary>
			/// <remarks>Result edits we have determined that must be made to convert a to b.</remarks>
			internal readonly EditList edits;

			internal State(HistogramDiff _enclosing, HashedSequencePair<S> p)
			{
				this._enclosing = _enclosing;
				this.cmp = p.GetComparator();
				this.a = p.GetA();
				this.b = p.GetB();
				this.edits = new EditList();
			}

			internal virtual void DiffReplace(Edit r)
			{
				Edit lcs = new HistogramDiffIndex<S>(this._enclosing.maxChainLength, this.cmp, this
					.a, this.b, r).FindLongestCommonSequence();
				if (lcs != null)
				{
					// If we were given an edit, we can prove a result here.
					//
					if (lcs.IsEmpty())
					{
						// An empty edit indicates there is nothing in common.
						// Replace the entire region.
						//
						this.edits.AddItem(r);
					}
					else
					{
						this.Diff(r.Before(lcs));
						this.Diff(r.After(lcs));
					}
				}
				else
				{
					if (this._enclosing.fallback != null)
					{
						SubsequenceComparator<HashedSequence<S>> cs = this.Subcmp();
						Subsequence<HashedSequence<S>> @as = Subsequence<S>.A(this.a, r);
						Subsequence<HashedSequence<S>> bs = Subsequence<S>.B(this.b, r);
						EditList res = this._enclosing.fallback.DiffNonCommon(cs, @as, bs);
						Sharpen.Collections.AddAll(this.edits, Subsequence<S>.ToBase(res, @as, bs));
					}
					else
					{
						this.edits.AddItem(r);
					}
				}
			}

			private void Diff(Edit r)
			{
				switch (r.GetType())
				{
					case Edit.Type.INSERT:
					case Edit.Type.DELETE:
					{
						this.edits.AddItem(r);
						break;
					}

					case Edit.Type.REPLACE:
					{
						this.DiffReplace(r);
						break;
					}

					case Edit.Type.EMPTY:
					default:
					{
						throw new InvalidOperationException();
					}
				}
			}

			private SubsequenceComparator<HashedSequence<S>> Subcmp()
			{
				return new SubsequenceComparator<HashedSequence<S>>(this.cmp);
			}

			private readonly HistogramDiff _enclosing;
		}
	}
}