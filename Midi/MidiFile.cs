﻿namespace M
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	/// <summary>
	/// Represents a MIDI file
	/// </summary>
#if MIDILIB
	public
#else
	internal
#endif
	sealed partial class MidiFile : ICloneable
 	{
		/// <summary>
		/// Constructs a MIDI file of the specified type with the specified timebase
		/// </summary>
		/// <param name="type">The MIDI file type - either 0, 1 or 2</param>
		/// <param name="timeBase">The timebase of the MIDI file in ticks/pulses per quarter note</param>
		public MidiFile(short type=1,short timeBase=480)
		{
			Type = type;
			TimeBase = timeBase;
			Tracks = new List<MidiSequence>();
		}
		/// <summary>
		/// Plays the file over the specified device
		/// </summary>
		/// <param name="deviceIndex">The index of the device to use</param>
		/// <param name="loop">Indicates whether to loop playback or not</param>
		public void Preview(int deviceIndex=0,bool loop=false)
		{
			MidiSequence.Merge(Tracks).Preview(TimeBase, deviceIndex,loop);
		}
		/// <summary>
		/// Changes the timebase of the MIDI file
		/// </summary>
		/// <param name="timeBase">The new timebase, in ticks/pulses per quarter note</param>
		/// <returns></returns>
		public MidiFile Resample(short timeBase)
		{
			var diff = timeBase / (double)TimeBase;
			var result = new MidiFile(Type, timeBase);
			foreach(var trk in Tracks)
				result.Tracks.Add(trk.Stretch(diff, false));
			return result;
		}
		/// <summary>
		/// Returns the internal name of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's name</remarks>
		public string Name {  get {
				if(1==Type && 0<Tracks.Count)
					return Tracks[0].Name;
				return null;
			} 
		}
		/// <summary>
		/// Returns the copyright information of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's copyright metadata</remarks>
		public string Copyright { get {
				if (1 == Type && 0 < Tracks.Count)
					return Tracks[0].Copyright;
				return null;

			} 
		}
		/// <summary>
		/// Indicates the MicroTempo of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's microtempo</remarks>
		public int MicroTempo { get {
				if (0 == Tracks.Count) return 500000;
				return Tracks[0].MicroTempo;
			}
		}
		/// <summary>
		/// Indicates all of the MicroTempos of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's microtempos</remarks>
		public IEnumerable<KeyValuePair<int,int>> MicroTempos {
			get {
				if (0 == Tracks.Count) return new KeyValuePair<int,int>[0];
				return Tracks[0].MicroTempos;
			}
		}
		/// <summary>
		/// Indicates the Tempo of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's tempo</remarks>
		public double Tempo {
			get {
				return MidiUtility.MicroTempoToTempo(MicroTempo);
			}
		}
		/// <summary>
		/// Indicates all of the Tempos of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's tempos</remarks>
		public IEnumerable<KeyValuePair<int,double>> Tempos {
			get {
				foreach(var mt in MicroTempos)
					yield return new KeyValuePair<int, double>(mt.Key, MidiUtility.MicroTempoToTempo(mt.Value));
			}
		}
		/// <summary>
		/// Indicates the time signature of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's time signature</remarks>
		public MidiTimeSignature TimeSignature {
			get {
				if (0 == Tracks.Count)
					return MidiTimeSignature.Default;
				return Tracks[0].TimeSignature;
			}
		}
		/// <summary>
		/// Indicates all the time signatures of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's time signatures</remarks>
		public IEnumerable<KeyValuePair<int,MidiTimeSignature>> TimeSignatures {
			get {
				if (0 == Tracks.Count)
					return new KeyValuePair<int, MidiTimeSignature>[0];
				return Tracks[0].TimeSignatures;
			}
		}
		/// <summary>
		/// Indicates the key signature of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's key signature</remarks>
		public MidiKeySignature KeySignature {
			get {
				if (0 == Tracks.Count)
					return MidiKeySignature.Default;
				return Tracks[0].KeySignature;
			}
		}
		/// <summary>
		/// Indicates all the key signatures of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's key signatures</remarks>
		public IEnumerable<KeyValuePair<int, MidiKeySignature>> KeySignatures {
			get {
				if (0 == Tracks.Count)
					return new KeyValuePair<int, MidiKeySignature>[0];
				return Tracks[0].KeySignatures;
			}
		}
		/// <summary>
		/// Indicates the type of the MIDI file
		/// </summary>
		/// <remarks>This can be 0, 1 or 2</remarks>
		public short Type { get; private set; }
		/// <summary>
		/// Indicates the timebase of the MIDI file as ticks/pulses per quarter note
		/// </summary>
		public short TimeBase { get; private set; }
		/// <summary>
		/// Retrieves a list of all MIDI tracks in the file
		/// </summary>
		public IList<MidiSequence> Tracks { get; private set; }
		/// <summary>
		/// Indicates the total length of the MIDI file, in ticks/pulses
		/// </summary>
		public int Length {  get {
				int result = 0;
				foreach(var trk in Tracks)
				{
					int l = trk.Length;
					if (l > result) result = l;
				}
				return result;
			}
		}
		/// <summary>
		/// Indicates the comments of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's comments</remarks>
		public IEnumerable<KeyValuePair<int, string>> Comments {
			get {
				if (0 < Tracks.Count)
					return Tracks[0].Comments;
				return new KeyValuePair<int, string>[0];
			}
		}
		/// <summary>
		/// Indicates the MIDI Markers of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's markers</remarks>
		public IEnumerable<KeyValuePair<int,string>> Markers {
			get {
				if(0<Tracks.Count)
					return Tracks[0].Markers;
				return new KeyValuePair<int, string>[0];
			}
		}
		/// <summary>
		/// Indicates the lyrics of the MIDI file
		/// </summary>
		/// <remarks>This is derived from Track #0's lyrics</remarks>
		public IEnumerable<KeyValuePair<int, string>> Lyrics {
			get {
				if (0 < Tracks.Count)
					return Tracks[0].Lyrics;
				return new KeyValuePair<int, string>[0];
			}
		}
		/// <summary>
		/// Indicates the position of the first beat of the MIDI file in pulses/ticks
		/// </summary>
		public int FirstDownBeat { get {
				var result = -1;
				foreach (var trk in Tracks)
				{
					var d = trk.FirstDownBeat;
					if (-1!=d && (-1==result || result > d))
						result = d;
				}
				return result;
			}
		}
		/// <summary>
		/// Indicates the position of the first note on message in the MIDI file in pulses/ticks
		/// </summary>
		public int FirstNoteOn {
			get {
				var result = -1;
				foreach (var trk in Tracks)
				{
					var d = trk.FirstNoteOn;
					if (-1 != d && (-1 == result || result > d))
						result = d;
				}
				return result;
			}
		}
		/// <summary>
		/// Stretches or compresses the MIDI file events
		/// </summary>
		/// <remarks>If <paramref name="adjustTempo"/> is false this will change the playback speed of the MIDI</remarks>
		/// <param name="diff">The differential for the size. 1 is the same length, .5 would be half the length and 2 would be twice the length</param>
		/// <param name="adjustTempo">Indicates whether or not the tempo should be adjusted to compensate</param>
		/// <returns>A new MIDI file that is stretched the specified amount</returns>
		public MidiFile Stretch(double diff,bool adjustTempo=false)
		{
			var result = new MidiFile(Type, TimeBase);
			foreach(var trk in Tracks)
				result.Tracks.Add(trk.Stretch(diff,adjustTempo));
			return result;
		}
		/// <summary>
		/// Creates a deep copy of the MIDI file
		/// </summary>
		/// <returns>A new MIDI file that is equivelent to this MIDI file</returns>
		public MidiFile Clone()
		{
			var result = new MidiFile(Type, TimeBase);
			for(int ic=Tracks.Count,i=0;i<ic;++i)
				result.Tracks.Add(Tracks[i].Clone());
			return result;
		}
		object ICloneable.Clone()
		{
			return Clone();
		}
		/// <summary>
		/// Reads a MIDI file from the specified stream
		/// </summary>
		/// <param name="stream">The stream to read from</param>
		/// <returns>A MIDI file object representing the MIDI data in the stream</returns>
		public static MidiFile ReadFrom(Stream stream)
		{
			KeyValuePair<string, byte[]> chunk;
			
			if (!_TryReadChunk(stream, out chunk) || "MThd" != chunk.Key || 6>chunk.Value.Length)
				throw new InvalidDataException("The stream is not a MIDI file format.");
			var type = BitConverter.ToInt16(chunk.Value, 0);
			var trackCount = BitConverter.ToInt16(chunk.Value, 2);
			var timeBase = BitConverter.ToInt16(chunk.Value, 4);
			if(BitConverter.IsLittleEndian)
			{
				type = _Swap(type);
				trackCount = _Swap(trackCount);
				timeBase = _Swap(timeBase);
			}
			var result = new MidiFile(type,timeBase);
			while(_TryReadChunk(stream,out chunk))
			{
				if("MTrk"==chunk.Key)
				{
					var tstm = new MemoryStream(chunk.Value, false);
					var trk = MidiSequence.ReadFrom(tstm);
					result.Tracks.Add(trk);
				}
			}
			return result;
		}
		/// <summary>
		/// Reads a MIDI file from the specified file
		/// </summary>
		/// <param name="filename">The filename</param>
		/// <returns>A new instance representing the MIDI file</returns>
		public static MidiFile ReadFrom(string filename)
		{
			using (var stream = File.OpenRead(filename))
				return ReadFrom(stream);
		}
		/// <summary>
		/// Writes the MIDI file to the specified stream
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		public void WriteTo(Stream stream)
		{
			
			try
			{
				stream.SetLength(0);
			}
			catch { }
			var buf = new byte[4];
			buf[0] = (byte)'M'; buf[1] = (byte)'T'; buf[2] = (byte)'h';buf[3] = (byte)'d';
			stream.Write(buf, 0, 4);
			var len = 6;
			if (BitConverter.IsLittleEndian)
				len=_Swap(len);
			stream.Write(BitConverter.GetBytes(len),0,4);
			var type = Type;
			var trackCount = (short)Tracks.Count;
			var timeBase = TimeBase;
			if (BitConverter.IsLittleEndian)
			{
				type = _Swap(type);
				trackCount = _Swap(trackCount);
				timeBase = _Swap(timeBase);
			}
			stream.Write(BitConverter.GetBytes(type), 0, 2);
			stream.Write(BitConverter.GetBytes(trackCount), 0, 2);
			stream.Write(BitConverter.GetBytes(timeBase), 0, 2);
			foreach(var trk in Tracks)
			{
				buf[0] = (byte)'M'; buf[1] = (byte)'T'; buf[2] = (byte)'r'; buf[3] = (byte)'k';
				stream.Write(buf, 0, 4);
				var tstm = new MemoryStream();
				trk.WriteTo(tstm);
				tstm.Position = 0;
				len = (int)tstm.Length;
				if (BitConverter.IsLittleEndian)
					len = _Swap(len);
				stream.Write(BitConverter.GetBytes(len), 0, 4);
				tstm.CopyTo(stream); 
				tstm.Close();
			}
		}
		/// <summary>
		/// Writes the MIDI file to the specified file
		/// </summary>
		/// <param name="filename">The filename to write</param>
		public void WriteTo(string filename)
		{
			using (var stream = File.OpenWrite(filename))
				WriteTo(stream);
		}
		private static bool _TryReadChunk(Stream stream,out KeyValuePair<string, byte[]> chunk)
		{
			chunk = default(KeyValuePair<string, byte[]>);
			var buf = new byte[4];
			if (4 != stream.Read(buf, 0, 4)) return false;
			var name = Encoding.ASCII.GetString(buf);
			if (4 != stream.Read(buf, 0, 4)) throw new EndOfStreamException();
			var len = BitConverter.ToInt32(buf, 0);
			if (BitConverter.IsLittleEndian)
				len = _Swap(len);
			buf = new byte[len];
			if(len!=stream.Read(buf,0,len)) throw new EndOfStreamException();
			chunk = new KeyValuePair<string, byte[]>(name, buf);
			return true;
		}
		private static ushort _Swap(ushort x) { return (ushort)((ushort)((x & 0xff) << 8) | ((x >> 8) & 0xff)); }
		private static uint _Swap(uint x) { return ((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) + ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24); }
		private static ulong _Swap(ulong x)
		{
			return (
				   (0x00000000000000FF) & (x >> 56) | (0x000000000000FF00) & (x >> 40)
				 | (0x0000000000FF0000) & (x >> 24) | (0x00000000FF000000) & (x >> 8)
				 | (0x000000FF00000000) & (x << 8) | (0x0000FF0000000000) & (x << 24)
				 | (0x00FF000000000000) & (x << 40) | (0xFF00000000000000) & (x << 56)
				 );
		}
		private static short _Swap(short x) => unchecked((short)_Swap(unchecked((ushort)x)));
		private static int _Swap(int x) => unchecked((int)_Swap(unchecked((uint)x)));
		private static long _Swap(long value) => unchecked((long)_Swap(unchecked((ulong)value)));
	}
}
