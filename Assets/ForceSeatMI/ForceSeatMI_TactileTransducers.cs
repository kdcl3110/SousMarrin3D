/*
 * Copyright (C) 2012-2026 MotionSystems
 * 
 * This file is part of ForceSeatMI SDK.
 *
 * www.motionsystems.eu
 *
 */

using System;
using System.Runtime.InteropServices;

namespace MotionSystems
{
	/// <summary>
	/// This structure defines effects for tactile transducers. Signals of given frequency and amplitude are generates on associated audio outputs.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TactileAudioBasedFeedbackEffects
	{
		/// <summary>
		/// put here sizeof(FSMI_TactileAudioBasedFeedbackEffects)
		/// </summary>
		public byte structSize; 

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyCenter; 

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyEngine;

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyFL;

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyFR;

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyRL;

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyRR;

		/// <summary>
		/// frequency in Hz
		/// </summary>
		public uint frequencyCH;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeCenter;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeEngine;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeFL;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeFR;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeRL;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeRR;

		/// <summary>
		// signal amplitude from 0 to 1
		/// </summary>
		public float amplitudeCH;

		/// <summary>
		/// Helper method to create a structure with preallocated space for nested structures
		/// </summary>
		public static FSMI_TactileAudioBasedFeedbackEffects Prepare()
		{
			var tabfEffects        = new FSMI_TactileAudioBasedFeedbackEffects();
			tabfEffects.structSize = (byte)Marshal.SizeOf(tabfEffects);
			return tabfEffects;
		}
	}
}
