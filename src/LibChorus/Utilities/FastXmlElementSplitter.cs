using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Chorus.Utilities
{
	///<summary>
	/// Responsible to read a file which has a sequence of similar elements
	/// and return byte arrays of each element for further processing.
	///</summary>
	public class FastXmlElementSplitter : IDisposable
	{
		private readonly static Encoding _encUtf8 = Encoding.UTF8;
		private readonly static List<byte> _endingWhitespace = new List<byte>
			{
				_encUtf8.GetBytes(" ")[0],
				_encUtf8.GetBytes("\t")[0],
				_encUtf8.GetBytes("\r")[0],
				_encUtf8.GetBytes("\n")[0]
			};

		private readonly string _pathname;
		private int _startOfRecordsOffset;
		private int _endOfRecordsOffset;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="pathname">Pathname of file to process.</param>
		public FastXmlElementSplitter(string pathname)
		{
			if (string.IsNullOrEmpty(pathname))
				throw new ArgumentException("Null or empty input", "pathname");

			if (!File.Exists(pathname))
				throw new FileNotFoundException("File was not found.", "pathname");

			_pathname = pathname;
		}

		///<summary>
		/// Return the second level elements that are in the input file.
		///</summary>
		///<param name="recordMarker">The element name of elements that are children of the main root elment.</param>
		///<returns>A collection of byte arrays of the records.</returns>
		/// <remarks>
		/// <para>
		/// <paramref name="recordMarker"/> should not contain the angle brackets that start/end an xml element.
		/// </para>
		/// <para>
		/// The input file can contain child elements of the main root element, which are not the same elements
		/// as those marked with <paramref name="recordMarker"/>, but if the file has them, they must be before the
		/// other <paramref name="recordMarker"/> elments.
		/// </para>
		/// </remarks>
		/// <exception cref="ArgumentException">
		/// Thrown if <paramref name="recordMarker"/> is null, an empty string, or there are no such records in the file.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the input file is not xml.
		/// </exception>
		public IEnumerable<byte[]> GetSecondLevelElementBytes(string recordMarker)
		{
			if (string.IsNullOrEmpty(recordMarker))
				throw new ArgumentException("Null or empty string.", "recordMarker");

			var inputBytes = File.ReadAllBytes(_pathname);
			var recordMarkerAsBytes = FormatRecordMarker(recordMarker);
			var openingAngleBracket = _encUtf8.GetBytes("<")[0];

			InitializeOffsets(openingAngleBracket, inputBytes, recordMarkerAsBytes);

			// Find the records.
			var results = new List<byte[]>(inputBytes.Length / 400); // Reasonable guess on size to avoid resizing so much.
			for (var i = _startOfRecordsOffset; i < _endOfRecordsOffset; ++i)
			{
				var endOffset = FindStartOfMainRecordOffset(i + 1, openingAngleBracket, inputBytes, recordMarkerAsBytes);
				// We should have the complete <foo> element now.
				results.Add(inputBytes.SubArray(i, endOffset - i));
				i = endOffset - 1;
			}
			return results;
		}

		///<summary>
		/// Return the second level elements that are in the input file.
		///</summary>
		///<param name="recordMarker">The element name of elements that are children of the main root elment.</param>
		///<returns>A collection of strings of the records.</returns>
		/// <remarks>
		/// <para>
		/// <paramref name="recordMarker"/> should not contain the angle brackets that start/end an xml element.
		/// </para>
		/// <para>
		/// The input file can contain child elements of the main root element, which are not the same elements
		/// as those marked with <paramref name="recordMarker"/>, but if the file has them, they must be before the
		/// other <paramref name="recordMarker"/> elments.
		/// </para>
		/// </remarks>
		/// <exception cref="ArgumentException">
		/// Thrown if <paramref name="recordMarker"/> is null, an empty string, or there are no such records in the file.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the input file is not xml.
		/// </exception>
		public IEnumerable<string> GetSecondLevelElementStrings(string recordMarker)
		{
			return new List<string>(
				GetSecondLevelElementBytes(recordMarker)
					.Select(byteResult => _encUtf8.GetString(byteResult)));
		}

		/// <summary>
		/// This method adjusts _startOfRecordsOffset to the offset to the start of the records,
		/// and adjusts _endOfRecordsOffset to the end of the last record.
		/// </summary>
		private void InitializeOffsets(byte openingAngleBracket, byte[] inputBytes, byte[] recordMarkerAsBytes)
		{
			// Find offset for end of records.
			_endOfRecordsOffset = 0;
			for (var i = inputBytes.Length - 1; i >= 0; --i)
			{
				if (inputBytes[i] != openingAngleBracket)
					continue;

				_endOfRecordsOffset = i;
				break;
			}
			if (_endOfRecordsOffset == 0)
				throw new InvalidOperationException("There was no main ending tag in the file.");

			// Find offset for first record.
			_startOfRecordsOffset = FindStartOfMainRecordOffset(0, openingAngleBracket, inputBytes, recordMarkerAsBytes);
			if (_startOfRecordsOffset == _endOfRecordsOffset)
				throw new InvalidOperationException("There was no main starting tag in the file.");
		}

		private int FindStartOfMainRecordOffset(int currentOffset, byte openingAngleBracket, byte[] inputBytes, byte[] recordMarkerAsBytes)
		{
			// Need to get the next starting marker, or the main closing tag
			// When the end point is found, call _outputHandler with the current array
			// from 'offset' to 'i' (more or less).
			// Skip quickly over anything that doesn't match even one character.
			for (var i = currentOffset; i < _endOfRecordsOffset; ++i)
			{
				var currentByte = inputBytes[i];
				// Need to get the next starting marker, or the main closing tag
				// When the end point is found, call _outputHandler with the current array
				// from 'offset' to 'i' (more or less).
				// Skip quickly over anything that doesn't match even one character.
				if (currentByte != openingAngleBracket)
					continue;

				// Try to match the rest of the marker.
				for (var j = 1; ; j++)
				{
					var current = inputBytes[i + j];
					if (_endingWhitespace.Contains(current))
					{
						// Got it!
						return i;
					}
					if (recordMarkerAsBytes[j] != current)
						break; // no match, resume searching for opening character.
					if (j != recordMarkerAsBytes.Length - 1)
						continue;
				}
			}

			return _endOfRecordsOffset; // Found the end.
		}

		private static byte[] FormatRecordMarker(string recordMarker)
		{
			return _encUtf8.GetBytes("<" + recordMarker.Replace("<", null).Replace(">", null).Trim());
		}

		~FastXmlElementSplitter()
		{
			Debug.WriteLine("**** FastXmlElementSplitter.Finalizer called ****");
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		private bool IsDisposed
		{ get; set; }

		private void Dispose(bool disposing)
		{
			if (IsDisposed)
				return; // Done already, so nothing left to do.

			if (disposing)
			{
			}

			// Dispose unmanaged resources here, whether disposing is true or false.

			// Main data members.

			IsDisposed = true;
		}
	}
}