﻿namespace M
{

	using System;
	using System.Collections.Generic;
	using System.IO;
	/// <summary>
	/// Represents a MIDI sequence
	/// </summary>
	/// <remarks>Each of these corresponds to one MIDI track</remarks>
#if MIDILIB
	public
#else
	internal
#endif
	sealed partial class MidiSequence : ICloneable
	{
		/// <summary>
		/// Creates a new MIDI sequence
		/// </summary>
		public MidiSequence()
		{
			Events = new List<MidiEvent>();
		}
		/// <summary>
		/// Indicates the events of the MIDI sequence
		/// </summary>
		public IList<MidiEvent> Events { get; private set; }
		/// <summary>
		/// Indicates the first downbeat of the MIDI sequence
		/// </summary>
		public int FirstDownBeat {
			get {
				foreach (var e in AbsoluteEvents)
				{
					var m = e.Message;
					if (0x99 == m.Status) // note down, percussion track
					{
						var mcm = m as MidiMessageWord;
						if (0!=mcm.Data2 && 35 == mcm.Data1) // bass drum
						{
							return e.Position;
						}
					}
				}
				return -1;

			}
		}
		/// <summary>
		/// Indicates the first note on message in the MIDI sequence
		/// </summary>
		public int FirstNoteOn {
			get {
				foreach (var e in AbsoluteEvents)
				{
					var m = e.Message;
					if (0x90 == (m.Status & 0xF0)) // note down
					{
						var mcm = m as MidiMessageWord;
						if (0!=mcm.Data2)
						{
							return e.Position;
						}
					}
				}
				return -1;

			}
		}
		/// <summary>
		/// Gets the <see cref="MidiContext"/> at the specified position
		/// </summary>
		/// <param name="position">The position to retrieve the context from, in ticks</param>
		/// <returns></returns>
		public MidiContext GetContext(int position=0)
		{
			var result = new MidiContext();
			int pos = 0;
			foreach(var e in Events)
			{
				pos += e.Position;
				if (pos > position)
					break;
				result.ProcessMessage(e.Message);
			}
			return result;
		}
		/// <summary>
		/// Gets a range of MIDI events as a new sequence
		/// </summary>
		/// <param name="start">The start of the range to retrieve in pulses/ticks</param>
		/// <param name="length">The length of the range to retrieve in pulses/ticks</param>
		/// <returns>A new MIDI sequence with the specified range of events</returns>
		public MidiSequence GetRange(int start,int length)
		{
			var result = new MidiSequence();
			int last = start;
			foreach (var e in AbsoluteEvents)
			{
				if(e.Position>=start)
				{
					if (e.Position >= start + length)
						break;
					result.Events.Add(new MidiEvent(e.Position - last, e.Message));
					last = e.Position;
				}
			}
			return result;
		}
		/// <summary>
		/// Indicates the name of the sequence, or null if no name is present
		/// </summary>
		public string Name { get {
				foreach(var e in AbsoluteEvents)
				{
					if(e.Message.Status==0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if(3==mm.Data1)
						{
							return mm.Text;
						}
					}
				}
				return null;
			}
		}
		/// <summary>
		/// Indicates the name of the instrument in the MIDI sequence or null if not known
		/// </summary>
		public string Instrument {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (4 == mm.Data1)
						{
							return mm.Text;
						}
					}
				}
				return null;
			}
		}
		/// <summary>
		/// Indicates the copyright of the MIDI sequence or null if unspecified
		/// </summary>
		public string Copyright {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (2 == mm.Data1)
						{
							return mm.Text;
						}
					}
				}
				return null;
			}
		}
		internal static MidiSequence ReadFrom(Stream stream)
		{
			MidiSequence result = new MidiSequence();
			var rs = (byte)0;
			var delta = _ReadVarlen(stream);
			if (BitConverter.IsLittleEndian)
				delta = _Swap(delta);
			var i = stream.ReadByte();
			while (-1 != i)
			{
				var hasStatus = false;
				var b = (byte)i;
				if (0x7F < b)
				{
					hasStatus = true;
					rs = b;
					i = stream.ReadByte();
					if (-1 != i)
						b = (byte)i;
					else
						b = 0;

				}
				var st = hasStatus ? rs : (byte)0;
				var m = (MidiMessage)null;
				switch (rs & 0xF0)
				{
					case 0x80:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessageNoteOff(b, (byte)stream.ReadByte(), unchecked((byte)(st & 0x0F)));
						break;
					case 0x90:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessageNoteOn(b, (byte)stream.ReadByte(), unchecked((byte)(st & 0x0F)));
						break;
					case 0xA0:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessageKeyPressure(b, (byte)stream.ReadByte(),  unchecked((byte)(st & 0x0F)));
						break;
					case 0xB0:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessageCC(b, (byte)stream.ReadByte(), unchecked((byte)(st & 0x0F)));
						break;
					case 0xC0:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessagePatchChange(b,unchecked((byte)(st & 0x0F)));
						break;
					case 0xD0:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessageChannelPressure(b, unchecked((byte)(st & 0x0F)));
						break;
					case 0xE0:
						if (i == -1) throw new EndOfStreamException();
						m = new MidiMessageChannelPitch(b, (byte)stream.ReadByte(), unchecked((byte)(st & 0x0F)));
						break;
					case 0xF0:
						switch (rs & 0xF)
						{
							case 0xF:
								if (i == -1) throw new EndOfStreamException();
								var l = _ReadVarlen(stream);
								//if (BitConverter.IsLittleEndian)
								//	l = _Swap(l);
								var ba = new byte[l];
								if (l != stream.Read(ba, 0, ba.Length))
									throw new EndOfStreamException();
								m = new MidiMessageMeta(st, b, ba);
								break;
							case 0x0:
							case 0x7:
								if (i == -1) throw new EndOfStreamException();
								l = _ReadVarlen(b,stream);
								//if (BitConverter.IsLittleEndian)
								//	l = _Swap(l);
								ba = new byte[l];
								if (l != stream.Read(ba, 0, ba.Length))
									throw new EndOfStreamException();
								m = new MidiMessageSysex(st, ba);
								break;
							case 0x2:
								if (i == -1) throw new EndOfStreamException();
								m = new MidiMessageWord(st, b, (byte)stream.ReadByte());
								break;
							case 0x3:
								if (i == -1) throw new EndOfStreamException();
								m = new MidiMessageByte(st, b);
								break;
							case 0x6:
							
							case 0x8:
							case 0xA:
							case 0xB:
							case 0xC:
								// status *was* specified if we got here
								m = new MidiMessage(st);
								break;
							default:
								throw new NotSupportedException("The MIDI message is not recognized.");
						}
						break;
				}
				
				result.Events.Add(new MidiEvent(delta,m));
				i = _ReadVarlen(stream);
				if (-1 == i)
					break;
				delta = i;
				i = stream.ReadByte();
			}
			return result;
		}
		internal void WriteTo(Stream stream)
		{
			foreach(var e in Events)
			{
				var pos = e.Position;
				//if (BitConverter.IsLittleEndian)
					//pos = _Swap(pos);
				_WriteVarlen(stream, pos);
				switch(e.Message.PayloadLength)
				{
					case 0:
						stream.WriteByte(e.Message.Status);
						break;
					case 1:
						if (0 != e.Message.Status) stream.WriteByte(e.Message.Status);
						var mb = e.Message as MidiMessageByte;
						stream.WriteByte(mb.Data1);
						break;
					case 2:
						if (0 != e.Message.Status) stream.WriteByte(e.Message.Status);
						var mw = e.Message as MidiMessageWord;
						stream.WriteByte(mw.Data1);
						stream.WriteByte(mw.Data2);
						break;
					case -1:
						if (0 != e.Message.Status) stream.WriteByte(e.Message.Status);
						var mbs = e.Message as MidiMessageMeta;
						stream.WriteByte(mbs.Data1);
						int v = mbs.Data.Length;
						//if (BitConverter.IsLittleEndian)
						//	v = _Swap(v);
						_WriteVarlen(stream, v);
						stream.Write(mbs.Data, 0, mbs.Data.Length);
						break;
				}
			}
		}
		/// <summary>
		/// Concatenates this sequence with another MIDI sequence
		/// </summary>
		/// <param name="right">The sequence to concatenate this sequence with</param>
		/// <returns>A new MIDI sequence that is the concatenation of this sequence and <paramref name="right"/></returns>
		public MidiSequence Concat(MidiSequence right) => Concat(this, right);
		/// <summary>
		/// Concatenates this sequence with other MIDI sequences
		/// </summary>
		/// <param name="sequences">The sequences to concatenate this sequence with</param>
		/// <returns>A new MIDI sequence that is the concatenation of this sequence and <paramref name="sequences"/></returns>
		public static MidiSequence Concat(params MidiSequence[] sequences) => Concat((IEnumerable<MidiSequence>)sequences);
		/// <summary>
		/// Concatenates this sequence with other MIDI sequences
		/// </summary>
		/// <param name="sequences">The sequences to concatenate this sequence with</param>
		/// <returns>A new MIDI sequence that is the concatenation of this sequence and <paramref name="sequences"/></returns>
		public static MidiSequence Concat(IEnumerable<MidiSequence> sequences)
		{
			var result = new MidiSequence();
			var endDelta = 0;
			var sawEnd = false;
			foreach (var seq in sequences)
			{
				int rs = 0;
				foreach (var e in seq.Events)
				{
					var m = e.Message;
					if (0 != m.Status) rs = m.Status;
					if(0xFF==rs)
					{
						var mbs = m as MidiMessageMeta;
						if (0x2F == mbs.Data1)
						{
							sawEnd = true;
							endDelta = e.Position;
							break;
						}
					}
					result.Events.Add(new MidiEvent(e.Position+endDelta, m));
					endDelta = 0;
				}
			}
			if(sawEnd) // add an end marker back to the track
				result.Events.Add(new MidiEvent(endDelta, new MidiMessageMeta(0xFF, 0x2F, new byte[0])));
			return result;
		}
		/// <summary>
		/// Merges this sequence with other MIDI sequences
		/// </summary>
		/// <param name="right">The sequence to merge this sequence with</param>
		/// <returns>A new MIDI sequence that is a merge of this sequence and <paramref name="right"/></returns>
		public MidiSequence Merge(MidiSequence right) => Merge(this, right);
		/// <summary>
		/// Merges this sequence with other MIDI sequences
		/// </summary>
		/// <param name="sequences">The sequences to merge this sequence with</param>
		/// <returns>A new MIDI sequence that is a merge of this sequence and <paramref name="sequences"/></returns>
		public static MidiSequence Merge(params MidiSequence[] sequences) => Merge((IEnumerable<MidiSequence>)sequences);
		/// <summary>
		/// Merges this sequence with other MIDI sequences
		/// </summary>
		/// <param name="sequences">The sequences to merge this sequence with</param>
		/// <returns>A new MIDI sequence that is a merge of this sequence and <paramref name="sequences"/></returns>
		public static MidiSequence Merge(IEnumerable<MidiSequence> sequences)
		{
			var result = new MidiSequence();
			var l = new List<MidiEvent>();
			foreach(var seq in sequences)
				l.AddRange(seq.AbsoluteEvents);
			l.Sort(delegate (MidiEvent x, MidiEvent y) { return x.Position.CompareTo(y.Position); });
			var hasMidiEnd = false;
			var last = 0;
			foreach (var e in l)
			{
				if(0xFF==e.Message.Status && -1==e.Message.PayloadLength)
				{
					var mbs = e.Message as MidiMessageMeta;
					// filter the midi end track sequences out, note if we found at least one
					if (0x2F == mbs.Data1)
					{
						hasMidiEnd = true;
						continue;
					}
				}
				result.Events.Add(new MidiEvent(e.Position - last, e.Message));
				last = e.Position;
			}
			if(hasMidiEnd) // if we found a midi end track, then add one back after all is done
				result.Events.Add(new MidiEvent(last, new MidiMessageMeta(0xFF, 0x2F, new byte[0])));
			
			return result;
		}
		/// <summary>
		/// Stretches or compresses the MIDI sequence events
		/// </summary>
		/// <remarks>If <paramref name="adjustTempo"/> is false this will change the playback speed of the MIDI</remarks>
		/// <param name="diff">The differential for the size. 1 is the same length, .5 would be half the length and 2 would be twice the length</param>
		/// <param name="adjustTempo">Indicates whether or not the tempo should be adjusted to compensate</param>
		/// <returns>A new MIDI sequence that is stretched the specified amount</returns>
		public MidiSequence Stretch(double diff,bool adjustTempo=false)
		{
			var result = new MidiSequence();
			if (!adjustTempo)
				foreach (var e in Events)
					result.Events.Add(new MidiEvent((int)Math.Round(e.Position * diff, MidpointRounding.AwayFromZero), e.Message));
			else
			{
				byte runningStatus = 0;
				foreach (var e in Events)
				{
					if (0 != e.Message.Status)
						runningStatus = e.Message.Status;
					var m = e.Message;
					if (-1 == m.PayloadLength)
					{
						if (0xFF == runningStatus)
						{
							var mbs = m as MidiMessageMeta;
							if(0x51==mbs.Data1)
							{
								var mt = 0;
								if (BitConverter.IsLittleEndian)
									mt = (mbs.Data[0] << 16) | (mbs.Data[1] << 8) | mbs.Data[2];
								else
									mt = (mbs.Data[2] << 16) | (mbs.Data[1] << 8) | mbs.Data[0];
								mt = (int)Math.Round(mt / diff, MidpointRounding.AwayFromZero);
								var buf = new byte[3];
								if (BitConverter.IsLittleEndian)
								{
									buf[0] = unchecked((byte)((mt >> 16) & 0xFF));
									buf[1] = unchecked((byte)((mt >> 8) & 0xFF));
									buf[2] = unchecked((byte)((mt) & 0xFF));
								}
								else
								{
									buf[0] = unchecked((byte)((mt) & 0xFF));
									buf[1] = unchecked((byte)((mt >> 8) & 0xFF));
									buf[2] = unchecked((byte)((mt >> 16) & 0xFF));
								}
								m= new MidiMessageMeta(mbs.Status, mbs.Data1, buf);
							}
						}
					}
					result.Events.Add(new MidiEvent((int)Math.Round(e.Position * diff, MidpointRounding.AwayFromZero), m));
				}
			}
			return result;
		}
		/// <summary>
		/// Indicates the MicroTempo of the MIDI sequence
		/// </summary>
		public int MicroTempo { get {
				foreach (var e in AbsoluteEvents)
				{
					switch(e.Message.Status & 0xF0)
					{
						case 0x80:
						case 0x90:
							return 500000;
					}
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (0x51 == mm.Data1)
						{
							return BitConverter.IsLittleEndian?
								(mm.Data[0] << 16) | (mm.Data[1] << 8) | mm.Data[2]:
								(mm.Data[2] << 16) | (mm.Data[1] << 8) | mm.Data[0];
						}
					}
				}
				return 500000;
			}
		}
		/// <summary>
		/// Indicates all of the MicroTempos in the sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int,int>> MicroTempos { get {
				foreach (var e in AbsoluteEvents)
				{
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (0x51 == mm.Data1)
						{
							if (BitConverter.IsLittleEndian)
								yield return new KeyValuePair<int, int>(e.Position, (mm.Data[0] << 16) | (mm.Data[1] << 8) | mm.Data[2]);
							else
								yield return new KeyValuePair<int, int>(e.Position,(mm.Data[2] << 16) | (mm.Data[1] << 8) | mm.Data[0]);
						}
					}
				}
			}
		}
		/// <summary>
		/// Indicates the tempo of the sequence
		/// </summary>
		public double Tempo {
			get {
				return MidiUtility.MicroTempoToTempo(MicroTempo);
			}
		}
		/// <summary>
		/// Indicates all the tempos in the sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int, double>> Tempos {
			get {
				foreach (var mt in MicroTempos)
					yield return new KeyValuePair<int, double>(mt.Key, MidiUtility.MicroTempoToTempo(mt.Value));
			}
		}
		/// <summary>
		/// Indicates the time signature of the MIDI sequence
		/// </summary>
		public MidiTimeSignature TimeSignature {
			get {
				foreach (var e in AbsoluteEvents)
				{
					switch (e.Message.Status & 0xF0)
					{
						case 0x80:
						case 0x90:
							return MidiTimeSignature.Default;
					}
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (0x58 == mm.Data1)
						{
							var num = mm.Data[0];
							var den = mm.Data[1];
							var met = mm.Data[2];
							var q32 = mm.Data[3];
							return new MidiTimeSignature(num, (short)Math.Pow(den, 2), met, q32);
						}
					}
				}
				return MidiTimeSignature.Default;
			}
		}
		/// <summary>
		/// Indicates all of the TimeSignatures in the sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int, MidiTimeSignature>> TimeSignatures {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (0x58 == mm.Data1)
						{
							var num = mm.Data[0];
							var den = mm.Data[1];
							var met = mm.Data[2];
							var q32 = mm.Data[3];
							yield return new KeyValuePair<int, MidiTimeSignature>(e.Position, new MidiTimeSignature(num, (short)Math.Pow(den, 2), met, q32));
						}
					}
				}
			}
		}
		/// <summary>
		/// Indicates the key signature of the MIDI sequence
		/// </summary>
		public MidiKeySignature KeySignature {
			get {
				foreach (var e in AbsoluteEvents)
				{
					switch (e.Message.Status & 0xF0)
					{
						case 0x80:
						case 0x90:
							return MidiKeySignature.Default;
					}
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (0x59 == mm.Data1)
						{
							return new MidiKeySignature(unchecked((sbyte)mm.Data[0]), 0 != mm.Data[1]);
						}
					}
				}
				return MidiKeySignature.Default;
			}
		}
		/// <summary>
		/// Indicates all of the MIDI key signatures in the sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int, MidiKeySignature>> KeySignatures {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (e.Message.Status == 0xFF)
					{
						var mm = e.Message as MidiMessageMeta;
						if (0x59 == mm.Data1)
						{
							yield return new KeyValuePair<int, MidiKeySignature>(e.Position, new MidiKeySignature(unchecked((sbyte)mm.Data[0]), 0 != mm.Data[1]));							
						}
					}
				}
			}
		}
		/// <summary>
		/// Indicates the length of the MIDI sequence
		/// </summary>
		public int Length {
			get {
				var l= 0;
				byte r = 0;
				foreach (var e in Events) {
					var m = e.Message;
					if (0 != m.Status)
						r = m.Status;
					l += e.Position;
					if(0xFF==r && 0x2F==(m as MidiMessageMeta).Data1)
					{
						break;
					}
				}
				return l + 1;
			}
		}
		/// <summary>
		/// Indicates the lyrics of the MIDI sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int,string>> Lyrics {
			get {
				foreach(var e in AbsoluteEvents)
				{
					if(0xFF == e.Message.Status)
					{
						var mm = e.Message as MidiMessageMeta;
						if(5==mm.Data1)
							yield return new KeyValuePair<int, string>(e.Position, mm.Text);
					}
				}
			}
		}
		/// <summary>
		/// Indicates the markers in the MIDI sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int, string>> Markers {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (0xFF == e.Message.Status)
					{
						var mm = e.Message as MidiMessageMeta;
						if (6 == mm.Data1)
							yield return new KeyValuePair<int, string>(e.Position, mm.Text);
					}
				}
			}
		}
		/// <summary>
		/// Indicates the comments in the MIDI sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int, string>> Comments {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (0xFF == e.Message.Status)
					{
						var mm = e.Message as MidiMessageMeta;
						if (1 == mm.Data1)
							yield return new KeyValuePair<int, string>(e.Position, mm.Text);
					}
				}
			}
		}
		/// <summary>
		/// Indicates the cue points in the MIDI sequence
		/// </summary>
		public IEnumerable<KeyValuePair<int, string>> CuePoints {
			get {
				foreach (var e in AbsoluteEvents)
				{
					if (0xFF == e.Message.Status)
					{
						var mm = e.Message as MidiMessageMeta;
						if (7 == mm.Data1)
							yield return new KeyValuePair<int, string>(e.Position, mm.Text);
					}
				}
			}
		}
		/// <summary>
		/// Indicates the events as absolutely positioned events
		/// </summary>
		public IEnumerable<MidiEvent> AbsoluteEvents {
			get {
				var runningStatus = default(byte);
				var channelPrefix = (byte)0xFF;
				var r = runningStatus;
				var pos=0;
				foreach(var e in Events)
				{
					pos += e.Position;
					var m = default(MidiMessage);
					var hs = true;
					if (0 != e.Message.Status)
						runningStatus = e.Message.Status;
					else
						hs = false;
					r = runningStatus;
					if(!hs && r<0xF0 && 0xFF!=channelPrefix)
					{
						r = unchecked((byte)((r & 0xF0) | channelPrefix));
					}
					switch (e.Message.PayloadLength)
					{
						case 0:
							m = new MidiMessage(r);
							break;
						case 1:
							var mb = e.Message as MidiMessageByte;
							m = new MidiMessageByte(r, mb.Data1);
							break;
						case 2:
							var mw = e.Message as MidiMessageWord;
							m = new MidiMessageWord(r, mw.Data1,mw.Data2);
							break;
						case -1:
							var mbs = e.Message as MidiMessageMeta;
							if (null != mbs)
							{
								m = new MidiMessageMeta(r, mbs.Data1, mbs.Data);
								if (0x20 == mbs.Data1)
									channelPrefix = mbs.Data[0];
								break;
							}
							var msx = e.Message as MidiMessageSysex;
							m = new MidiMessageSysex(r, msx.Data);
							break;
					}
					yield return new MidiEvent(pos, m);
				}
			}
		}
		/// <summary>
		/// Plays the sequence to the specified MIDI device using the specified timebase
		/// </summary>
		/// <param name="timeBase">The timebase to use, in pulses/ticks per quarter note</param>
		/// <param name="deviceIndex">The MIDI device to output to</param>
		/// <param name="loop">Indicates whether to loop playback or not</param>
		public void Preview(short timeBase = 480, int deviceIndex = 0,bool loop=false)
		{
			var handle = MidiUtility.OpenOutputDevice(deviceIndex);
			var ppq = timeBase;
			var mt = MidiUtility.TempoToMicroTempo(120d);

			try
			{
				while (loop)
				{
					var ticksusec = mt / (double)timeBase;
					var tickspertick = ticksusec / (TimeSpan.TicksPerMillisecond / 1000) * 100;
					var tickStart = MidiUtility.PreciseUtcNowTicks;
					var tickCurrent = tickStart;

					var end = (long)(Length * tickspertick + tickStart);
					var tpm = TimeSpan.TicksPerMillisecond;

					using (var e = AbsoluteEvents.GetEnumerator())
					{
						if (!e.MoveNext())
							return;
						var done = false;
						while (!done && tickCurrent <= end)
						{
							tickCurrent = MidiUtility.PreciseUtcNowTicks;
							var ce = (long)((tickCurrent - tickStart) / tickspertick);
							while (!done && e.Current.Position <= ce)
							{
								if (0xFF == e.Current.Message.Status)
								{
									var mbs = e.Current.Message as MidiMessageMeta;
									if (0x51 == mbs.Data1)
									{
										if (BitConverter.IsLittleEndian)
											mt = (mbs.Data[0] << 16) | (mbs.Data[1] << 8) | mbs.Data[2];
										else
											mt = (mbs.Data[2] << 16) | (mbs.Data[1] << 8) | mbs.Data[0];
										ticksusec = mt / (double)ppq;
										tickspertick = ticksusec / (tpm / 1000) * 100;
										end = (long)(Length * tickspertick + tickStart);
									}
									else if (0x2F == mbs.Data1)
										done = true;
								}
								MidiUtility.Send(handle, e.Current.Message);
								if (!e.MoveNext())
									done = true;
							}
						}
					}
				}
			}
			finally
			{
				MidiUtility.CloseOutputDevice(handle);
			}
		}
		/// <summary>
		/// Creates a deep copy of the sequence
		/// </summary>
		/// <returns></returns>
		public MidiSequence Clone()
		{
			var result = new MidiSequence();
			for(int ic=Events.Count,i=0;i<ic;++i)
				result.Events.Add(Events[i].Clone());
			return result;
		}
		object ICloneable.Clone()
		{
			return Clone();
		}
		private static uint _Swap(uint x) { return ((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) + ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24); }
		
		private static int _Swap(int x) => unchecked((int)_Swap(unchecked((uint)x)));

		private static int _ReadVarlen(Stream stream)
		{
			var b = stream.ReadByte();
			var result = 0;
			if (-1 == b) return -1;
			if (0x80 > b) // single value
			{
				result = b;
				return result;
			}
			else // short or int
			{
				result = b & 0x7F;
				result <<= 7;
				if (-1 == (b = stream.ReadByte())) throw new EndOfStreamException();
				result |= b & 0x7F;
				if (0x80 > b)
				{
					if (BitConverter.IsLittleEndian)
						return result;
					else
						return _Swap(result);
				}
				// int
				result <<= 7;
				if (-1 == (b = stream.ReadByte())) throw new EndOfStreamException();
				result |= b & 0x7F;
				if (0x80 > b)
				{
					if (BitConverter.IsLittleEndian)
						return result;
					else
						return _Swap(result << 7);
				}
				// int (4 len)
				result <<= 7;
				if (-1 == (b = stream.ReadByte())) throw new EndOfStreamException();
				result |= b & 0x7F;
				if (0x80 > b)
				{
					if (BitConverter.IsLittleEndian)
						return result;
					else
						return _Swap(result << 7);
				}
				throw new NotSupportedException("MIDI Variable length quantity can't be greater than 28 bits.");
			}
		}
		private static int _ReadVarlen(byte firstByte,Stream stream)
		{
			var b = (int)firstByte;
			var result = 0;
			if (0x80 > b) // single value
			{
				result = b;
				return result;
			}
			else // short or int
			{
				result = b & 0x7F;
				result <<= 7;
				if (-1 == (b = stream.ReadByte())) throw new EndOfStreamException();
				result |= b & 0x7F;
				if (0x80 > b)
				{
					if (BitConverter.IsLittleEndian)
						return result;
					else
						return _Swap(result);
				}
				// int
				result <<= 7;
				if (-1 == (b = stream.ReadByte())) throw new EndOfStreamException();
				result |= b & 0x7F;
				if (0x80 > b)
				{
					if (BitConverter.IsLittleEndian)
						return result;
					else
						return _Swap(result << 7);
				}
				// int (4 len)
				result <<= 7;
				if (-1 == (b = stream.ReadByte())) throw new EndOfStreamException();
				result |= b & 0x7F;
				if (0x80 > b)
				{
					if (BitConverter.IsLittleEndian)
						return result;
					else
						return _Swap(result << 7);
				}
				throw new NotSupportedException("MIDI Variable length quantity can't be greater than 28 bits.");
			}
		}
		private static void _WriteVarlen(Stream stream, int value)
		{
			int buffer;
			buffer = value & 0x7f;
			while ((value >>= 7) > 0)
			{
				buffer <<= 8;
				buffer |= 0x80;
				buffer += (value & 0x7f);
			}

			while (true)
			{
				stream.WriteByte(unchecked((byte)buffer));
				if (0 < unchecked((byte)(buffer & 0x80))) buffer >>= 8;
				else
					break;
			}
		}
	}
}
